import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';

@Component({
  selector: 'app-complete-project-dialog',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule
  ],
  templateUrl: './complete-project-dialog.component.html',
  styleUrl: './complete-project-dialog.component.css'
})
export class CompleteProjectDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<CompleteProjectDialogComponent>);
  private readonly fb = inject(FormBuilder);

  readonly form: FormGroup = this.fb.group({
    outcome: ['', Validators.required],
    customOutcome: ['']
  });

  constructor() {
    // Watch for custom outcome selection
    this.form.get('outcome')?.valueChanges.subscribe(value => {
      const customControl = this.form.get('customOutcome');
      if (value === 'Custom...') {
        customControl?.setValidators([Validators.required]);
      } else {
        customControl?.clearValidators();
      }
      customControl?.updateValueAndValidity();
    });
  }

  showCustomOutcome(): boolean {
    return this.form.get('outcome')?.value === 'Custom...';
  }

  onSubmit() {
    if (this.form.valid) {
      const outcome = this.form.value.outcome === 'Custom...'
        ? this.form.value.customOutcome
        : this.form.value.outcome;

      this.dialogRef.close({ outcome });
    }
  }
}
