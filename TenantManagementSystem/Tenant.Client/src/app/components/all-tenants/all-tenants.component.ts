import { Component, OnInit, OnDestroy, Inject, PLATFORM_ID } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { EntryService, Entry } from '../../services/entry.service';
import { isPlatformBrowser } from '@angular/common';
import { filter } from 'rxjs/operators';
import { Subscription } from 'rxjs';

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
    MatChipsModule
  ],
  templateUrl: './all-tenants.component.html',
  styleUrl: './all-tenants.component.scss'
})
export class AllTenantsComponent implements OnInit, OnDestroy {
  entries: Entry[] = [];
  displayedColumns: string[] = ['name', 'startDate', 'endDate', 'status', 'dashboard'];
  loading = true;
  error: string | null = null;
  private navSub?: Subscription;

  constructor(
    private entryService: EntryService,
    private router: Router,
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
    this.entryService.getEntries().subscribe({
      next: (list) => {
        this.loadAttempt = 0;
        this.entries = list;
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
}
