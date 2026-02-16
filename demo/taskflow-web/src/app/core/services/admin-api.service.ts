import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  DomainEvent,
  DomainEventSchema,
  WorkItemVersionState,
  WorkItemVersionStateSchema,
  ProjectVersionState,
  ProjectVersionStateSchema,
  EnrichedProjectVersionState,
  EnrichedProjectVersionStateSchema,
  SnapshotResult,
  SnapshotResultSchema,
  SeedDataResult,
  SeedDataResultSchema,
  StorageConnection,
  StorageConnectionSchema,
  EventUpcastingDemonstration,
  EventUpcastingDemonstrationSchema,
  AuditLogResponse,
  AuditLogResponseSchema,
  ReportingIndexResponse,
  ReportingIndexResponseSchema,
  ProjectionStatus,
  ProjectionStatusSchema,
} from '../contracts/admin.contracts';

@Injectable({
  providedIn: 'root'
})
export class AdminApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/admin`;

  // Event exploration
  getProjectEvents(projectId: string): Observable<DomainEvent[]> {
    return this.http.get(`${this.baseUrl}/events/project/${projectId}`).pipe(
      map(response => {
        if (!Array.isArray(response)) {
          return [];
        }
        return response.map(item => DomainEventSchema.parse(item));
      })
    );
  }

  getWorkItemEvents(workItemId: string): Observable<DomainEvent[]> {
    return this.http.get(`${this.baseUrl}/events/workitem/${workItemId}`).pipe(
      map(response => {
        if (!Array.isArray(response)) {
          return [];
        }
        return response.map(item => DomainEventSchema.parse(item));
      })
    );
  }

  // Time travel
  getWorkItemAtVersion(workItemId: string, version: number): Observable<WorkItemVersionState> {
    return this.http.get(`${this.baseUrl}/workitems/${workItemId}/version/${version}`).pipe(
      map(response => WorkItemVersionStateSchema.parse(response))
    );
  }

  getProjectAtVersion(projectId: string, version: number): Observable<ProjectVersionState> {
    return this.http.get(`${this.baseUrl}/projects/${projectId}/version/${version}`).pipe(
      map(response => ProjectVersionStateSchema.parse(response))
    );
  }

  getProjectAtVersionEnriched(projectId: string, version: number): Observable<EnrichedProjectVersionState> {
    return this.http.get(`${this.baseUrl}/projects/${projectId}/version/${version}/enriched`).pipe(
      map(response => EnrichedProjectVersionStateSchema.parse(response))
    );
  }

  // Snapshots
  createWorkItemSnapshot(workItemId: string): Observable<SnapshotResult> {
    return this.http.post(`${this.baseUrl}/workitems/${workItemId}/snapshot`, {}).pipe(
      map(response => SnapshotResultSchema.parse(response))
    );
  }

  createProjectSnapshot(projectId: string): Observable<SnapshotResult> {
    return this.http.post(`${this.baseUrl}/projects/${projectId}/snapshot`, {}).pipe(
      map(response => SnapshotResultSchema.parse(response))
    );
  }

  // Demo data
  seedDemoData(): Observable<SeedDataResult> {
    return this.http.post(`${this.baseUrl}/demo/seed`, {}).pipe(
      map(response => SeedDataResultSchema.parse(response))
    );
  }

  seedDemoSprints(): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/demo/seed-sprints`, {});
  }

  seedDemoReleases(): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/demo/seed-releases`, {});
  }

  // Storage connection
  getStorageConnection(): Observable<StorageConnection> {
    return this.http.get(`${this.baseUrl}/storage/connection`).pipe(
      map(response => StorageConnectionSchema.parse(response))
    );
  }

  // Projection management
  getProjectionStatus(): Observable<ProjectionStatus[]> {
    return this.http.get<ProjectionStatus[]>(`${this.baseUrl}/projections`).pipe(
      map(response => response.map(p => ProjectionStatusSchema.parse(p)))
    );
  }

  getProjectionJson(name: string): Observable<string> {
    return this.http.get(`${this.baseUrl}/projections/${name}/json`, { responseType: 'text' });
  }

  rebuildProjection(name: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/projections/${name}/rebuild`, {});
  }

  /**
   * Build all projections from the event store.
   * Progress is tracked via SignalR (ProjectionBuildProgress event).
   */
  buildAllProjections(): Observable<any> {
    return this.http.post(`${this.baseUrl}/projections/build-all`, {});
  }

  // Event Upcasting Demonstration
  getEventUpcastingDemonstration(): Observable<EventUpcastingDemonstration> {
    return this.http.get(`${this.baseUrl}/projections/eventupcastingdemonstration/json`).pipe(
      map(response => EventUpcastingDemonstrationSchema.parse(response))
    );
  }

  // Storage Provider Status
  getStorageProviderStatus(): Observable<StorageProviderStatus> {
    return this.http.get<StorageProviderStatus>(`${this.baseUrl}/storage/providers`);
  }

  // Audit Log
  getWorkItemAuditLog(workItemId: string): Observable<AuditLogResponse> {
    return this.http.get(`${this.baseUrl}/audit-log/workitem/${workItemId}`).pipe(
      map(response => AuditLogResponseSchema.parse(response))
    );
  }

  // Reporting Index
  getWorkItemReportingIndex(): Observable<ReportingIndexResponse> {
    return this.http.get(`${this.baseUrl}/reporting-index`).pipe(
      map(response => ReportingIndexResponseSchema.parse(response))
    );
  }
}

export interface StorageProviderInfo {
  enabled: boolean;
  name: string;
}

export interface StorageProviderStatus {
  providers: {
    blob: StorageProviderInfo;
    table: StorageProviderInfo;
    cosmos: StorageProviderInfo;
    s3: StorageProviderInfo;
  };
  timestamp: string;
}
