import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
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
    }, { validators: [InitialEntryFormComponent.dateRangeValidator] });
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
        action: 'create',
        data: this.entryForm.value
      });
    }
  }

  onCancel() {
    this.dialogRef.close();
  }
} 
