import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { interval, Subscription } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { SignalRService } from '../../core/services/signalr.service';
import { AdminApiService } from '../../core/services/admin-api.service';
import { ProjectionViewerDialogComponent } from './projection-viewer-dialog.component';
import { ProjectionStatus } from '../../core/contracts/admin.contracts';

@Component({
  selector: 'app-projections',
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatTableModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
    MatDialogModule
  ],
  templateUrl: './projections.component.html',
  styleUrl: './projections.component.css'
})
export class ProjectionsComponent implements OnInit, OnDestroy {
  private readonly signalrService = inject(SignalRService);
  private readonly adminApi = inject(AdminApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  readonly projections = signal<any[]>([]);
  readonly isLoading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly projectionColumns = ['name', 'storageType', 'projectionStatus', 'status', 'schemaVersion', 'lastUpdate', 'checkpoint', 'actions'];

  // Store raw projection data with timestamps
  private rawProjections: ProjectionStatus[] = [];
  private updateTimerSubscription?: Subscription;

  ngOnInit() {
    this.loadProjections();
    this.subscribeToProjectionUpdates();
    this.startUpdateTimer();
  }

  ngOnDestroy() {
    this.updateTimerSubscription?.unsubscribe();
  }

  private startUpdateTimer() {
    // Update the "last updated" time every 5 seconds
    this.updateTimerSubscription = interval(5000).subscribe(() => {
      this.updateProjectionTimes();
    });
  }

  private updateProjectionTimes() {
    if (this.rawProjections.length > 0) {
      const formatted = this.rawProjections.map(p => ({
        name: p.name,
        storageType: p.storageType || 'Blob',
        status: p.status,
        projectionStatus: p.projectionStatus || 'Active',
        schemaVersion: p.schemaVersion ?? 1,
        codeSchemaVersion: p.codeSchemaVersion ?? 1,
        needsSchemaUpgrade: p.needsSchemaUpgrade ?? false,
        lastUpdate: p.lastUpdate ? this.formatLastUpdate(p.lastUpdate) : 'Never persisted',
        lastGenerationDuration: this.formatDuration(p.lastGenerationDurationMs ?? null),
        checkpoint: p.checkpointFingerprint ? p.checkpointFingerprint.substring(0, 12) + '...' : p.checkpoint.toString(),
        isPersisted: !!p.lastUpdate
      }));
      this.projections.set(formatted);
    }
  }

  private formatDuration(ms: number | null): string {
    if (ms === null || ms === undefined) return '-';
    if (ms < 1000) return `${ms}ms`;
    const seconds = (ms / 1000).toFixed(2);
    return `${seconds}s`;
  }

  private subscribeToProjectionUpdates() {
    // Listen for projection update events from SignalR
    this.signalrService.onProjectionUpdated.subscribe({
      next: (event) => {
        console.log('Projection updated event received:', event);
        console.log('Projection statuses:', event.projections.map(p => `${p.name}: ${p.status}`));

        // Update projection data from SignalR event
        this.rawProjections = event.projections as ProjectionStatus[];

        console.log('Updated rawProjections:', this.rawProjections);

        // Update the displayed projections immediately
        this.updateProjectionTimes();
      },
      error: (error) => {
        console.error('Error receiving projection update:', error);
      }
    });
  }

  loadProjections() {
    this.isLoading.set(true);
    this.loadError.set(null);

    this.adminApi.getProjectionStatus().subscribe({
      next: (projections) => {
        console.log('Loaded projections from API:', projections);

        // Store raw data
        this.rawProjections = projections;

        // Update the displayed projections
        this.updateProjectionTimes();
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error('Error loading projections:', error);
        this.loadError.set(error.message || 'Failed to load projections');
        this.isLoading.set(false);
      }
    });
  }

  private formatLastUpdate(dateString: string): string {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffSecs = Math.floor(diffMs / 1000);

    if (diffSecs < 60) return `${diffSecs} seconds ago`;
    const diffMins = Math.floor(diffSecs / 60);
    if (diffMins < 60) return `${diffMins} minutes ago`;
    const diffHours = Math.floor(diffMins / 60);
    return `${diffHours} hours ago`;
  }

  viewProjection(name: string) {
    this.dialog.open(ProjectionViewerDialogComponent, {
      width: '90vw',
      maxWidth: '1400px',
      height: '85vh',
      maxHeight: '900px',
      panelClass: 'projection-viewer-dialog',
      data: {
        projectionName: name
      }
    });
  }

  getProjectionStatusTooltip(status: string): string {
    switch (status.toLowerCase()) {
      case 'active':
        return 'Projection is active and processing events normally';
      case 'rebuilding':
        return 'Projection is being rebuilt - inline updates are skipped';
      case 'disabled':
        return 'Projection is disabled - all updates are skipped';
      default:
        return 'Unknown status';
    }
  }

  rebuildProjection(name: string) {
    console.log('Rebuilding projection:', name);

    this.adminApi.rebuildProjection(name).subscribe({
      next: (result) => {
        this.snackBar.open(`Projection '${name}' rebuilt successfully!`, 'Close', {
          duration: 3000,
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
        // Reload projections to get updated status
        this.loadProjections();
      },
      error: (error) => {
        console.error('Error rebuilding projection:', error);
        this.snackBar.open(`Failed to rebuild projection '${name}'`, 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }
}
