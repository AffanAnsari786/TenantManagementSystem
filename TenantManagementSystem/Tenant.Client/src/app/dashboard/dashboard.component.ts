import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBarModule, MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ShareLinkRequest, ShareService } from '../services/share.service';
import { CreateEntryRequest, CreateRecordRequest, Entry, EntryRecord, EntryService, UpdateRecordRequest } from '../services/entry.service';
import { TenantDataService } from '../services/tenant-data.service';
import { ReceiptService } from '../services/receipt.service';
import { SignalRService } from '../services/signalr.service';
import { EntryFormComponent } from '../components/entry-form/entry-form.component';
import { ShareModalComponent, ShareModalData } from '../components/share-modal/share-modal.component';

/**
 * UI-local projection of EntryRecord with dates already parsed — the shared
 * Entry type in entry.service.ts uses ISO strings (wire format), so the view
 * layer converts once on load and keeps strongly-typed Date values after that.
 */
interface PaymentEntry {
  id: string;
  rentPeriod: Date;
  amount: number;
  receivedDate: Date;
  createdDate: Date;
}

function toPaymentEntry(r: EntryRecord): PaymentEntry {
  return {
    id: r.id,
    rentPeriod: new Date(r.rentPeriod),
    amount: r.amount,
    receivedDate: new Date(r.receivedDate),
    createdDate: new Date(r.createdDate)
  };
}

