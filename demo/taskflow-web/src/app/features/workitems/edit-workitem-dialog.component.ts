import { Component, inject, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { WorkItemApiService } from '../../core/services/workitem-api.service';
import type { ActiveWorkItem } from '../../core/contracts/dashboard.contracts';
import type { WorkItemPriority } from '../../core/contracts/project.contracts';

@Component({
  selector: 'app-edit-workitem-dialog',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSelectModule,
    MatTabsModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ],
  templateUrl: './edit-workitem-dialog.component.html',
  styleUrl: './edit-workitem-dialog.component.css'
})
export class EditWorkItemDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly workItemApi = inject(WorkItemApiService);
  private readonly dialogRef = inject(MatDialogRef<EditWorkItemDialogComponent>);
  private readonly snackBar = inject(MatSnackBar);

  priorityForm: FormGroup;
  descriptionForm: FormGroup;
  deadlineForm: FormGroup;
  assignmentForm: FormGroup;

  isSubmitting = false;
  errorMessage = '';

  constructor(@Inject(MAT_DIALOG_DATA) public workItem: ActiveWorkItem) {
    this.priorityForm = this.fb.group({
      newPriority: [workItem.priority, Validators.required],
      rationale: ['', Validators.required],
    });

    this.descriptionForm = this.fb.group({
      newDescription: ['', Validators.required],
    });

    this.deadlineForm = this.fb.group({
      deadline: [workItem.deadline ? new Date(workItem.deadline) : null],
    });

    this.assignmentForm = this.fb.group({
      memberId: [workItem.assignedTo || '', Validators.required],
    });
  }

  ngOnInit() {
    // Load current description
    this.workItemApi.getWorkItem(this.workItem.workItemId).subscribe({
      next: (workItem) => {
        this.descriptionForm.patchValue({
          newDescription: workItem.description
        });
      },
      error: (error) => console.error('Error loading work item:', error)
    });
  }

  updatePriority(): void {
    if (this.priorityForm.invalid) return;

    this.isSubmitting = true;
    this.errorMessage = '';

    this.workItemApi.reprioritize(this.workItem.workItemId, {
      newPriority: this.priorityForm.value.newPriority as WorkItemPriority,
      rationale: this.priorityForm.value.rationale
    }).subscribe({
      next: (result) => {
        this.handleSuccess('Priority updated successfully');
      },
      error: (error) => this.handleError(error)
    });
  }

  updateDescription(): void {
    if (this.descriptionForm.invalid) return;

    this.isSubmitting = true;
    this.errorMessage = '';

    this.workItemApi.refineRequirements(this.workItem.workItemId, {
      newDescription: this.descriptionForm.value.newDescription
    }).subscribe({
      next: (result) => {
        this.handleSuccess('Description updated successfully');
      },
      error: (error) => this.handleError(error)
    });
  }

  updateDeadline(): void {
    if (!this.deadlineForm.value.deadline) return;

    this.isSubmitting = true;
    this.errorMessage = '';

    const deadline = new Date(this.deadlineForm.value.deadline);
    deadline.setHours(23, 59, 59, 999);

    this.workItemApi.establishDeadline(this.workItem.workItemId, {
      deadline: deadline.toISOString()
    }).subscribe({
      next: (result) => {
        this.handleSuccess('Deadline set successfully');
      },
      error: (error) => this.handleError(error)
    });
  }

  removeDeadline(): void {
    this.isSubmitting = true;
    this.errorMessage = '';

    this.workItemApi.removeDeadline(this.workItem.workItemId).subscribe({
      next: (result) => {
        this.handleSuccess('Deadline removed successfully');
      },
      error: (error) => this.handleError(error)
    });
  }

  assignResponsibility(): void {
    if (this.assignmentForm.invalid) return;

    this.isSubmitting = true;
    this.errorMessage = '';

    this.workItemApi.assignResponsibility(this.workItem.workItemId, {
      memberId: this.assignmentForm.value.memberId
    }).subscribe({
      next: (result) => {
        this.handleSuccess('Work item assigned successfully');
      },
      error: (error) => this.handleError(error)
    });
  }

  unassignResponsibility(): void {
    this.isSubmitting = true;
    this.errorMessage = '';

    this.workItemApi.relinquishResponsibility(this.workItem.workItemId).subscribe({
      next: (result) => {
        this.handleSuccess('Work item unassigned successfully');
      },
      error: (error) => this.handleError(error)
    });
  }

  commenceWork(): void {
    this.isSubmitting = true;
    this.errorMessage = '';

    this.workItemApi.commenceWork(this.workItem.workItemId).subscribe({
      next: (result) => {
        this.handleSuccess('Work commenced successfully');
      },
      error: (error) => this.handleError(error)
    });
  }

  private handleSuccess(message: string): void {
    this.snackBar.open(message, 'Close', {
      duration: 3000,
      horizontalPosition: 'end',
      verticalPosition: 'top'
    });
    this.isSubmitting = false;
    this.dialogRef.close({ success: true });
  }

  private handleError(error: any): void {
    console.error('Error updating work item:', error);
    this.errorMessage = error.error?.message || 'An unexpected error occurred';
    this.isSubmitting = false;
  }

  close(): void {
    this.dialogRef.close();
  }
}
