import { Component, signal, computed, inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatBadgeModule } from '@angular/material/badge';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { StreamMigrationApiService, AvailableStream, StreamEventsResponse } from '../../core/services/stream-migration-api.service';
import {
  SignalRService,
  LiveMigrationStartedEvent,
  LiveMigrationIterationProgressEvent,
  LiveMigrationEventCopiedEvent,
  LiveMigrationCompletedEvent,
  LiveMigrationFailedEvent
} from '../../core/services/signalr.service';
import { Subscription } from 'rxjs';

interface EventData {
  id: string;
  version: number;
  type: string;
  timestamp: Date;
  data: Record<string, unknown>;
  schemaVersion: number;
  isLiveEvent?: boolean;
  writtenTo?: ('source' | 'target')[];
}

interface TransformationStep {
  eventType: string;
  fromVersion: number;
  toVersion: number;
  changes: string[];
}

interface LiveEventLog {
  id: string;
  timestamp: Date;
  eventType: string;
  phase: MigrationPhase;
  writtenTo: string[];
  message: string;
}

type MigrationPhase = 'idle' | 'normal' | 'replicating' | 'tailing' | 'cutover' | 'complete';

@Component({
  selector: 'app-stream-migration',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatDividerModule,
    MatProgressBarModule,
    MatExpansionModule,
    MatChipsModule,
    MatTooltipModule,
    MatBadgeModule,
    MatSlideToggleModule,
    MatSelectModule,
    MatFormFieldModule
  ],
  templateUrl: './stream-migration.component.html',
  styleUrl: './stream-migration.component.css'
})
export class StreamMigrationComponent implements OnDestroy, OnInit {
  private readonly apiService = inject(StreamMigrationApiService);
  private readonly signalRService = inject(SignalRService);
  private subscriptions: Subscription[] = [];

  // Live migration ID for filtering SignalR events
  readonly liveMigrationId = signal<string | null>(null);

  // Slow mode: adds 500ms delay per event for demo visibility
  readonly slowMode = signal(true); // Enabled by default for demo visibility

  // Live migration real-time state (updated via SignalR)
  readonly liveMigrationPhase = signal<'idle' | 'catchup' | 'synced' | 'closing' | 'complete' | 'failed'>('idle');
  readonly liveMigrationIteration = signal(0);
  readonly liveMigrationSourceVersion = signal(0);
  readonly liveMigrationTargetVersion = signal(0);
  readonly liveMigrationEventsBehind = signal(0);
  readonly liveMigrationTotalEventsCopied = signal(0);
  readonly liveMigrationElapsedTime = signal('00:00:00');
  readonly liveMigrationIsSynced = signal(false);
  readonly liveMigrationEventsTransformed = signal(0);
  readonly liveMigrationError = signal<string | null>(null);
  readonly liveMigrationTargetStreamId = signal<string | null>(null);

  // Per-event progress for visual animation
  readonly lastCopiedEvent = signal<LiveMigrationEventCopiedEvent | null>(null);
  readonly recentCopiedEvents = signal<LiveMigrationEventCopiedEvent[]>([]);

  // Available streams for API mode
  readonly availableStreams = signal<AvailableStream[]>([]);
  readonly selectedStreamId = signal<string | null>(null);
  readonly loadingStreams = signal(false);
  readonly loadingSourceEvents = signal(false);

  // Computed property to get the selected stream details
  readonly selectedStream = computed(() => {
    const id = this.selectedStreamId();
    return this.availableStreams().find(s => s.id === id) ?? null;
  });

  // Transformation toggle: false = just copy events, true = apply transformations
  readonly applyTransformation = signal(false);

  readonly migrationPhase = signal<MigrationPhase>('idle');
  readonly isRunning = signal(false);
  readonly progress = signal(0);
  readonly eventsProcessed = signal(0);
  readonly liveEventCounter = signal(0);

  // Debounced target event refresh during migration
  private targetRefreshTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private readonly TARGET_REFRESH_DEBOUNCE_MS = 300;

  readonly sourceEvents = signal<EventData[]>([]);
  readonly targetEvents = signal<EventData[]>([]);
  readonly liveEventLogs = signal<LiveEventLog[]>([]);