@Component({
  selector: 'app-dashboard',
  imports: [
    CommonModule, 
    RouterModule, 
    MatCardModule, 
    MatButtonModule, 
    MatTableModule, 
    MatIconModule,
    MatTooltipModule,
    MatSnackBarModule,
    MatDialogModule
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit, OnDestroy {
  tenantName: string = '';
  startDate: Date = new Date();
  endDate: Date = new Date();
  address: string = '';
  aadhaarNumber: string = '';
  propertyName: string = '';
  paymentEntries: PaymentEntry[] = [];
  displayedColumns: string[] = ['rentPeriod', 'amount', 'receivedDate', 'actions'];
  currentEntryId: string = '';
  loading = true;

  get hasPayments(): boolean {
    return this.paymentEntries.length > 0;
  }

  private signalRJoinedEntryId: string | null = null;

  constructor(
    private router: Router,
    private route: ActivatedRoute,
    private dialog: MatDialog,
    private shareService: ShareService,
    private snackBar: MatSnackBar,
    private entryService: EntryService,
    private tenantDataService: TenantDataService,
    private receiptService: ReceiptService,
    private signalRService: SignalRService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      const entryIdParam = params.get('entryId');
      if (entryIdParam && entryIdParam !== '0' && entryIdParam !== 'new') {
          this.loadEntryById(entryIdParam);
          return;
      }
      this.handleNoEntryId();
    });
  }

  ngOnDestroy() {
    this.signalRService.offEntryUpdated();
    this.signalRService.disconnect();
  }

  /**
   * Owner-side live sync: after loading an entry, join its SignalR group so
   * that edits made in another tab/device push a silent refresh here.
   */
  private ensureSignalRSubscribed(entryId: string): void {
    if (!isPlatformBrowser(this.platformId)) return;
    if (this.signalRJoinedEntryId === entryId) return;

    this.signalRJoinedEntryId = entryId;
    this.signalRService.connect().then(() => {
      this.signalRService.onEntryUpdated(() => {
        // Silent reload — no spinner flicker.
        this.loadEntryById(entryId, true);
      });
      return this.signalRService.joinEntry(entryId);
    }).catch(err => {
      console.warn('SignalR connect failed on owner dashboard:', err);
      this.signalRJoinedEntryId = null;
    });
  }

  private applyEntry(entry: Entry): void {
    this.currentEntryId = entry.id;
    this.tenantName = entry.name;
    this.startDate = new Date(entry.startDate);
    this.endDate = new Date(entry.endDate);
    this.address = entry.address || 'No address provided';
    this.aadhaarNumber = entry.aadhaarNumber || 'Not provided';
    this.propertyName = entry.propertyName || 'Unassigned Property';
    this.paymentEntries = (entry.records || []).map(toPaymentEntry);
  }

  private loadEntryById(entryId: string, silent: boolean = false): void {
    if (!silent) this.loading = true;
    this.entryService.getEntry(entryId).subscribe({
      next: (entry: Entry) => {
        this.applyEntry(entry);
        this.loading = false;
        this.ensureSignalRSubscribed(entry.id);
      },
      error: (err) => {
        this.loading = false;
        if (err?.status === 401) {
          window.location.href = '/login';
          return;
        }
        if (err?.status === 404) {
          this.router.navigate(['/all-tenants']);
          return;
        }
        this.snackBar.open('Failed to load tenant dashboard.', 'Close', { duration: 3000 });
      }
    });
  }

  private handleNoEntryId(): void {
    if (!isPlatformBrowser(this.platformId)) {
      this.loading = false;
      return;
    }
    const serviceData = this.tenantDataService.getTenantData();
    const historyState = history.state as { newEntry?: any } | null;
    const newEntryData = serviceData || historyState?.newEntry;

    if (newEntryData) {
      const createRequest: CreateEntryRequest = {
        name: newEntryData.name,
        startDate: new Date(newEntryData.startDate).toISOString(),
        endDate: new Date(newEntryData.endDate).toISOString(),
        address: newEntryData.address,
        aadhaarNumber: newEntryData.aadhaarNumber,
        propertyName: newEntryData.propertyName
      };
      this.entryService.createEntry(createRequest).subscribe({
        next: (entry) => {
          this.tenantDataService.clearTenantData();
          this.router.navigate(['/dashboard', entry.id], { replaceUrl: true });
        },
        error: (err) => {
          this.snackBar.open('Error creating tenant entry', 'Close', { duration: 3000 });
          this.router.navigate(['/all-tenants']);
        }
      });
    } else {
      this.router.navigate(['/all-tenants']);
    }
  }

  onAddPayment() {
    // If no entry exists, create one first
    if (!this.currentEntryId || this.currentEntryId === '') {
      this.createDefaultEntryAndAddPayment();
      return;
    }

    const dialogRef = this.dialog.open(EntryFormComponent, {
      width: '500px',
      data: {
        mode: 'create'
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.action === 'create' && result?.data) {
        this.addPaymentRecord(result.data);
      }
    });
  }

  private createDefaultEntryAndAddPayment() {
    const createRequest: CreateEntryRequest = {
      name: this.tenantName || 'Default Tenant',
      startDate: this.startDate.toISOString(),
      endDate: this.endDate.toISOString()
    };

    this.entryService.createEntry(createRequest).subscribe({
      next: (entry) => {
        this.currentEntryId = entry.id;
        this.tenantName = entry.name;
        this.startDate = new Date(entry.startDate);
        this.endDate = new Date(entry.endDate);
        // Now open the payment form
        this.onAddPayment();
      },
      error: (error) => {
        console.error('Error creating entry:', error);
        this.snackBar.open('Error creating tenant entry', 'Close', { duration: 3000 });
      }
    });
  }

  private addPaymentRecord(formData: any) {
    // Convert string dates to ISO strings for API
    const rentPeriodDate = new Date(formData.rentPeriod);
    const receivedDate = new Date(formData.receivedDate);
    
    const recordRequest: CreateRecordRequest = {
      rentPeriod: rentPeriodDate.toISOString(),
      amount: Number(formData.amount),
      receivedDate: receivedDate.toISOString()
    };

    this.entryService.addRecord(this.currentEntryId, recordRequest).subscribe({
      next: (record) => {
        this.paymentEntries = [toPaymentEntry(record), ...this.paymentEntries];
      },
      error: (error) => {
        console.error('Error adding payment record:', error);
        this.snackBar.open('Error adding payment record', 'Close', { duration: 3000 });
      }
    });
  }

  onPreviewEntry(entry: PaymentEntry) {
    this.dialog.open(EntryFormComponent, {
      width: '500px',
      data: {
        entry,
        mode: 'preview'
      }
    });
  }

  onEditEntry(entry: PaymentEntry) {
    const dialogRef = this.dialog.open(EntryFormComponent, {
      width: '500px',
      data: {
        entry,
        mode: 'edit'
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result && result.action === 'update') {
        // Convert string dates to API format
        const updateRequest: UpdateRecordRequest = {
          rentPeriod: new Date(result.data.rentPeriod).toISOString(),
          amount: Number(result.data.amount),
          receivedDate: new Date(result.data.receivedDate).toISOString()
        };

        this.entryService.updateRecord(this.currentEntryId, entry.id, updateRequest).subscribe({
          next: (updatedRecord) => {
            const updatedEntry: PaymentEntry = {
              ...toPaymentEntry(updatedRecord),
              createdDate: entry.createdDate // keep original created date
            };

            const index = this.paymentEntries.findIndex(e => e.id === entry.id);
            if (index !== -1) {
              this.paymentEntries = [
                ...this.paymentEntries.slice(0, index),
                updatedEntry,
                ...this.paymentEntries.slice(index + 1)
              ];
            }

            this.snackBar.open('Payment entry updated successfully', 'Close', { duration: 3000 });
          },
          error: (error) => {
            console.error('Error updating payment record:', error);
            this.snackBar.open('Error updating payment entry', 'Close', { duration: 3000 });
          }
        });
      }
    });
  }

  onDeleteEntry(entry: PaymentEntry): void {
    if (confirm(`Are you sure you want to delete the payment entry for ${entry.rentPeriod.toLocaleDateString('en-US', { month: 'long', year: 'numeric' })}?`)) {
      this.entryService.deleteRecord(this.currentEntryId, entry.id).subscribe({
        next: () => {
          // Remove from local array
          this.paymentEntries = this.paymentEntries.filter(e => e.id !== entry.id);
          this.snackBar.open('Payment entry deleted successfully', 'Close', { duration: 3000 });
        },
        error: (error) => {
          console.error('Error deleting payment record:', error);
          this.snackBar.open('Error deleting payment entry', 'Close', { duration: 3000 });
        }
      });
    }
  }

  // generateId not needed


  onShareDashboard(): void {
    if (!this.hasPayments) {
      return;
    }

    // Open modal with loading state
    const dialogRef = this.dialog.open(ShareModalComponent, {
      width: '520px',
      maxWidth: '95vw',
      maxHeight: '90vh',
      disableClose: true,
      panelClass: 'share-modal-panel',
      autoFocus: false,
      restoreFocus: false,
      data: {
        shareUrl: '',
        expiryDate: new Date(),
        isLoading: true
      } as ShareModalData
    });

    const request: ShareLinkRequest = {
      entryId: this.currentEntryId,
      expiryDays: 30
    };

    this.shareService.generateShareLink(request).subscribe({
      next: (response) => {
        // Update modal with the actual data
        dialogRef.componentInstance.data = {
          shareUrl: response.shareUrl,
          expiryDate: new Date(response.expiryDate),
          isLoading: false
        };
      },
      error: (error) => {
        console.error('Error generating share link:', error);
        dialogRef.close();
        this.snackBar.open(
          'Failed to generate share link. Please try again.', 
          'Close', 
          { 
            duration: 3000,
            panelClass: ['error-snackbar']
          }
        );
      }
    });
  }

  formatDate(date: Date): string {
    return new Date(date).toLocaleDateString('en-US', { 
      year: 'numeric', 
      month: 'long', 
      day: 'numeric' 
    });
  }

  getTotalRevenue(): number {
    return this.paymentEntries.reduce((total, entry) => total + entry.amount, 0);
  }

  getAveragePayment(): number {
    if (this.paymentEntries.length === 0) return 0;
    return this.getTotalRevenue() / this.paymentEntries.length;
  }

  getRecentPaymentsCount(): number {
    const now = new Date();
    const currentMonth = now.getMonth();
    const currentYear = now.getFullYear();
    
    return this.paymentEntries.filter(entry => {
      const entryDate = new Date(entry.receivedDate);
      return entryDate.getMonth() === currentMonth && entryDate.getFullYear() === currentYear;
    }).length;
  }

  downloadReceipt(recordId: string) {
    if (!isPlatformBrowser(this.platformId)) return;
    this.receiptService.downloadReceipt(recordId).subscribe({
      next: (blob: Blob) => {
        const pdfBlob = blob.type ? blob : new Blob([blob], { type: 'application/pdf' });
        const url = window.URL.createObjectURL(pdfBlob);
        const tenantNameSanitized = this.tenantName.replace(/[^a-zA-Z0-9]/g, '_');
        const monthYear = new Date().toLocaleDateString('en-US', { month: 'long', year: 'numeric' }).replace(' ', '');
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
      error: (error: any) => {
        console.error('Error downloading receipt:', error);
        this.snackBar.open('Failed to download receipt', 'Close', { duration: 3000 });
      }
    });
  }
} 
