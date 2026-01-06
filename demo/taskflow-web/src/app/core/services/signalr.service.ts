import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Observable, Subject, BehaviorSubject } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ProjectInitiatedEvent {
  projectId: string;
  name: string;
}

export interface WorkItemEvent {
  workItemId: string;
  projectId?: string;
  [key: string]: any;
}

export interface ProjectionUpdatedEvent {
  projections: Array<{
    name: string;
    storageType: string;
    status: string;
    lastUpdate: string;
    checkpoint: number;
    checkpointFingerprint: string;
    isPersisted: boolean;
    lastGenerationDurationMs: number | null;
  }>;
  timestamp: string;
}

export interface ProjectionBuildProgressEvent {
  projectionName: string;
  status: 'building' | 'completed' | 'error' | 'skipped';
  current: number;
  total: number;
  message?: string;
  timestamp: string;
}

export interface SeedProgressEvent {
  provider: 'blob' | 'table' | 'cosmos';
  current: number;
  total: number;
  percentage: number;
  message?: string;
  timestamp: string;
}

export interface LiveMigrationStartedEvent {
  migrationId: string;
  sourceStreamId: string;
  targetStreamId: string;
  sourceEventCount: number;
  timestamp: string;
}

export interface LiveMigrationIterationProgressEvent {
  migrationId: string;
  phase: 'catchup' | 'synced' | 'closing' | 'complete' | 'failed';
  iteration: number;
  sourceVersion: number;
  targetVersion: number;
  eventsBehind: number;
  eventsCopiedThisIteration: number;
  totalEventsCopied: number;
  percentage: number;
  elapsedTime: string;
  isSynced: boolean;
  message?: string;
  timestamp: string;
}

export interface LiveMigrationEventCopiedEvent {
  migrationId: string;
  eventVersion: number;
  eventType: string;
  wasTransformed: boolean;
  originalEventType?: string;
  originalSchemaVersion?: number;
  newSchemaVersion?: number;
  totalEventsCopied: number;
  sourceVersion: number;
  percentage: number;
  timestamp: string;
}

export interface LiveMigrationCompletedEvent {
  migrationId: string;
  totalEventsCopied: number;
  iterations: number;
  elapsedTime: string;
  eventsTransformed: number;
  timestamp: string;
}