  readonly liveEventTypes = [
    { type: 'WorkItem.CommentAdded', icon: 'comment', data: { comment: 'Great progress!', author: 'jane@example.com' } },
    { type: 'WorkItem.PriorityChanged', icon: 'priority_high', data: { priority: 'high', reason: 'Client request' } },
    { type: 'WorkItem.LabelAdded', icon: 'label', data: { label: 'urgent', addedBy: 'manager@example.com' } },
    { type: 'WorkItem.AttachmentAdded', icon: 'attach_file', data: { filename: 'specs.pdf', size: '2.4MB' } },
  ];

  readonly transformations: TransformationStep[] = [
    {
      eventType: 'WorkItem.Created',
      fromVersion: 1,
      toVersion: 2,
      changes: ['Added "priority" field with default "medium"', 'Renamed "status" to "initialStatus"']
    },
    {
      eventType: 'WorkItem.AssigneeChanged',
      fromVersion: 1,
      toVersion: 2,
      changes: ['Renamed "assignee" to "assignedTo"', 'Added "assignedAt" timestamp']
    },
    {
      eventType: 'WorkItem.StatusChanged',
      fromVersion: 1,
      toVersion: 2,
      changes: ['Added "previousStatus" field', 'Added "changedBy" field']
    },
    {
      eventType: 'WorkItem.Completed',
      fromVersion: 1,
      toVersion: 2,
      changes: ['Renamed "completedBy" to "resolvedBy"', 'Added "resolution" enum field']
    }
  ];

  readonly phases = [
    { id: 'normal', label: 'Normal', icon: 'play_arrow', description: 'All operations on source stream' },
    { id: 'replicating', label: 'Replicating', icon: 'content_copy', description: 'Copying events to target stream' },
    { id: 'tailing', label: 'Tailing', icon: 'compare_arrows', description: 'Catching up new events' },
    { id: 'cutover', label: 'Cutover', icon: 'swap_horiz', description: 'Atomic switch to target stream' },
    { id: 'complete', label: 'Complete', icon: 'check_circle', description: 'Migration finished' }
  ];

  readonly phaseIndex = computed(() => {
    const phase = this.migrationPhase();
    if (phase === 'idle') return -1;
    return this.phases.findIndex(p => p.id === phase);
  });

  readonly canAddLiveEvent = computed(() => {
    // Always allow adding live events - they go to the appropriate stream(s) based on phase
    const phase = this.migrationPhase();
    return phase !== 'idle';
  });

  readonly readSource = computed(() => {
    const phase = this.migrationPhase();
    return phase === 'idle' || phase === 'normal' || phase === 'replicating';
  });

  readonly readTarget = computed(() => {
    const phase = this.migrationPhase();
    return phase === 'tailing' || phase === 'cutover' || phase === 'complete';
  });

  ngOnInit(): void {
    // Connect to SignalR and subscribe to live migration events
    this.connectSignalR();

    // Load available streams when component initializes
    this.loadAvailableStreams();
  }

  private async connectSignalR(): Promise<void> {
    try {
      await this.signalRService.connect();

      // Subscribe to live migration events
      this.subscriptions.push(
        this.signalRService.onLiveMigrationStarted.subscribe(event => {
          this.handleLiveMigrationStarted(event);
        })
      );

      this.subscriptions.push(
        this.signalRService.onLiveMigrationIterationProgress.subscribe(event => {
          this.handleLiveMigrationIterationProgress(event);
        })
      );

      this.subscriptions.push(
        this.signalRService.onLiveMigrationEventCopied.subscribe(event => {
          this.handleLiveMigrationEventCopied(event);
        })
      );

      this.subscriptions.push(
        this.signalRService.onLiveMigrationCompleted.subscribe(event => {
          this.handleLiveMigrationCompleted(event);
        })
      );

      this.subscriptions.push(
        this.signalRService.onLiveMigrationFailed.subscribe(event => {
          this.handleLiveMigrationFailed(event);
        })
      );
    } catch (error) {
      console.error('Failed to connect to SignalR:', error);
    }
  }

