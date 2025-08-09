import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

export interface EntryDialogData {
  entry?: any;
  mode: 'create' | 'edit' | 'preview';
}
@Component({
  selector: 'app-entry-form',
  imports: [CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule],
  templateUrl: './entry-form.component.html',
  styleUrl: './entry-form.component.scss'
})
export class EntryFormComponent {
  entryForm: FormGroup;
  isEditMode: boolean;
  isPreviewMode: boolean;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<EntryFormComponent>,
    @Inject(MAT_DIALOG_DATA) private data: EntryDialogData
  ) {
    this.isEditMode = data?.mode === 'edit';
    this.isPreviewMode = data?.mode === 'preview';

    this.entryForm = this.fb.group({
      rentPeriod: ['', Validators.required],
      amount: ['', [Validators.required, Validators.min(0)]],
      receivedDate: ['', Validators.required],
      createdDate: [new Date()]
    });

    if (data?.entry) {
      const entry = {
        ...data.entry,
        rentPeriod: this.formatDate(new Date(data.entry.rentPeriod)),
        receivedDate: data.entry.receivedDate ? this.formatDate(new Date(data.entry.receivedDate)) : null
      };
      this.entryForm.patchValue(entry);
    }

    if (this.isPreviewMode) {
      this.entryForm.disable();
    }
  }

  getFormTitle(): string {
    if (this.isPreviewMode) return 'View Payment Entry';
    return this.isEditMode ? 'Edit Payment Entry' : 'Add Payment Entry';
  }

  onSubmit() {
    if (this.entryForm.valid) {
      const formValue = this.entryForm.value;
      const result = {
        ...formValue,
        id: this.data?.entry?.id
      };
      this.dialogRef.close({ 
        action: this.isEditMode ? 'update' : 'create',
        data: result 
      });
    }
  }

  onCancel() {
    this.dialogRef.close();
  }

  private formatDate(date: Date): string {
    return date.toISOString().split('T')[0];
  }
} 
