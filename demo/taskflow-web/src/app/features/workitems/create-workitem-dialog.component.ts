import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WorkItemApiService } from '../../core/services/workitem-api.service';
import { DashboardApiService } from '../../core/services/dashboard-api.service';
import type { PlanWorkItemRequest } from '../../core/contracts/workitem.contracts';
import type { WorkItemPriority } from '../../core/contracts/project.contracts';
import type { ProjectSummary } from '../../core/contracts/dashboard.contracts';

@Component({
  selector: 'app-create-workitem-dialog',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSelectModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './create-workitem-dialog.component.html',
  styleUrl: './create-workitem-dialog.component.css'
})
export class CreateWorkItemDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly workItemApi = inject(WorkItemApiService);
  private readonly dashboardApi = inject(DashboardApiService);
  private readonly dialogRef = inject(MatDialogRef<CreateWorkItemDialogComponent>);

  workItemForm: FormGroup;
  isSubmitting = false;
  errorMessage = '';
  projects: ProjectSummary[] = [];

  constructor() {
    this.workItemForm = this.fb.group({
      projectId: ['', Validators.required],
      title: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(200)]],
      description: ['', Validators.required],
      priority: ['Medium', Validators.required],
    });

    // Load projects
    this.dashboardApi.getAllProjects().subscribe({
      next: (projects) => {
        this.projects = projects.filter(p => !p.isCompleted);
      },
      error: (error) => console.error('Error loading projects:', error)
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  submit(): void {
    if (this.workItemForm.invalid) {
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = '';

    const request: PlanWorkItemRequest = {
      projectId: this.workItemForm.value.projectId,
      title: this.workItemForm.value.title,
      description: this.workItemForm.value.description,
      priority: this.workItemForm.value.priority as WorkItemPriority,
    };

    this.workItemApi.planWorkItem(request).subscribe({
      next: (result) => {
        if (result.success) {
          this.dialogRef.close(result);
        } else {
          this.errorMessage = result.message || 'Failed to plan work item';
          this.isSubmitting = false;
        }
      },
      error: (error) => {
        console.error('Error planning work item:', error);
        this.errorMessage = error.error?.message || 'An unexpected error occurred';
        this.isSubmitting = false;
      }
    });
  }
}