  private handleLiveMigrationStarted(event: LiveMigrationStartedEvent): void {
    // Accept events if we're running and either ID matches or we don't have an ID yet
    const currentId = this.liveMigrationId();
    if (!this.isRunning() && currentId && event.migrationId !== currentId) return;

    // Store the migration ID from the first event if we don't have one
    if (!currentId && this.isRunning()) {
      this.liveMigrationId.set(event.migrationId);
    }

    this.liveMigrationPhase.set('catchup');
    this.liveMigrationSourceVersion.set(event.sourceEventCount);
    // Store the target stream identifier for fetching target events during migration
    this.liveMigrationTargetStreamId.set(event.targetStreamId);
    this.addSystemLog(`Live migration started: ${event.sourceStreamId} -> ${event.targetStreamId}`);
  }

  private handleLiveMigrationIterationProgress(event: LiveMigrationIterationProgressEvent): void {
    // Accept events if we're running and either ID matches or we don't have an ID yet
    const currentId = this.liveMigrationId();
    if (!this.isRunning() && currentId && event.migrationId !== currentId) return;
    if (currentId && event.migrationId !== currentId) return;

    // Store the migration ID from the first event if we don't have one
    if (!currentId && this.isRunning()) {
      this.liveMigrationId.set(event.migrationId);
    }

    this.liveMigrationPhase.set(event.phase as any);
    this.liveMigrationIteration.set(event.iteration);
    this.liveMigrationSourceVersion.set(event.sourceVersion);
    this.liveMigrationTargetVersion.set(event.targetVersion);
    this.liveMigrationEventsBehind.set(event.eventsBehind);
    this.liveMigrationTotalEventsCopied.set(event.totalEventsCopied);
    this.liveMigrationElapsedTime.set(event.elapsedTime);
    this.liveMigrationIsSynced.set(event.isSynced);
    this.progress.set(event.percentage);

    if (event.message) {
      this.addSystemLog(`Iteration ${event.iteration}: ${event.message}`);
    }

    // Update migration phase based on SignalR event
    if (event.isSynced) {
      this.migrationPhase.set('cutover');
    } else {
      this.migrationPhase.set('replicating');
    }
  }

  private handleLiveMigrationEventCopied(event: LiveMigrationEventCopiedEvent): void {
    // Accept events if we're running and either ID matches or we don't have an ID yet
    const currentId = this.liveMigrationId();
    if (!this.isRunning() && currentId && event.migrationId !== currentId) return;
    if (currentId && event.migrationId !== currentId) return;

    // Store the migration ID from the first event if we don't have one
    if (!currentId && this.isRunning()) {
      this.liveMigrationId.set(event.migrationId);
    }

    this.lastCopiedEvent.set(event);
    this.eventsProcessed.set(event.totalEventsCopied);
    this.progress.set(event.percentage);

    // Track recent events for animation (keep last 5)
    this.recentCopiedEvents.update(events => [event, ...events].slice(0, 5));

    // Track transformed events
    if (event.wasTransformed) {
      this.liveMigrationEventsTransformed.update(n => n + 1);
    }

    // Add target event to display (placeholder - actual data loaded from storage shortly)
    // Use 1-based version for consistency with source events display
    const displayVersion = event.eventVersion + 1;

    const targetEvent: EventData = {
      id: `event-${displayVersion}`,
      version: displayVersion,
      type: event.eventType,
      timestamp: new Date(event.timestamp),
      data: { _loading: true }, // Placeholder - will be replaced with real data from storage
      schemaVersion: event.newSchemaVersion ?? 1,
      isLiveEvent: false,
      writtenTo: ['target']
    };
    this.targetEvents.update(events => [...events, targetEvent]);

    // Schedule a debounced refresh of target events from storage to get actual data
    this.scheduleTargetEventsRefresh();

    // Log transformation if it occurred
    if (event.wasTransformed) {
      this.addSystemLog(`Transformed: ${event.originalEventType} v${event.originalSchemaVersion} -> ${event.eventType} v${event.newSchemaVersion}`);
    }
  }

