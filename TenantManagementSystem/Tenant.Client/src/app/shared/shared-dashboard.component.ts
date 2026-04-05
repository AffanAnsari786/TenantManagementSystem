import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBarModule, MatSnackBar } from '@angular/material/snack-bar';
import { ShareService, SharedEntry } from '../services/share.service';
import { SignalRService } from '../services/signalr.service';
import { ReceiptService } from '../services/receipt.service';

interface PaymentRecord {
  id: string;
  rentPeriod: string;
  amount: number;
  receivedDate: string;
  createdDate: string;
  tenantSign?: string;
}
@Component({
  selector: 'app-shared',
  imports: [CommonModule,MatCardModule,MatTableModule,MatIconModule,MatButtonModule,MatSnackBarModule],
  templateUrl: './shared-dashboard.component.html',
  styleUrl: './shared-dashboard.component.scss'
})
export class SharedDashboardComponent implements OnInit, OnDestroy {
  dashboardData: SharedEntry | null = null;
  paymentRecords: PaymentRecord[] = [];
  displayedColumns: string[] = ['rentPeriod', 'amount', 'receivedDate', 'status'];
  loading: boolean = true;
  error: string | null = null;

  private shareToken: string = '';
  private signalRJoined = false;
  /** Polling fallback so shared view updates even if SignalR fails (e.g. CORS, port). */
  private pollIntervalId: ReturnType<typeof setInterval> | null = null;
  private readonly POLL_INTERVAL_MS = 5000;

  get hasPayments(): boolean {
    return this.paymentRecords.length > 0;
  }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private shareService: ShareService,
    private signalRService: SignalRService,
    private snackBar: MatSnackBar,
    private receiptService: ReceiptService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  ngOnInit(): void {
    const token = this.route.snapshot.paramMap.get('token');
    if (token) {
      this.shareToken = token;
      if (isPlatformBrowser(this.platformId)) {
        this.loadSharedDashboard(token);
      } else {
        this.loading = true;
      }
    } else {
      this.error = 'Invalid share link';
      this.loading = false;
    }
  }

  ngOnDestroy(): void {
    if (this.pollIntervalId) {
      clearInterval(this.pollIntervalId);
      this.pollIntervalId = null;
    }
    this.signalRService.offEntryUpdated();
    this.signalRService.disconnect();
  }

  /**
   * Shared link flow: When you copy the share link and open it in another tab (or send to another user),
   * the app loads /shared/{token}. This component calls GET /api/share/{token} with no login required.
   * The API looks up the token in SharedLinks, returns the linked Entry and its Records. Anyone with
   * the link can view that dashboard read-only. Ensure the API is running and the link is not expired.
   */
  private normalizeSharedEntry(data: SharedEntry | Record<string, unknown>): SharedEntry {
    const raw = data as Record<string, unknown>;
    const records = raw['records'] ?? raw['Records'];
    return {
      id: (raw['id'] ?? raw['Id']) as string,
      name: (raw['name'] ?? raw['Name']) as string,
      startDate: (raw['startDate'] ?? raw['StartDate']) as string,
      endDate: (raw['endDate'] ?? raw['EndDate']) as string,
      records: Array.isArray(records) ? (records as SharedEntry['records']) : []
    };
  }

  private updateDashboardData(data: SharedEntry): void {
    const previousRecordsCount = this.paymentRecords.length;
    const previousRecordIds = this.paymentRecords.map(r => r.id);
    const previousSignature = this.recordsSignature(this.paymentRecords);

    this.dashboardData = data;
    const newRecords = data.records || [];
    const newRecordIds = newRecords.map(r => r.id);
    const newSignature = this.recordsSignature(newRecords);

    this.paymentRecords = newRecords;

    if (!this.loading) {
      if (newRecords.length > previousRecordsCount) {
        const newRecordsCount = newRecords.length - previousRecordsCount;
        this.snackBar.open(
          `Dashboard updated! ${newRecordsCount} new payment(s) added.`,
          'Close',
          { duration: 3000, panelClass: ['success-snackbar'] }
        );
      } else if (newRecords.length < previousRecordsCount) {
        const deletedRecordsCount = previousRecordsCount - newRecords.length;
        this.snackBar.open(
          `Dashboard updated! ${deletedRecordsCount} payment(s) deleted.`,
          'Close',
          { duration: 3000, panelClass: ['info-snackbar'] }
        );
      } else if (previousRecordsCount > 0 && (previousSignature !== newSignature || !this.arraysEqual(previousRecordIds, newRecordIds))) {
        this.snackBar.open(
          `Dashboard updated! Payment entries modified.`,
          'Close',
          { duration: 3000, panelClass: ['info-snackbar'] }
        );
      }
    }
  }

