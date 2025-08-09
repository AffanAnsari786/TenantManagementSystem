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
    // TODO: Implement entry creation logic
    alert(`Entry Created!\nName: ${this.entryName}\nStart: ${this.startDate}\nEnd: ${this.endDate}`);
    this.closeModal();
  }
}