  private handleLiveMigrationCompleted(event: LiveMigrationCompletedEvent): void {
    // Accept events if we're running and either ID matches or we don't have an ID yet
    const currentId = this.liveMigrationId();
    if (!this.isRunning() && currentId && event.migrationId !== currentId) return;
    if (currentId && event.migrationId !== currentId) return;

    this.liveMigrationPhase.set('complete');
    this.migrationPhase.set('complete');
    this.liveMigrationTotalEventsCopied.set(event.totalEventsCopied);
    this.liveMigrationElapsedTime.set(event.elapsedTime);
    this.liveMigrationEventsTransformed.set(event.eventsTransformed);
    this.progress.set(100);
    this.isRunning.set(false);

    // Add the closing event to source stream display
    const sourceVersion = this.sourceEvents().length + 1;
    const closeEvent: EventData = {
      id: 'stream-closed',
      version: sourceVersion,
      type: 'EventStream.Closed',
      timestamp: new Date(),
      data: {
        reason: 'Migration completed',
        continuationStream: 'v2',
        closedAt: new Date().toISOString()
      },
      schemaVersion: 1,
      isLiveEvent: false,
      writtenTo: ['source']
    };
    this.sourceEvents.update(events => [...events, closeEvent]);

    // Reload target events from storage to get the actual transformed data
    const stream = this.selectedStream();
    if (stream) {
      this.loadTargetEventsFromStorage(stream.type, stream.id);
    }

    this.addSystemLog(`Migration complete! ${event.totalEventsCopied} events copied, ${event.eventsTransformed} transformed in ${event.iterations} iterations (${event.elapsedTime})`);
  }

  private scheduleTargetEventsRefresh(): void {
    // Clear any pending refresh
    if (this.targetRefreshTimeoutId) {
      clearTimeout(this.targetRefreshTimeoutId);
    }

    // Schedule a new refresh after a short debounce
    this.targetRefreshTimeoutId = setTimeout(() => {
      this.targetRefreshTimeoutId = null;
      const stream = this.selectedStream();
      if (stream && this.isRunning()) {
        this.refreshTargetEventsFromStorage(stream.type, stream.id);
      }
    }, this.TARGET_REFRESH_DEBOUNCE_MS);
  }

  private refreshTargetEventsFromStorage(objectType: string, objectId: string): void {
    // Use the target stream identifier if available (during migration)
    // This reads directly from the target blob stream, not the object document's active stream
    const targetStreamId = this.liveMigrationTargetStreamId();

    const observable = targetStreamId
      ? this.apiService.getTargetStreamEvents(objectId, objectType, targetStreamId)
      : this.apiService.getStreamEvents(objectType, objectId);

    const sub = observable.subscribe({
      next: (response) => {
        if (response?.events) {
          // Map the API response to our EventData format, marking as target events
          const storageEvents: EventData[] = response.events.map(evt => ({
            id: evt.id,
            version: evt.version,
            type: evt.type,
            timestamp: new Date(evt.timestamp),
            data: evt.data,
            schemaVersion: evt.schemaVersion,
            isLiveEvent: evt.isLiveEvent ?? false,
            writtenTo: ['target'] as ('source' | 'target')[]
          }));

          // Update existing target events with actual data from storage
          // but preserve any placeholder events that haven't been persisted yet
          this.targetEvents.update(currentEvents => {
            const updatedEvents: EventData[] = [];

            for (const current of currentEvents) {
              // Find matching event from storage by version
              const storageEvent = storageEvents.find(s => s.version === current.version);
              if (storageEvent) {
                // Replace placeholder with actual data from storage
                updatedEvents.push(storageEvent);
              } else {
                // Keep the placeholder (event not yet in storage)
                updatedEvents.push(current);
              }
            }

            return updatedEvents;
          });
        }
      },
      error: (err) => {
        console.error('Failed to refresh target events:', err);
      }
    });
    this.subscriptions.push(sub);
  }

