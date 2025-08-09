import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { filter } from 'rxjs';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterModule, MatToolbarModule, MatButtonModule, MatIconModule, MatSidenavModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  showToolbar = true;
  sidenavOpened = false;

  constructor(private router: Router) {
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        // Hide toolbar for shared routes and 404 page
        this.showToolbar = !event.url.includes('/shared/') && !event.url.includes('/404') && event.url !== '/404';
        // Close sidebar on navigation
        this.sidenavOpened = false;
      });
  }

  toggleSidebar() {
    this.sidenavOpened = !this.sidenavOpened;
  }

  closeSidebar() {
    this.sidenavOpened = false;
  }

  onLogout() {
    this.closeSidebar(); // Close sidebar before logout
    localStorage.removeItem('token');
    window.location.href = '/login';
  }
}
