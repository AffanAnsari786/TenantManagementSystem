import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { EntryService, Entry, PagedResponse } from '../../services/entry.service';
import { isPlatformBrowser } from '@angular/common';
import { filter } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { InitialEntryFormComponent } from '../initial-entry-form/initial-entry-form.component';

@Component({
  selector: 'app-all-tenants',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatPaginatorModule,
    MatDialogModule,
    MatSnackBarModule
  ],
  templateUrl: './all-tenants.component.html',
  styleUrl: './all-tenants.component.scss'
})
export class AllTenantsComponent implements OnInit, OnDestroy {
  entries: Entry[] = [];
  displayedColumns: string[] = ['name', 'propertyName', 'startDate', 'endDate', 'status', 'dashboard'];
  loading = true;
  error: string | null = null;
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  private navSub?: Subscription;

  constructor(
    private entryService: EntryService,
    private router: Router,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  ngOnInit(): void {
    // Short delay so token is available when coming directly from login (localStorage + interceptor ready)
    setTimeout(() => this.loadEntries(), 50);
    // Reload whenever user navigates to this page (e.g. back from dashboard)
    this.navSub = this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e: NavigationEnd) => {
        if (e.urlAfterRedirects?.includes('/all-tenants')) {
          setTimeout(() => this.loadEntries(), 50);
        }
      });
  }

  ngOnDestroy(): void {
    this.navSub?.unsubscribe();
  }

  private loadAttempt = 0;

  loadEntries(): void {
    if (!isPlatformBrowser(this.platformId)) {
      this.loading = false;
      return;
    }
    this.error = null;
    this.loading = true;
    const isRetry = this.loadAttempt > 0;
    this.entryService.getEntries(this.pageIndex + 1, this.pageSize).subscribe({
      next: (response) => {
        this.loadAttempt = 0;
        this.entries = response.data;
        this.totalRecords = response.totalRecords;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        const status = err?.status ?? (err as { status?: number })?.status;
        if (status === 401) {
          this.loadAttempt = 0;
          this.router.navigate(['/login']);
          return;
        }
        // Auto-retry once (helps when token wasn't ready yet after login or when returning from another page)
        if (!isRetry && this.loadAttempt === 0) {
          this.loadAttempt = 1;
          setTimeout(() => this.loadEntries(), 600);
          return;
        }
        this.loadAttempt = 0;
        this.error = 'Failed to load tenants.';
      }
    });
  }

  onRetry(): void {
    this.loadEntries();
  }

  isActive(endDate: string): boolean {
    const end = new Date(endDate);
    return !isNaN(end.getTime()) && end >= new Date();
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }

  goToDashboard(entry: Entry): void {
    this.router.navigate(['/dashboard', entry.id]);
  }

  onEditTenant(entry: Entry): void {
    const dialogRef = this.dialog.open(InitialEntryFormComponent, {
      width: '600px',
      disableClose: true,
      panelClass: 'entry-form-dialog',
      data: {
        entry,
        mode: 'edit'
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.action === 'edit' && result.data) {
        this.entryService.updateEntry(entry.id, result.data).subscribe({
          next: () => {
            this.snackBar.open('Tenant updated successfully', 'Close', { duration: 3000 });
            this.loadEntries(); // Refresh table
          },
          error: () => this.snackBar.open('Failed to update tenant', 'Close', { duration: 3000 })
        });
      }
    });
  }

  onDeleteTenant(entry: Entry): void {
    if (confirm(`Are you sure you want to delete tenant ${entry.name}? This will delete all their records permanently.`)) {
      this.entryService.deleteEntry(entry.id).subscribe({
        next: () => {
          this.snackBar.open('Tenant deleted successfully', 'Close', { duration: 3000 });
          // If we deleted the last item on the page, go back one page
          if (this.entries.length === 1 && this.pageIndex > 0) {
            this.pageIndex--;
          }
          this.loadEntries();
        },
        error: () => this.snackBar.open('Failed to delete tenant', 'Close', { duration: 3000 })
      });
    }
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadEntries();
  }
}
