import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { WorkItemApiService } from '../../core/services/workitem-api.service';
import { EditWorkItemDialogComponent } from './edit-workitem-dialog.component';
import { CompleteWorkItemDialogComponent } from './complete-workitem-dialog.component';
import type { WorkItemDto } from '../../core/contracts/workitem.contracts';

@Component({
  selector: 'app-workitem-detail',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatDividerModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './workitem-detail.component.html',
  styleUrl: './workitem-detail.component.css'
})
export class WorkItemDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly workItemApi = inject(WorkItemApiService);
  private readonly dialog = inject(MatDialog);

  readonly workItem = signal<WorkItemDto | null>(null);
  readonly loading = signal(true);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadWorkItem(id);
    }
  }

  private loadWorkItem(id: string) {
    this.loading.set(true);
    this.workItemApi.getWorkItem(id).subscribe({
      next: (workItem) => {
        this.workItem.set(workItem);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load work item:', err);
        this.loading.set(false);
      }
    });
  }

  editWorkItem() {
    const workItem = this.workItem();
    if (!workItem) return;

    const dialogRef = this.dialog.open(EditWorkItemDialogComponent, {
      width: '700px',
      data: {
        workItemId: workItem.workItemId,
        projectId: workItem.projectId,
        title: workItem.title,
        priority: workItem.priority,
        status: workItem.status,
        assignedTo: workItem.assignedTo,
        deadline: workItem.deadline
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.success) {
        this.loadWorkItem(workItem.workItemId);
      }
    });
  }

  completeWorkItem() {
    const workItem = this.workItem();
    if (!workItem) return;

    const dialogRef = this.dialog.open(CompleteWorkItemDialogComponent, {
      width: '600px',
      data: {
        workItemId: workItem.workItemId,
        title: workItem.title
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.success) {
        this.loadWorkItem(workItem.workItemId);
      }
    });
  }

  goBack() {
    this.router.navigate(['/workitems']);
  }

  getStatusDisplay(status: string): string {
    if (status === 'InProgress') return 'In Progress';
    return status;
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleDateString();
  }

  isOverdue(dateString: string): boolean {
    const deadline = new Date(dateString);
    const now = new Date();
    return deadline < now;
  }
}
