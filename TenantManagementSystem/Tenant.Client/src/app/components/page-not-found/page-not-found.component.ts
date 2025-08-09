import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';

@Component({
  selector: 'app-page-not-found',
  imports: [CommonModule, MatButtonModule, MatIconModule],
  templateUrl: './page-not-found.component.html',
  styleUrl: './page-not-found.component.scss'
})
export class PageNotFoundComponent {
  constructor(private router: Router) {}

  goHome(): void {
    this.router.navigate(['/login']);
  }

  goBack(): void {
    window.history.back();
  }
}
