import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { ProjectApiService } from '../../core/services/project-api.service';
import { SignalRService } from '../../core/services/signalr.service';
import { CompleteProjectDialogComponent } from './complete-project-dialog.component';
import type { ProjectDto } from '../../core/contracts/project.contracts';

@Component({
  selector: 'app-project-detail',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatListModule,
    MatDividerModule
  ],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.css'
})
export class ProjectDetailComponent implements OnInit {
  private readonly projectApi = inject(ProjectApiService);
  private readonly signalrService = inject(SignalRService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  readonly project = signal<ProjectDto | null>(null);

  ngOnInit() {
    const projectId = this.route.snapshot.paramMap.get('id');
    if (projectId) {
      this.loadProject(projectId);
      this.subscribeToUpdates(projectId);
    }
  }

  private loadProject(projectId: string) {
    this.projectApi.getProject(projectId).subscribe({
      next: (project) => this.project.set(project),
      error: (err) => console.error('Failed to load project:', err)
    });
  }

  private subscribeToUpdates(projectId: string) {
    this.signalrService.onProjectRebranded.subscribe((event: any) => {
      if (event.projectId === projectId) {
        this.loadProject(projectId);
      }
    });

    this.signalrService.onTeamMemberAdded.subscribe((event: any) => {
      if (event.projectId === projectId) {
        this.loadProject(projectId);
      }
    });
  }

  goBack() {
    this.router.navigate(['/projects']);
  }

  getTeamMembers() {
    const proj = this.project();
    if (!proj) return [];

    return Object.entries(proj.teamMembers).map(([id, role]) => ({
      id,
      name: id,
      role: role as string
    }));
  }

  completeProject() {
    const proj = this.project();
    if (!proj) return;

    const dialogRef = this.dialog.open(CompleteProjectDialogComponent, {
      width: '600px',
      disableClose: false
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.outcome) {
        this.projectApi.completeProject(proj.projectId, { outcome: result.outcome }).subscribe({
          next: () => {
            this.snackBar.open('Project completed successfully!', 'Close', {
              duration: 3000,
              horizontalPosition: 'end',
              verticalPosition: 'top'
            });
            this.loadProject(proj.projectId);
          },
          error: (err) => {
            this.snackBar.open(`Failed to complete project: ${err.error?.message || err.message}`, 'Close', {
              duration: 5000,
              horizontalPosition: 'end',
              verticalPosition: 'top'
            });
          }
        });
      }
    });
  }
}
