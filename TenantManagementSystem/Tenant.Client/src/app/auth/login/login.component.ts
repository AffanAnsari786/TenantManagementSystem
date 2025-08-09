import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-login',
  imports: [CommonModule, FormsModule, MatCardModule, MatInputModule, MatButtonModule, MatIconModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
username: string = '';
  password: string = '';

  constructor(private router: Router) {}

  onLogin() {
    // For now, hardcode admin check
    if (this.username === '' && this.password === '') {
      // Store token or role in localStorage/session
      localStorage.setItem('token', 'admin-token');
      this.router.navigate(['/home']);
    } else {
      alert('Invalid credentials');
    }
  }
}
