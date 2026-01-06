import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FormsModule } from '@angular/forms';
import { DashboardApiService } from '../../core/services/dashboard-api.service';
import { SignalRService } from '../../core/services/signalr.service';
import type { ActiveWorkItem } from '../../core/contracts/dashboard.contracts';
import { CreateWorkItemDialogComponent } from './create-workitem-dialog.component';
import { EditWorkItemDialogComponent } from './edit-workitem-dialog.component';
import { CompleteWorkItemDialogComponent } from './complete-workitem-dialog.component';

@Component({
  selector: 'app-workitem-list-full',
  imports: [
    CommonModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatMenuModule,
    MatDialogModule,
    MatSnackBarModule,
    FormsModule
  ],
  templateUrl: './workitem-list-full.component.html',
  styleUrl: './workitem-list-full.component.css'
})
export class WorkItemListFullComponent implements OnInit {
  private readonly dashboardApi = inject(DashboardApiService);
  private readonly signalrService = inject(SignalRService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly workItems = signal<ActiveWorkItem[]>([]);
  readonly filteredWorkItems = signal<ActiveWorkItem[]>([]);
  readonly displayedColumns = ['title', 'priority', 'status', 'assignedTo', 'deadline', 'actions'];

  statusFilter = '';
  priorityFilter = '';
  searchText = '';

  ngOnInit() {
    this.loadWorkItems();
    this.subscribeToUpdates();
  }

  private loadWorkItems() {
    this.dashboardApi.getActiveWorkItems().subscribe({
      next: (items: ActiveWorkItem[]) => {
        this.workItems.set(items);
        this.applyFilters();
      },
      error: (err: Error) => console.error('Failed to load work items:', err)
    });
  }

  applyFilters() {
    let filtered = this.workItems();

    if (this.statusFilter) {
      filtered = filtered.filter(item => item.status === this.statusFilter);
    }

    if (this.priorityFilter) {
      filtered = filtered.filter(item => item.priority === this.priorityFilter);
    }

    if (this.searchText) {
      const search = this.searchText.toLowerCase();
      filtered = filtered.filter(item =>
        item.title.toLowerCase().includes(search)
      );
    }

    this.filteredWorkItems.set(filtered);
  }

  clearFilters() {
    this.statusFilter = '';
    this.priorityFilter = '';
    this.searchText = '';
    this.applyFilters();
  }

  private subscribeToUpdates() {
    this.signalrService.onWorkItemPlanned.subscribe(() => {
      this.loadWorkItems();
    });

    this.signalrService.onWorkItemChanged.subscribe(() => {
      this.loadWorkItems();
    });

    this.signalrService.onWorkCompleted.subscribe(() => {
      this.loadWorkItems();
    });

    // Reload when projections are completed (idle state)
    this.signalrService.onProjectionUpdated.subscribe((event) => {
      if (event.projections.some(p => p.status === 'idle')) {
        this.loadWorkItems();
      }
    });
  }

  viewDetails(workItemId: string) {
    this.router.navigate(['/workitems', workItemId]);
  }

  createWorkItem() {
    const dialogRef = this.dialog.open(CreateWorkItemDialogComponent, {
      width: '600px',
      disableClose: false
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.success) {
        this.snackBar.open('Work item planned successfully!', 'Close', {
          duration: 3000,
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
        this.loadWorkItems();
      }
    });
  }

  editWorkItem(item: ActiveWorkItem) {
    const dialogRef = this.dialog.open(EditWorkItemDialogComponent, {
      width: '700px',
      data: item
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.success) {
        this.loadWorkItems();
      }
    });
  }

  completeWorkItem(item: ActiveWorkItem) {
    const dialogRef = this.dialog.open(CompleteWorkItemDialogComponent, {
      width: '600px',
      data: {
        workItemId: item.workItemId,
        title: item.title
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.success) {
        this.snackBar.open('Work item completed successfully!', 'Close', {
          duration: 3000,
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
        this.loadWorkItems();
      }
    });
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