  private loadTargetEventsFromStorage(objectType: string, objectId: string): void {
    // Cancel any pending debounced refresh
    if (this.targetRefreshTimeoutId) {
      clearTimeout(this.targetRefreshTimeoutId);
      this.targetRefreshTimeoutId = null;
    }

    const sub = this.apiService.getStreamEvents(objectType, objectId).subscribe({
      next: (response) => {
        if (response?.events) {
          // Map the API response to our EventData format, marking as target events
          const targetEvents: EventData[] = response.events.map(evt => ({
            id: evt.id,
            version: evt.version,
            type: evt.type,
            timestamp: new Date(evt.timestamp),
            data: evt.data,
            schemaVersion: evt.schemaVersion,
            isLiveEvent: evt.isLiveEvent ?? false,
            writtenTo: ['target']
          }));
          this.targetEvents.set(targetEvents);
        }
      },
      error: (err) => {
        console.error('Failed to reload target events:', err);
      }
    });
    this.subscriptions.push(sub);
  }

  private handleLiveMigrationFailed(event: LiveMigrationFailedEvent): void {
    // Accept events if we're running and either ID matches or we don't have an ID yet
    const currentId = this.liveMigrationId();
    if (!this.isRunning() && currentId && event.migrationId !== currentId) return;
    if (currentId && event.migrationId !== currentId) return;

    this.liveMigrationPhase.set('failed');
    this.liveMigrationError.set(event.error);
    this.isRunning.set(false);

    this.addSystemLog(`Migration failed: ${event.error} (after ${event.iterations} iterations, ${event.eventsCopiedBeforeFailure} events copied)`);
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(sub => sub.unsubscribe());

    // Clean up any pending debounced refresh
    if (this.targetRefreshTimeoutId) {
      clearTimeout(this.targetRefreshTimeoutId);
      this.targetRefreshTimeoutId = null;
    }
  }

  private loadAvailableStreams(): void {
    this.loadingStreams.set(true);
    const sub = this.apiService.getAvailableStreams().subscribe(response => {
      this.loadingStreams.set(false);
      if (response?.streams) {
        this.availableStreams.set(response.streams);
        // Auto-select the first stream if none selected
        if (!this.selectedStreamId() && response.streams.length > 0) {
          this.selectedStreamId.set(response.streams[0].id);
          // Load source events for the auto-selected stream
          this.loadSourceEvents();
        }
      }
    });
    this.subscriptions.push(sub);
  }

  selectStream(streamId: string): void {
    this.selectedStreamId.set(streamId);
    this.resetMigration();
    // Load source events for the selected stream
    this.loadSourceEvents();
  }

  private loadSourceEvents(): void {
    const stream = this.selectedStream();
    if (!stream) {
      return;
    }

    this.loadingSourceEvents.set(true);
    const sub = this.apiService.getStreamEvents(stream.type, stream.id).subscribe({
      next: (response) => {
        this.loadingSourceEvents.set(false);
        if (response?.events) {
          this.sourceEvents.set(response.events.map(e => ({
            id: e.id,
            version: e.version,
            type: e.type,
            timestamp: new Date(e.timestamp),
            data: e.data,
            schemaVersion: e.schemaVersion,
            isLiveEvent: e.isLiveEvent,
            writtenTo: e.writtenTo as ('source' | 'target')[]
          })));
        }
      },
      error: (err) => {
        this.loadingSourceEvents.set(false);
        console.error('Error loading source events:', err);
      }
    });
    this.subscriptions.push(sub);
  }

  toggleTransformation(): void {
    this.applyTransformation.update(v => !v);
    // Don't call resetMigration() here as it clears source events
    // Just reset migration state without clearing events
    this.isRunning.set(false);
    this.targetEvents.set([]);
    this.eventsProcessed.set(0);
    this.progress.set(0);
    this.migrationPhase.set('normal');
    this.liveEventLogs.set([]);
    // Note: sourceEvents are NOT cleared - they remain from the initial load
  }

  async startMigration(): Promise<void> {
    if (this.isRunning()) return;
    await this.startLiveMigration();
  }

  toggleSlowMode(): void {
    this.slowMode.update(v => !v);
  }

