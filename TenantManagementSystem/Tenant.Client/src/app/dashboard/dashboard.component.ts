import { Component, OnInit, Inject, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBarModule, MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ShareLinkRequest, ShareService } from '../services/share.service';
import { CreateEntryRequest, CreateRecordRequest, EntryService, UpdateRecordRequest } from '../services/entry.service';
import { TenantDataService } from '../services/tenant-data.service';
import { EntryFormComponent } from '../components/entry-form/entry-form.component';
import { ShareModalComponent, ShareModalData } from '../components/share-modal/share-modal.component';

interface Entry {
  id: number;
  name: string;
  startDate: Date;
  endDate: Date;
  rentPeriod: Date;
  amount: number;
  receivedDate: Date;
  createdDate: Date;
}

interface PaymentEntry {
  id: number;
  rentPeriod: Date;
  amount: number;
  receivedDate: Date;
  createdDate: Date;
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
export class DashboardComponent implements OnInit {
  tenantName: string = '';
  startDate: Date = new Date();
  endDate: Date = new Date();
  paymentEntries: PaymentEntry[] = [];
  displayedColumns: string[] = ['rentPeriod', 'amount', 'receivedDate', 'actions'];
  currentEntryId: number = 0; // This will be set when entry is created or loaded

  get hasPayments(): boolean {
    return this.paymentEntries.length > 0;
  }

  constructor(
    private router: Router,
    private dialog: MatDialog,
    private shareService: ShareService,
    private snackBar: MatSnackBar,
    private entryService: EntryService,
    private tenantDataService: TenantDataService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    // Constructor logic moved to ngOnInit for proper initialization
  }

  ngOnInit() {
    this.loadDashboardData();
  }

  private loadDashboardData() {
    // Check service first, then navigation state with new entry data
    const serviceData = this.tenantDataService.getTenantData();
    const routerState = this.router.getCurrentNavigation()?.extras?.state as { newEntry: any } | null;
    
    // Only access history in browser environment
    let historyState: { newEntry: any } | null = null;
    if (isPlatformBrowser(this.platformId)) {
      historyState = history.state as { newEntry: any } | null;
    }
    
    // Try service data first, then router state, then history state
    const newEntryData = serviceData || routerState?.newEntry || historyState?.newEntry;
    
    console.log('Service data:', serviceData);
    console.log('Navigation state:', routerState);
    console.log('History state:', historyState);
    console.log('Final new entry data:', newEntryData);
    
    if (newEntryData) {
      // Create entry on backend  
      const createRequest: CreateEntryRequest = {
        name: newEntryData.name,
        startDate: new Date(newEntryData.startDate).toISOString(),
        endDate: new Date(newEntryData.endDate).toISOString()
      };

      console.log('Creating entry with request:', createRequest);

      this.entryService.createEntry(createRequest).subscribe({
        next: (entry) => {
          console.log('Entry created successfully:', entry);
          this.currentEntryId = entry.id;
          this.tenantName = entry.name;
          this.startDate = new Date(entry.startDate);
          this.endDate = new Date(entry.endDate);
          this.paymentEntries = entry.records.map(r => ({
            id: r.id,
            rentPeriod: new Date(r.rentPeriod),
            amount: r.amount,
            receivedDate: new Date(r.receivedDate),
            createdDate: new Date(r.createdDate)
          }));
          
          // Clear the service data after using it
          this.tenantDataService.clearTenantData();
        },
        error: (error) => {
          console.error('Error creating entry:', error);
          this.snackBar.open('Error creating tenant entry', 'Close', { duration: 3000 });
        }
      });
    } else {
      console.log('No new entry data found, loading existing entries');
      // Load existing entries
      // Only make API calls in browser environment
      if (isPlatformBrowser(this.platformId)) {
        this.entryService.getEntries().subscribe({
          next: (entries) => {
            console.log('Loaded entries:', entries);
            if (entries.length > 0) {
              const entry = entries[0]; // Use the first entry for now
              this.currentEntryId = entry.id;
              this.tenantName = entry.name;
              this.startDate = new Date(entry.startDate);
              this.endDate = new Date(entry.endDate);
              this.paymentEntries = entry.records.map(r => ({
                id: r.id,
                rentPeriod: new Date(r.rentPeriod),
                amount: r.amount,
                receivedDate: new Date(r.receivedDate),
                createdDate: new Date(r.createdDate)
              }));
            } else {
              // No entries found, user probably came directly to dashboard
              // We'll create an entry when they add their first payment
              console.log('No entries found, setting default values');
              this.tenantName = 'Default Tenant';
              this.startDate = new Date();
              this.endDate = new Date(Date.now() + 365 * 24 * 60 * 60 * 1000); // 1 year from now
            }
          },
          error: (error) => {
            console.error('Error loading entries:', error);
            // Set default values on error
            this.tenantName = 'Default Tenant';
            this.startDate = new Date();
            this.endDate = new Date(Date.now() + 365 * 24 * 60 * 60 * 1000);
          }
        });
      } else {
        // During SSR, set default values
        console.log('SSR mode - setting default values');
        this.tenantName = 'Default Tenant';
        this.startDate = new Date();
        this.endDate = new Date(Date.now() + 365 * 24 * 60 * 60 * 1000);
      }
    }
  }

  onAddPayment() {
    // If no entry exists, create one first
    if (!this.currentEntryId || this.currentEntryId === 0) {
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
        const newPayment = {
          id: record.id,
          rentPeriod: new Date(record.rentPeriod),
          amount: record.amount,
          receivedDate: new Date(record.receivedDate),
          createdDate: new Date(record.createdDate)
        };
        this.paymentEntries = [newPayment, ...this.paymentEntries];
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
            // Update the local array with API response
            const updatedEntry = {
              id: updatedRecord.id,
              rentPeriod: new Date(updatedRecord.rentPeriod),
              amount: updatedRecord.amount,
              receivedDate: new Date(updatedRecord.receivedDate),
              createdDate: entry.createdDate // Keep original created date
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

  private generateId(): number {
    return Math.max(0, ...this.paymentEntries.map(e => e.id)) + 1;
  }

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
} 
