import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { Inject } from '@angular/core';

export interface InitialEntryDialogData {
  entry?: any;
  mode: 'create' | 'edit';
}

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
  isEditMode: boolean;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<InitialEntryFormComponent>,
    @Inject(MAT_DIALOG_DATA) public data: InitialEntryDialogData
  ) {
    this.isEditMode = this.data?.mode === 'edit';

    this.entryForm = this.fb.group({
      name: ['', Validators.required],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      address: ['', [Validators.maxLength(500)]],
      aadhaarNumber: ['', [Validators.pattern('^[0-9]{12}$')]],
      propertyName: ['', [Validators.maxLength(200)]],
      createdDate: [new Date()]
    }, { validators: [InitialEntryFormComponent.dateRangeValidator] });

    if (this.isEditMode && this.data?.entry) {
      const entryToEdit = this.data.entry;
      this.entryForm.patchValue({
        name: entryToEdit.name,
        startDate: this.formatDate(new Date(entryToEdit.startDate)),
        endDate: this.formatDate(new Date(entryToEdit.endDate)),
        address: entryToEdit.address !== 'No address provided' ? entryToEdit.address : '',
        aadhaarNumber: entryToEdit.aadhaarNumber !== 'Not provided' ? entryToEdit.aadhaarNumber : '',
        propertyName: entryToEdit.propertyName !== 'Unassigned Property' ? entryToEdit.propertyName : ''
      });
    }
  }

  private formatDate(date: Date): string {
    if (isNaN(date.getTime())) return '';
    return date.toISOString().split('T')[0];
  }

  static dateRangeValidator(group: AbstractControl): ValidationErrors | null {
    const startValue = group.get('startDate')?.value;
    const endValue = group.get('endDate')?.value;

    if (!startValue || !endValue) return null;

    const start = new Date(startValue);
    const end = new Date(endValue);
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return null;

    if (start.getTime() >= end.getTime()) {
      return { startDateAfterOrEqualEndDate: true };
    }

    const diffMs = end.getTime() - start.getTime();
    const diffDays = diffMs / (1000 * 60 * 60 * 24);
    if (diffDays < 30) {
      return { rentPeriodTooShort: { minDays: 30, actualDays: diffDays } };
    }

    return null;
  }

  onSubmit() {
    if (this.entryForm.valid) {
      this.dialogRef.close({ 
        action: this.isEditMode ? 'edit' : 'create',
        data: {
          ...this.entryForm.value,
          id: this.data?.entry?.id
        }
      });
    }
  }

  onCancel() {
    this.dialogRef.close();
  }
} 