  private async startLiveMigration(): Promise<void> {
    const stream = this.selectedStream();
    if (!stream) {
      console.error('No stream selected');
      return;
    }

    // Store current source events - they should NOT be cleared during live migration
    const currentSourceEvents = this.sourceEvents();

    this.isRunning.set(true);
    this.targetEvents.set([]);
    this.eventsProcessed.set(0);
    this.progress.set(0);
    this.liveEventLogs.set([]);
    this.migrationPhase.set('normal');

    // Reset live migration state
    this.liveMigrationPhase.set('idle');
    this.liveMigrationIteration.set(0);
    this.liveMigrationSourceVersion.set(0);
    this.liveMigrationTargetVersion.set(0);
    this.liveMigrationEventsBehind.set(0);
    this.liveMigrationTotalEventsCopied.set(0);
    this.liveMigrationElapsedTime.set('00:00:00');
    this.liveMigrationIsSynced.set(false);
    this.liveMigrationEventsTransformed.set(0);
    this.liveMigrationError.set(null);
    this.liveMigrationTargetStreamId.set(null);
    this.recentCopiedEvents.set([]);
    this.lastCopiedEvent.set(null);

    // Ensure source events are preserved (they might have been cleared by signal reset)
    if (this.sourceEvents().length === 0 && currentSourceEvents.length > 0) {
      this.sourceEvents.set(currentSourceEvents);
    }

    this.addSystemLog('Starting live migration with real-time SignalR progress...');
    this.migrationPhase.set('replicating');

    // Call the API - progress will come via SignalR
    const sub = this.apiService.executeLiveMigration(
      stream.id,
      stream.type,
      this.applyTransformation(),
      this.slowMode() ? 500 : 0 // Add 500ms delay per event in slow mode
    ).subscribe({
      next: (result) => {
        if (result) {
          // Store the migration ID to filter SignalR events
          this.liveMigrationId.set(result.migrationId);

          // Ensure source events are still preserved after API call
          if (this.sourceEvents().length === 0 && currentSourceEvents.length > 0) {
            this.sourceEvents.set(currentSourceEvents);
          }

          if (!result.success) {
            this.liveMigrationError.set(result.error ?? 'Unknown error');
            this.liveMigrationPhase.set('failed');
            this.isRunning.set(false);
            this.addSystemLog(`Migration failed: ${result.error}`);
          } else {
            // Handle success from HTTP response (SignalR might have been missed)
            // Only update if we're still running (SignalR didn't already handle it)
            if (this.isRunning()) {
              // Store target stream ID from HTTP response if SignalR event was missed
              if (result.targetStreamId) {
                this.liveMigrationTargetStreamId.set(result.targetStreamId);
              }

              this.liveMigrationPhase.set('complete');
              this.migrationPhase.set('complete');
              this.liveMigrationTotalEventsCopied.set(result.totalEventsCopied);
              this.liveMigrationElapsedTime.set(result.elapsedTime);
              this.progress.set(100);
              this.isRunning.set(false);

              // Reload target events from storage to get actual transformed data
              if (stream) {
                this.loadTargetEventsFromStorage(stream.type, stream.id);
              }

              this.addSystemLog(`Migration complete! ${result.totalEventsCopied} events copied in ${result.iterations} iterations (${result.elapsedTime})`);
            }
          }
        } else {
          this.isRunning.set(false);
          this.addSystemLog('Failed to start live migration');
        }
      },
      error: (err) => {
        console.error('Error starting live migration:', err);
        this.isRunning.set(false);
        this.addSystemLog(`Error: ${err.message}`);
      }
    });
    this.subscriptions.push(sub);
  }

  private addSystemLog(message: string): void {
    this.liveEventLogs.update(logs => [{
      id: `sys-${Date.now()}`,
      timestamp: new Date(),
      eventType: 'System',
      phase: this.migrationPhase(),
      writtenTo: [],
      message: message
    }, ...logs].slice(0, 15));
  }

  addLiveEvent(): void {
    if (!this.canAddLiveEvent()) return;

    const stream = this.selectedStream();
    if (stream) {
      this.addLiveEventToStream(stream.id, stream.type);
    }
  }

