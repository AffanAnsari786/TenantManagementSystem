import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-initial-entry-form',
  imports: [CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule],
  templateUrl: './initial-entry-form.component.html',
  styleUrl: './initial-entry-form.component.scss'
})
export class InitialEntryFormComponent {
  entryForm: FormGroup;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<InitialEntryFormComponent>
  ) {
    this.entryForm = this.fb.group({
      name: ['', Validators.required],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      createdDate: [new Date()]
    });
  }

  onSubmit() {
    if (this.entryForm.valid) {
      this.dialogRef.close({ 
        action: 'create',
        data: this.entryForm.value
      });
    }
  }

  onCancel() {
    this.dialogRef.close();
  }
} 
