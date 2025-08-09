import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';


export interface ShareModalData {
  shareUrl: string;
  expiryDate: Date;
  isLoading?: boolean;
}

@Component({
  selector: 'app-share-modal',
  imports: [CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatSnackBarModule,
    MatProgressSpinnerModule],
  templateUrl: './share-modal.component.html',
  styleUrl: './share-modal.component.scss'
})
export class ShareModalComponent {
  constructor(
    public dialogRef: MatDialogRef<ShareModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ShareModalData,
    private snackBar: MatSnackBar
  ) {}

  copyToClipboard(): void {
    navigator.clipboard.writeText(this.data.shareUrl).then(() => {
      this.snackBar.open('Link copied to clipboard!', 'Close', {
        duration: 3000,
        panelClass: ['success-snackbar'],
        horizontalPosition: 'center',
        verticalPosition: 'bottom'
      });
    }).catch(() => {
      this.snackBar.open('Failed to copy link', 'Close', {
        duration: 3000,
        panelClass: ['error-snackbar']
      });
    });
  }

  formatDate(date: Date): string {
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }
}
