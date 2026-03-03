import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-admin',
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent {
  showModal = false;
  entryName = '';
  startDate: string | null = null;
  endDate: string | null = null;

  getDateValidationError(): string | null {
    if (!this.startDate || !this.endDate) return null;

    const start = new Date(this.startDate);
    const end = new Date(this.endDate);
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return null;

    if (start.getTime() >= end.getTime()) {
      return 'Start date must be earlier than end date';
    }

    const diffDays = (end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24);
    if (diffDays < 30) {
      return 'Rent period must be at least 30 days (1 month)';
    }

    return null;
  }

  isDateRangeValid(): boolean {
    return this.getDateValidationError() === null;
  }

  openModal() {
    this.showModal = true;
  }

  closeModal() {
    this.showModal = false;
    this.entryName = '';
    this.startDate = null;
    this.endDate = null;
  }

  addEntry() {
    const error = this.getDateValidationError();
    if (error) {
      alert(error);
      return;
    }

    // TODO: Implement entry creation logic
    alert(`Entry Created!\nName: ${this.entryName}\nStart: ${this.startDate}\nEnd: ${this.endDate}`);
    this.closeModal();
  }
}