export interface LiveMigrationFailedEvent {
  migrationId: string;
  error: string;
  iterations: number;
  eventsCopiedBeforeFailure: number;
  timestamp: string;
}

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection: HubConnection | null = null;
  private readonly connectionState$ = new BehaviorSubject<HubConnectionState>(HubConnectionState.Disconnected);

  // Event subjects
  private readonly projectInitiated$ = new Subject<ProjectInitiatedEvent>();
  private readonly projectRebranded$ = new Subject<any>();
  private readonly projectCompleted$ = new Subject<any>();
  private readonly teamMemberAdded$ = new Subject<any>();
  private readonly workItemPlanned$ = new Subject<WorkItemEvent>();
  private readonly workItemChanged$ = new Subject<WorkItemEvent>();
  private readonly workCompleted$ = new Subject<WorkItemEvent>();
  private readonly workItemRelocated$ = new Subject<any>();
  private readonly projectionUpdated$ = new Subject<ProjectionUpdatedEvent>();
  private readonly projectionBuildProgress$ = new Subject<ProjectionBuildProgressEvent>();
  private readonly seedProgress$ = new Subject<SeedProgressEvent>();
  private readonly eventOccurred$ = new Subject<any>();

  // Live migration event subjects
  private readonly liveMigrationStarted$ = new Subject<LiveMigrationStartedEvent>();
  private readonly liveMigrationIterationProgress$ = new Subject<LiveMigrationIterationProgressEvent>();
  private readonly liveMigrationEventCopied$ = new Subject<LiveMigrationEventCopiedEvent>();
  private readonly liveMigrationCompleted$ = new Subject<LiveMigrationCompletedEvent>();
  private readonly liveMigrationFailed$ = new Subject<LiveMigrationFailedEvent>();

  get connectionState(): Observable<HubConnectionState> {
    return this.connectionState$.asObservable();
  }

  get onProjectInitiated(): Observable<ProjectInitiatedEvent> {
    return this.projectInitiated$.asObservable();
  }

  get onProjectRebranded(): Observable<any> {
    return this.projectRebranded$.asObservable();
  }

  get onProjectCompleted(): Observable<any> {
    return this.projectCompleted$.asObservable();
  }

  get onTeamMemberAdded(): Observable<any> {
    return this.teamMemberAdded$.asObservable();
  }

  get onWorkItemPlanned(): Observable<WorkItemEvent> {
    return this.workItemPlanned$.asObservable();
  }

  get onWorkItemChanged(): Observable<WorkItemEvent> {
    return this.workItemChanged$.asObservable();
  }

  get onWorkCompleted(): Observable<WorkItemEvent> {
    return this.workCompleted$.asObservable();
  }

  get onWorkItemRelocated(): Observable<any> {
    return this.workItemRelocated$.asObservable();
  }

  get onProjectionUpdated(): Observable<ProjectionUpdatedEvent> {
    return this.projectionUpdated$.asObservable();
  }

  get onProjectionBuildProgress(): Observable<ProjectionBuildProgressEvent> {
    return this.projectionBuildProgress$.asObservable();
  }

  get onSeedProgress(): Observable<SeedProgressEvent> {
    return this.seedProgress$.asObservable();
  }

  get onEventOccurred(): Observable<any> {
    return this.eventOccurred$.asObservable();
  }

  get onLiveMigrationStarted(): Observable<LiveMigrationStartedEvent> {
    return this.liveMigrationStarted$.asObservable();
  }

  get onLiveMigrationIterationProgress(): Observable<LiveMigrationIterationProgressEvent> {
    return this.liveMigrationIterationProgress$.asObservable();
  }

  get onLiveMigrationEventCopied(): Observable<LiveMigrationEventCopiedEvent> {
    return this.liveMigrationEventCopied$.asObservable();
  }

  get onLiveMigrationCompleted(): Observable<LiveMigrationCompletedEvent> {
    return this.liveMigrationCompleted$.asObservable();
  }

  get onLiveMigrationFailed(): Observable<LiveMigrationFailedEvent> {
    return this.liveMigrationFailed$.asObservable();
  }

  async connect(): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      return;
    }

    this.hubConnection = new HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Retry intervals
      .configureLogging(LogLevel.Information)
      .build();

    // Register event handlers
    this.hubConnection.on('ProjectInitiated', (data: ProjectInitiatedEvent) => {
      this.projectInitiated$.next(data);
    });

    this.hubConnection.on('ProjectRebranded', (data: any) => {
      this.projectRebranded$.next(data);
    });

    this.hubConnection.on('ProjectCompleted', (data: any) => {
      this.projectCompleted$.next(data);
    });

    this.hubConnection.on('TeamMemberAdded', (data: any) => {
      this.teamMemberAdded$.next(data);
    });

    this.hubConnection.on('WorkItemPlanned', (data: WorkItemEvent) => {
      this.workItemPlanned$.next(data);
    });

    this.hubConnection.on('WorkItemChanged', (data: WorkItemEvent) => {
      this.workItemChanged$.next(data);
    });

    this.hubConnection.on('WorkCompleted', (data: WorkItemEvent) => {
      this.workCompleted$.next(data);
    });

    this.hubConnection.on('WorkItemRelocated', (data: any) => {
      this.workItemRelocated$.next(data);
    });

    this.hubConnection.on('ProjectionUpdated', (data: ProjectionUpdatedEvent) => {
      this.projectionUpdated$.next(data);
    });

    this.hubConnection.on('ProjectionBuildProgress', (data: ProjectionBuildProgressEvent) => {
      this.projectionBuildProgress$.next(data);
    });

    this.hubConnection.on('SeedProgress', (data: SeedProgressEvent) => {
      this.seedProgress$.next(data);
    });

    this.hubConnection.on('EventOccurred', (data: any) => {
      this.eventOccurred$.next(data);
    });

    // Live migration event handlers
    this.hubConnection.on('LiveMigrationStarted', (data: LiveMigrationStartedEvent) => {
      this.liveMigrationStarted$.next(data);
    });

    this.hubConnection.on('LiveMigrationIterationProgress', (data: LiveMigrationIterationProgressEvent) => {
      this.liveMigrationIterationProgress$.next(data);
    });

    this.hubConnection.on('LiveMigrationEventCopied', (data: LiveMigrationEventCopiedEvent) => {
      this.liveMigrationEventCopied$.next(data);
    });

    this.hubConnection.on('LiveMigrationCompleted', (data: LiveMigrationCompletedEvent) => {
      this.liveMigrationCompleted$.next(data);
    });

    this.hubConnection.on('LiveMigrationFailed', (data: LiveMigrationFailedEvent) => {
      this.liveMigrationFailed$.next(data);
    });

    this.hubConnection.onreconnecting(() => {
      this.connectionState$.next(HubConnectionState.Reconnecting);
    });

    this.hubConnection.onreconnected(() => {
      this.connectionState$.next(HubConnectionState.Connected);
    });

    this.hubConnection.onclose(() => {
      this.connectionState$.next(HubConnectionState.Disconnected);
    });

    try {
      await this.hubConnection.start();
      this.connectionState$.next(HubConnectionState.Connected);
      console.log('SignalR connected successfully');
    } catch (err) {
      console.error('Error connecting to SignalR:', err);
      this.connectionState$.next(HubConnectionState.Disconnected);
      throw err;
    }
  }

  async disconnect(): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.stop();
      this.hubConnection = null;
      this.connectionState$.next(HubConnectionState.Disconnected);
    }
  }

  async joinProject(projectId: string): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      await this.hubConnection.invoke('JoinProject', projectId);
    }
  }

  async leaveProject(projectId: string): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      await this.hubConnection.invoke('LeaveProject', projectId);
    }
  }
}