  private recordsSignature(records: PaymentRecord[]): string {
    return records
      .map(r => `${r.id}:${r.amount}:${r.receivedDate}:${r.rentPeriod ?? ''}`)
      .sort()
      .join('|');
  }

  private arraysEqual(a: string[], b: string[]): boolean {
    if (a.length !== b.length) return false;
    const sortedA = [...a].sort();
    const sortedB = [...b].sort();
    return sortedA.every((val, index) => val === sortedB[index]);
  }

  private loadSharedDashboard(token: string): void {
    if (!isPlatformBrowser(this.platformId)) return;

    // Pass token as-is; avoid double-decode (e.g. + in URL can become space)
    const safeToken = decodeURIComponent(token || '').trim() || token;
    this.shareService.getSharedDashboard(safeToken).subscribe({
      next: (data) => {
        this.updateDashboardData(this.normalizeSharedEntry(data));
        this.loading = false;

        // Start polling so shared view auto-refreshes even when SignalR doesn't connect
        if (!this.pollIntervalId && this.shareToken) {
          this.pollIntervalId = setInterval(() => {
            if (this.shareToken) this.loadSharedDashboard(this.shareToken);
          }, this.POLL_INTERVAL_MS);
        }

        const entryId = this.dashboardData?.id;
        if (entryId != null && isPlatformBrowser(this.platformId) && !this.signalRJoined) {
          this.signalRJoined = true;
          this.signalRService.connect().then(() => {
            this.signalRService.onEntryUpdated(() => {
              this.loadSharedDashboard(this.shareToken);
            });
            this.signalRService.joinEntry(entryId, this.shareToken);
          }).catch((err) => {
            console.warn('SignalR connect failed:', err);
            this.signalRJoined = false;
          });
        }
      },
      error: (error) => {
        console.error('Error loading shared dashboard:', error);
        if (error?.status === 404) {
          this.error = 'This shared link has expired or is no longer valid.';
        } else if (error?.status === 0 || error?.message?.includes('Http failure')) {
          this.error = 'Cannot reach the server. Make sure the API is running (e.g. on the same machine) and try again.';
        } else {
          this.error = 'Unable to load the shared dashboard. Please try again later.';
        }
        this.loading = false;
        this.signalRService.disconnect();
      }
    });
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  downloadReceipt(recordId: string) {
    if (!this.dashboardData || !this.shareToken) return;
    
    // We pass the share token directly as the publicId. 
    // The backend `publicId` query param actually expects the Entry's PublicId Guid,
    // wait - looking at ReceiptsController: public async Task<IActionResult> DownloadSharedReceipt(int recordId, [FromQuery] string publicId)
    // and ReceiptService: if (Guid.TryParse(publicId, out Guid parsedId)) { query = query.Where(r => r.Entry!.PublicId == parsedId); }
    // We need the `PublicId` (the Guid).
    // Let's modify the shared receipt download to accept the share token instead, or pass the entry ID.
    // Wait, `ReceiptsController` expects `publicId`. In `SharedEntry` we have `id` which is the Entry's PublicId (since Entry Service maps PublicId to `id`).
    // Let's pass `this.dashboardData.id`. 
    
    if (!isPlatformBrowser(this.platformId)) return;
    this.receiptService.downloadSharedReceipt(recordId, this.dashboardData.id).subscribe({
      next: (blob: Blob) => {
        const pdfBlob = blob.type ? blob : new Blob([blob], { type: 'application/pdf' });
        const url = window.URL.createObjectURL(pdfBlob);
        const tenantNameSanitized = this.dashboardData!.name.replace(/[^a-zA-Z0-9]/g, '_');
        const record = this.paymentRecords.find(r => r.id === recordId);
        let monthYear = '';
        if (record && record.rentPeriod) {
            monthYear = new Date(record.rentPeriod).toLocaleDateString('en-US', { month: 'long', year: 'numeric' }).replace(' ', '');
        } else {
            monthYear = new Date().toLocaleDateString('en-US', { month: 'long', year: 'numeric' }).replace(' ', '');
        }
        const fileName = `Receipt_${monthYear}_${tenantNameSanitized}.pdf`;

        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        a.style.display = 'none';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(() => window.URL.revokeObjectURL(url), 1000);
      },
      error: (error) => {
        console.error('Error downloading shared receipt:', error);
        this.snackBar.open('Failed to download receipt', 'Close', { duration: 3000 });
      }
    });
  }
}
