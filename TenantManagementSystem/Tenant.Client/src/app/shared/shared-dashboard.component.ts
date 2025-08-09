import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBarModule, MatSnackBar } from '@angular/material/snack-bar';
import { ShareService, SharedEntry } from '../services/share.service';
import { interval, Subscription } from 'rxjs';
import { switchMap, catchError } from 'rxjs/operators';
import { of } from 'rxjs';

interface PaymentRecord {
  id: number;
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
  
  private refreshSubscription: Subscription | null = null;
  private shareToken: string = '';
  private refreshInterval = 5000; // 5 seconds

  get hasPayments(): boolean {
    return this.paymentRecords.length > 0;
  }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private shareService: ShareService,
    private snackBar: MatSnackBar,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  ngOnInit(): void {
    const token = this.route.snapshot.paramMap.get('token');
    if (token) {
      this.shareToken = token;
      // Only load data in browser environment
      if (isPlatformBrowser(this.platformId)) {
        this.loadSharedDashboard(token);
        this.startAutoRefresh();
      } else {
        // During SSR, show loading state
        this.loading = true;
      }
    } else {
      this.error = 'Invalid share link';
      this.loading = false;
    }
  }

  ngOnDestroy(): void {
    this.stopAutoRefresh();
  }

  private startAutoRefresh(): void {
    // Only start auto-refresh in browser environment
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    
    this.refreshSubscription = interval(this.refreshInterval)
      .pipe(
        switchMap(() => this.shareService.getSharedDashboard(this.shareToken)),
        catchError((error) => {
          console.error('Auto-refresh error:', error);
          // Don't stop refreshing on error, just continue
          return of(null);
        })
      )
      .subscribe({
        next: (data) => {
          if (data) {
            this.updateDashboardData(data);
          }
        },
        error: (error) => {
          console.error('Refresh subscription error:', error);
        }
      });
  }

  private stopAutoRefresh(): void {
    if (this.refreshSubscription) {
      this.refreshSubscription.unsubscribe();
      this.refreshSubscription = null;
    }
  }

  private updateDashboardData(data: SharedEntry): void {
    const previousRecordsCount = this.paymentRecords.length;
    const previousRecordIds = this.paymentRecords.map(r => r.id);
    
    this.dashboardData = data;
    const newRecords = data.records || [];
    const newRecordIds = newRecords.map(r => r.id);
    
    this.paymentRecords = newRecords;
    
    if (!this.loading) {
      // Check for added records
      if (newRecords.length > previousRecordsCount) {
        const newRecordsCount = newRecords.length - previousRecordsCount;
        this.snackBar.open(
          `Dashboard updated! ${newRecordsCount} new payment(s) added.`,
          'Close',
          {
            duration: 3000,
            panelClass: ['success-snackbar']
          }
        );
      }
      // Check for deleted records
      else if (newRecords.length < previousRecordsCount) {
        const deletedRecordsCount = previousRecordsCount - newRecords.length;
        this.snackBar.open(
          `Dashboard updated! ${deletedRecordsCount} payment(s) deleted.`,
          'Close',
          {
            duration: 3000,
            panelClass: ['info-snackbar']
          }
        );
      }
      // Check for updated records (same count but different data)
      else if (previousRecordsCount > 0 && !this.arraysEqual(previousRecordIds, newRecordIds)) {
        this.snackBar.open(
          `Dashboard updated! Payment entries modified.`,
          'Close',
          {
            duration: 3000,
            panelClass: ['info-snackbar']
          }
        );
      }
    }
  }

  private arraysEqual(a: number[], b: number[]): boolean {
    if (a.length !== b.length) return false;
    const sortedA = [...a].sort();
    const sortedB = [...b].sort();
    return sortedA.every((val, index) => val === sortedB[index]);
  }

  private loadSharedDashboard(token: string): void {
    // Only make API calls in browser environment
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    
    this.shareService.getSharedDashboard(token).subscribe({
      next: (data) => {
        this.updateDashboardData(data);
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading shared dashboard:', error);
        if (error.status === 404) {
          this.error = 'This shared link has expired or is no longer valid.';
        } else if (error.status === 0) {
          this.error = 'Unable to connect to the server. Please check your internet connection and try again.';
        } else {
          this.error = 'Unable to load the shared dashboard. Please try again later.';
        }
        this.loading = false;
        this.stopAutoRefresh(); // Stop auto-refresh on error
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
}
