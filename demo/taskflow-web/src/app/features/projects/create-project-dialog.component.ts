import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ProjectApiService } from '../../core/services/project-api.service';
import type { InitiateProjectRequest } from '../../core/contracts/project.contracts';

@Component({
  selector: 'app-create-project-dialog',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './create-project-dialog.component.html',
  styleUrl: './create-project-dialog.component.css'
})
export class CreateProjectDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly projectApi = inject(ProjectApiService);
  private readonly dialogRef = inject(MatDialogRef<CreateProjectDialogComponent>);

  projectForm: FormGroup;
  isSubmitting = false;
  errorMessage = '';

  constructor() {
    this.projectForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
      description: ['', Validators.required],
      ownerId: [crypto.randomUUID(), Validators.required], // Generate a valid GUID
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  submit(): void {
    if (this.projectForm.invalid) {
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = '';

    const request: InitiateProjectRequest = {
      name: this.projectForm.value.name,
      description: this.projectForm.value.description,
      ownerId: this.projectForm.value.ownerId,
    };

    this.projectApi.initiateProject(request).subscribe({
      next: (result) => {
        if (result.success) {
          this.dialogRef.close(result);
        } else {
          this.errorMessage = result.message || 'Failed to create project';
          this.isSubmitting = false;
        }
      },
      error: (error) => {
        console.error('Error creating project:', error);
        this.errorMessage = error.error?.message || 'An unexpected error occurred';
        this.isSubmitting = false;
      }
    });
  }
}
