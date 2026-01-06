import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { forkJoin, Subscription } from 'rxjs';
import { AdminApiService, StorageProviderStatus } from '../../core/services/admin-api.service';
import { EpicApiService } from '../../core/services/epic-api.service';
import { SignalRService, ProjectionBuildProgressEvent, SeedProgressEvent } from '../../core/services/signalr.service';
import type { SeedDataResult } from '../../core/contracts/admin.contracts';

export interface EpicSeedResult {
  success: boolean;
  message: string;
  epicIds: string[];
  note: string;
}

export interface SprintSeedResult {
  success: boolean;
  message: string;
  sprintIds: string[];
  note: string;
}

export interface CombinedSeedResult extends SeedDataResult {
  epicsCreated?: number;
  epicIds?: string[];
  sprintsCreated?: number;
  sprintIds?: string[];
}

export type SeedingStatus = 'idle' | 'seeding' | 'completed' | 'error';

export interface SeedingProgress {
  blob: SeedingStatus;
  table: SeedingStatus;
  cosmos: SeedingStatus;
}

export interface SeedingPercentages {
  blob: number;
  table: number;
  cosmos: number;
}

@Component({
  selector: 'app-demo-data',
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatProgressBarModule,
    MatDividerModule,
    MatSnackBarModule
  ],
  templateUrl: './demo-data.component.html',
  styleUrl: './demo-data.component.css'
})
export class DemoDataComponent implements OnInit, OnDestroy {
  private readonly adminApi = inject(AdminApiService);
  private readonly epicApi = inject(EpicApiService);
  private readonly signalrService = inject(SignalRService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  private progressSubscription?: Subscription;
  private seedProgressSubscription?: Subscription;

  readonly isSeeding = signal(false);
  readonly isSeedingBlob = signal(false);
  readonly isSeedingTable = signal(false);
  readonly isSeedingCosmosDb = signal(false);
  readonly seedResult = signal<CombinedSeedResult | null>(null);
  readonly seedingProgress = signal<SeedingProgress>({ blob: 'idle', table: 'idle', cosmos: 'idle' });
  readonly seedingPercentages = signal<SeedingPercentages>({ blob: 0, table: 0, cosmos: 0 });
  readonly blobSeedResult = signal<SeedDataResult | null>(null);
  readonly tableSeedResult = signal<EpicSeedResult | null>(null);
  readonly cosmosDbSeedResult = signal<SprintSeedResult | null>(null);
  readonly isBuildingProjections = signal(false);
  readonly buildProgress = signal<ProjectionBuildProgressEvent[]>([]);
  readonly buildResult = signal<{ success: boolean; message: string } | null>(null);

  // Provider availability status
  readonly providerStatus = signal<StorageProviderStatus | null>(null);
  readonly isCosmosDbAvailable = signal(true); // Assume available until we know otherwise

  ngOnInit() {
    // Fetch provider status on component init
    this.adminApi.getStorageProviderStatus().subscribe({
      next: (status) => {
        this.providerStatus.set(status);
        this.isCosmosDbAvailable.set(status.providers.cosmos.enabled);
      },
      error: (error) => {
        console.error('Failed to fetch provider status:', error);
        // On error, assume all providers are available
      }
    });
  }

  ngOnDestroy() {
    this.progressSubscription?.unsubscribe();
    this.seedProgressSubscription?.unsubscribe();
  }

  seedDemoData() {
    this.isSeeding.set(true);
    this.seedResult.set(null);

    // Seed regular demo data, epics, and sprints in parallel
    forkJoin({
      demoData: this.adminApi.seedDemoData(),
      epics: this.epicApi.seedDemoEpics(),
      sprints: this.adminApi.seedDemoSprints()
    }).subscribe({
      next: ({ demoData, epics, sprints }) => {
        this.isSeeding.set(false);
        // Combine results
        const combinedResult: CombinedSeedResult = {
          ...demoData,
          epicsCreated: epics.epicIds?.length || 0,
          epicIds: epics.epicIds || [],
          sprintsCreated: sprints.sprintIds?.length || 0,
          sprintIds: sprints.sprintIds || []
        };
        this.seedResult.set(combinedResult);
        this.snackBar.open('Demo data generated successfully (including Epics in Table Storage and Sprints in CosmosDB)!', 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'success-snackbar'
        });
      },
      error: (error) => {
        this.isSeeding.set(false);
        console.error('Error seeding data:', error);
        this.snackBar.open('Failed to generate demo data. See console for details.', 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'error-snackbar'
        });
      }
    });
  }

  /**
   * Seeds all demo data and then triggers projection building without waiting for projections to complete.
   * This is the recommended way to initialize all demo data at once.
   */
  seedAllAndTriggerProjections() {
    const cosmosAvailable = this.isCosmosDbAvailable();

    this.isSeeding.set(true);
    this.seedResult.set(null);
    this.buildProgress.set([]);
    this.buildResult.set(null);
    this.seedingProgress.set({
      blob: 'seeding',
      table: 'seeding',
      cosmos: cosmosAvailable ? 'seeding' : 'idle'
    });
    this.seedingPercentages.set({ blob: 0, table: 0, cosmos: 0 });

    // Subscribe to seed progress updates from SignalR
    this.seedProgressSubscription?.unsubscribe();
    this.seedProgressSubscription = this.signalrService.onSeedProgress.subscribe({
      next: (progress: SeedProgressEvent) => {
        console.log('[SeedProgress] Received:', progress.provider, progress.percentage);
        this.seedingPercentages.update(p => {
          const updated = {
            ...p,
            [progress.provider]: progress.percentage
          };
          console.log('[SeedProgress] Updated percentages:', updated);
          return updated;
        });
      }
    });

    // Track partial results as they come in
    let demoDataResult: SeedDataResult | null = null;
    let epicsResult: EpicSeedResult | null = null;
    let sprintsResult: SprintSeedResult | null = cosmosAvailable ? null : { success: true, message: 'Skipped', sprintIds: [], note: 'CosmosDB not available' };

    const checkAllComplete = () => {
      if (demoDataResult && epicsResult && sprintsResult) {
        this.isSeeding.set(false);
        const combinedResult: CombinedSeedResult = {
          ...demoDataResult,
          epicsCreated: epicsResult.epicIds?.length || 0,
          epicIds: epicsResult.epicIds || [],
          sprintsCreated: sprintsResult.sprintIds?.length || 0,
          sprintIds: sprintsResult.sprintIds || []
        };
        this.seedResult.set(combinedResult);
        this.triggerProjectionsInBackground();
        this.snackBar.open('Demo data generated! Projections are building in the background...', 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'success-snackbar'
        });
      }
    };

    // Subscribe to each provider separately to track individual progress
    this.adminApi.seedDemoData().subscribe({
      next: (result) => {
        demoDataResult = result;
        this.seedingProgress.update(p => ({ ...p, blob: 'completed' }));
        checkAllComplete();
      },
      error: (error) => {
        console.error('Error seeding Blob data:', error);
        this.seedingProgress.update(p => ({ ...p, blob: 'error' }));
      }
    });

    this.epicApi.seedDemoEpics().subscribe({
      next: (result) => {
        epicsResult = result;
        this.seedingProgress.update(p => ({ ...p, table: 'completed' }));
        checkAllComplete();
      },
      error: (error) => {
        console.error('Error seeding Table data:', error);
        this.seedingProgress.update(p => ({ ...p, table: 'error' }));
      }
    });

    // Only seed sprints if CosmosDB is available
    if (cosmosAvailable) {
      this.adminApi.seedDemoSprints().subscribe({
        next: (result) => {
          sprintsResult = result;
          this.seedingProgress.update(p => ({ ...p, cosmos: 'completed' }));
          checkAllComplete();
        },
        error: (error) => {
          console.error('Error seeding CosmosDB data:', error);
          this.seedingProgress.update(p => ({ ...p, cosmos: 'error' }));
        }
      });
    } else {
      // CosmosDB not available, mark as complete immediately
      checkAllComplete();
    }
  }

  /**
   * Triggers projection building in the background without blocking the UI.
   * Subscribes to SignalR for progress updates but doesn't wait for completion.
   */
  private triggerProjectionsInBackground() {
    this.isBuildingProjections.set(true);

    // Subscribe to progress updates from SignalR
    this.progressSubscription?.unsubscribe();
    this.progressSubscription = this.signalrService.onProjectionBuildProgress.subscribe({
      next: (progress) => {
        // Update or add progress entry
        const currentProgress = this.buildProgress();
        const existingIndex = currentProgress.findIndex(p => p.projectionName === progress.projectionName);
        if (existingIndex >= 0) {
          const updated = [...currentProgress];
          updated[existingIndex] = progress;
          this.buildProgress.set(updated);
        } else {
          this.buildProgress.set([...currentProgress, progress]);
        }

        // Check if all projections are done
        if (progress.projectionName === 'All' && progress.status === 'completed') {
          this.isBuildingProjections.set(false);
          this.snackBar.open('All projections built successfully!', 'Close', {
            duration: 3000,
            horizontalPosition: 'end',
            verticalPosition: 'top',
            panelClass: 'success-snackbar'
          });
        }
      }
    });

    // Fire and forget - trigger projection build but don't wait
    this.adminApi.buildAllProjections().subscribe({
      next: (result) => {
        this.isBuildingProjections.set(false);
        this.buildResult.set(result);
      },
      error: (error) => {
        this.isBuildingProjections.set(false);
        console.error('Error building projections:', error);
        this.snackBar.open('Projections failed to build. See console for details.', 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'error-snackbar'
        });
      }
    });
  }

  viewProjects() {
    this.router.navigate(['/projects']);
  }

  viewWorkItems() {
    this.router.navigate(['/workitems']);
  }

  viewEpics() {
    this.router.navigate(['/epics']);
  }

  viewSprints() {
    this.router.navigate(['/sprints']);
  }

  viewProjections() {
    this.router.navigate(['/projections']);
  }

  // Individual provider seed methods
  seedBlobOnly() {
    this.isSeedingBlob.set(true);
    this.blobSeedResult.set(null);

    this.adminApi.seedDemoData().subscribe({
      next: (result) => {
        this.isSeedingBlob.set(false);
        this.blobSeedResult.set(result);
        this.snackBar.open(`Blob Storage seeded: ${result.projectsCreated} projects, ${result.workItemsCreated} work items`, 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'success-snackbar'
        });
      },
      error: (error) => {
        this.isSeedingBlob.set(false);
        console.error('Error seeding Blob data:', error);
        this.snackBar.open('Failed to seed Blob Storage. See console for details.', 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'error-snackbar'
        });
      }
    });
  }

  seedTableOnly() {
    this.isSeedingTable.set(true);
    this.tableSeedResult.set(null);

    this.epicApi.seedDemoEpics().subscribe({
      next: (result) => {
        this.isSeedingTable.set(false);
        this.tableSeedResult.set(result);
        this.snackBar.open(`Table Storage seeded: ${result.epicIds?.length || 0} epics created`, 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'success-snackbar'
        });
      },
      error: (error) => {
        this.isSeedingTable.set(false);
        console.error('Error seeding Table Storage data:', error);
        this.snackBar.open('Failed to seed Table Storage. See console for details.', 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'error-snackbar'
        });
      }
    });
  }

  seedCosmosDbOnly() {
    this.isSeedingCosmosDb.set(true);
    this.cosmosDbSeedResult.set(null);

    this.adminApi.seedDemoSprints().subscribe({
      next: (result) => {
        this.isSeedingCosmosDb.set(false);
        this.cosmosDbSeedResult.set(result);
        this.snackBar.open(`CosmosDB seeded: ${result.sprintIds?.length || 0} sprints created`, 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'success-snackbar'
        });
      },
      error: (error) => {
        this.isSeedingCosmosDb.set(false);
        console.error('Error seeding CosmosDB data:', error);
        this.snackBar.open('Failed to seed CosmosDB. See console for details.', 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'error-snackbar'
        });
      }
    });
  }

  isAnySeedingInProgress(): boolean {
    return this.isSeeding() || this.isSeedingBlob() || this.isSeedingTable() || this.isSeedingCosmosDb() || this.isBuildingProjections();
  }

  buildProjections() {
    this.isBuildingProjections.set(true);
    this.buildProgress.set([]);
    this.buildResult.set(null);

    // Subscribe to progress updates from SignalR
    this.progressSubscription?.unsubscribe();
    this.progressSubscription = this.signalrService.onProjectionBuildProgress.subscribe({
      next: (progress) => {
        // Update or add progress entry
        const currentProgress = this.buildProgress();
        const existingIndex = currentProgress.findIndex(p => p.projectionName === progress.projectionName);
        if (existingIndex >= 0) {
          const updated = [...currentProgress];
          updated[existingIndex] = progress;
          this.buildProgress.set(updated);
        } else {
          this.buildProgress.set([...currentProgress, progress]);
        }

        // Check if all projections are done
        if (progress.projectionName === 'All' && progress.status === 'completed') {
          this.isBuildingProjections.set(false);
        }
      }
    });

    this.adminApi.buildAllProjections().subscribe({
      next: (result) => {
        this.isBuildingProjections.set(false);
        this.buildResult.set(result);
        this.snackBar.open('All projections built successfully!', 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'success-snackbar'
        });
      },
      error: (error) => {
        this.isBuildingProjections.set(false);
        console.error('Error building projections:', error);
        this.snackBar.open('Failed to build projections. See console for details.', 'Close', {
          duration: 5000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: 'error-snackbar'
        });
      }
    });
  }
}
