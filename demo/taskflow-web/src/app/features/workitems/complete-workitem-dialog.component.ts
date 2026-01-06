import { Component, inject, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WorkItemApiService } from '../../core/services/workitem-api.service';
import type { CompleteWorkRequest } from '../../core/contracts/workitem.contracts';

export interface CompleteWorkItemDialogData {
  workItemId: string;
  title: string;
}

@Component({
  selector: 'app-complete-workitem-dialog',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './complete-workitem-dialog.component.html',
  styleUrl: './complete-workitem-dialog.component.css'
})
export class CompleteWorkItemDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly workItemApi = inject(WorkItemApiService);
  private readonly dialogRef = inject(MatDialogRef<CompleteWorkItemDialogComponent>);

  completeForm: FormGroup;
  isSubmitting = false;
  errorMessage = '';

  constructor(@Inject(MAT_DIALOG_DATA) public data: CompleteWorkItemDialogData) {
    this.completeForm = this.fb.group({
      outcome: ['', Validators.required],
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  submit(): void {
    if (this.completeForm.invalid) {
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = '';

    const request: CompleteWorkRequest = {
      outcome: this.completeForm.value.outcome,
    };

    this.workItemApi.completeWork(this.data.workItemId, request).subscribe({
      next: (result) => {
        if (result.success) {
          this.dialogRef.close(result);
        } else {
          this.errorMessage = result.message || 'Failed to complete work item';
          this.isSubmitting = false;
        }
      },
      error: (error) => {
        console.error('Error completing work item:', error);
        this.errorMessage = error.error?.message || 'An unexpected error occurred';
        this.isSubmitting = false;
      }
    });
  }
}
