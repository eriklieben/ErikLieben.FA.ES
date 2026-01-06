import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DashboardApiService } from '../../core/services/dashboard-api.service';
import { SignalRService } from '../../core/services/signalr.service';
import type { ProjectSummary } from '../../core/contracts/dashboard.contracts';
import { CreateProjectDialogComponent } from './create-project-dialog.component';

@Component({
  selector: 'app-project-list',
  imports: [
    CommonModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatChipsModule,
    MatDialogModule,
    MatSnackBarModule
  ],
  templateUrl: './project-list.component.html',
  styleUrl: './project-list.component.css'
})
export class ProjectListComponent implements OnInit {
  private readonly dashboardApi = inject(DashboardApiService);
  private readonly signalrService = inject(SignalRService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly projects = signal<ProjectSummary[]>([]);
  readonly displayedColumns = ['name', 'owner', 'status', 'teamSize', 'actions'];

  ngOnInit() {
    this.loadProjects();
    this.subscribeToUpdates();
  }

  private loadProjects() {
    this.dashboardApi.getAllProjects().subscribe({
      next: (projects) => this.projects.set(projects),
      error: (err) => console.error('Failed to load projects:', err)
    });
  }

  private subscribeToUpdates() {
    this.signalrService.onProjectInitiated.subscribe(() => {
      this.loadProjects();
    });

    this.signalrService.onProjectCompleted.subscribe(() => {
      this.loadProjects();
    });

    // Reload when projections are completed (idle state)
    this.signalrService.onProjectionUpdated.subscribe((event) => {
      if (event.projections.some(p => p.status === 'idle')) {
        this.loadProjects();
      }
    });
  }

  viewProject(projectId: string) {
    this.router.navigate(['/projects', projectId]);
  }

  createProject() {
    const dialogRef = this.dialog.open(CreateProjectDialogComponent, {
      width: '600px',
      disableClose: false
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.success) {
        this.snackBar.open('Project created successfully!', 'Close', {
          duration: 3000,
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
        this.loadProjects();
      }
    });
  }

  getTeamSize(project: ProjectSummary): number {
    return project.teamMemberCount;
  }
}