  private addLiveEventToStream(objectId: string, objectType: string): void {
    this.liveEventCounter.update(c => c + 1);
    const counter = this.liveEventCounter();
    const eventTemplate = this.liveEventTypes[counter % this.liveEventTypes.length];
    const phase = this.migrationPhase();

    // After migration completes, events go to the target stream (object document now points to target)
    const isAfterMigration = phase === 'complete' || phase === 'cutover';
    const targetStreamName = isAfterMigration ? 'target' : 'source';

    this.addSystemLog(`Adding live event '${eventTemplate.type}' to ${targetStreamName} stream...`);

    const sub = this.apiService.addLiveEventToStream(objectId, objectType, eventTemplate.type, eventTemplate.data).subscribe({
      next: (response) => {
        if (response) {
          // Log the event - after migration, events go to target stream
          this.liveEventLogs.update(logs => [{
            id: `live-${counter}`,
            timestamp: new Date(),
            eventType: response.eventType,
            phase: phase,
            writtenTo: isAfterMigration ? ['Target'] : ['Source'],
            message: `Live event '${response.eventType}' written to ${targetStreamName} stream (v${response.eventVersion})`
          }, ...logs].slice(0, 10));

          // After migration, reload target events (object document now points to target)
          // Before migration, reload source events
          if (isAfterMigration) {
            this.loadTargetEventsFromStorage(objectType, objectId);
          } else {
            this.loadSourceEvents();
          }
        } else {
          // API returned null (error was caught in service)
          this.addSystemLog(`Failed to add live event - API returned null`);
          this.liveEventCounter.update(c => c - 1);
        }
      },
      error: (err) => {
        console.error('Error adding live event:', err);
        this.addSystemLog(`Failed to add live event: ${err.message || 'Unknown error'}`);
        this.liveEventCounter.update(c => c - 1);
      }
    });
    this.subscriptions.push(sub);
  }

  resetMigration(): void {
    this.migrationPhase.set('idle');
    this.isRunning.set(false);
    this.progress.set(0);
    this.eventsProcessed.set(0);
    this.targetEvents.set([]);
    this.sourceEvents.set([]);
    this.liveEventLogs.set([]);
    this.liveEventCounter.set(0);

    // Reset live migration state
    this.liveMigrationId.set(null);
    this.liveMigrationPhase.set('idle');
    this.liveMigrationIteration.set(0);
    this.liveMigrationSourceVersion.set(0);
    this.liveMigrationTargetVersion.set(0);
    this.liveMigrationEventsBehind.set(0);
    this.liveMigrationTotalEventsCopied.set(0);
    this.liveMigrationElapsedTime.set('00:00:00');
    this.liveMigrationIsSynced.set(false);
    this.liveMigrationEventsTransformed.set(0);
    this.liveMigrationError.set(null);
    this.liveMigrationTargetStreamId.set(null);
    this.recentCopiedEvents.set([]);
    this.lastCopiedEvent.set(null);
  }

  formatDate(date: Date): string {
    return new Date(date).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric'
    });
  }

  formatTime(date: Date): string {
    return new Date(date).toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });
  }

  getEventIcon(type: string): string {
    if (type.includes('Created')) return 'add_circle';
    if (type.includes('Assignee')) return 'person';
    if (type.includes('Status')) return 'sync';
    if (type.includes('Completed')) return 'check_circle';
    if (type.includes('Comment')) return 'comment';
    if (type.includes('Priority')) return 'priority_high';
    if (type.includes('Label')) return 'label';
    if (type.includes('Attachment')) return 'attach_file';
    return 'event';
  }

  formatData(data: Record<string, unknown>): string {
    return JSON.stringify(data, null, 2);
  }

  getPhaseDescription(): string {
    const phase = this.migrationPhase();
    switch (phase) {
      case 'idle': return 'Click "Start Migration" to begin';
      case 'normal': return 'Events are written to source stream only';
      case 'replicating': return 'Copying events from source to target stream';
      case 'tailing': return 'Catching up new events before cutover';
      case 'cutover': return 'Switching to target stream...';
      case 'complete': return 'Migration complete! All operations now use target stream';
      default: return '';
    }
  }
}
