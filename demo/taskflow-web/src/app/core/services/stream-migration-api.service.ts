import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

export interface AvailableStream {
  id: string;
  type: string;
  name: string;
  eventCount: number;
}

export interface AvailableStreamsResponse {
  streams: AvailableStream[];
}

export interface StreamEventDto {
  id: string;
  version: number;
  type: string;
  timestamp: string;
  data: Record<string, unknown>;
  schemaVersion: number;
  isLiveEvent: boolean;
  writtenTo: string[];
}

export interface StreamEventsResponse {
  streamId: string;
  streamType: string;
  events: StreamEventDto[];
  totalEvents: number;
}

export interface ExecuteLiveMigrationRequest {
  objectId: string;
  objectType: string;
  applyTransformation?: boolean;
  demoDelayMs?: number; // Delay in milliseconds per event (0 = no delay)
}

export interface LiveMigrationResult {
  success: boolean;
  migrationId: string;
  sourceStreamId: string;
  targetStreamId: string;
  totalEventsCopied: number;
  iterations: number;
  elapsedTime: string;
  eventsTransformed: number;
  error?: string;
}

export interface AddLiveEventResponse {
  success: boolean;
  eventVersion: number;
  eventType: string;
  schemaVersion: number;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class StreamMigrationApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/admin/migration';

  /**
   * Get available streams that can be used for migration demo
   */
  getAvailableStreams(): Observable<AvailableStreamsResponse | null> {
    return this.http.get<AvailableStreamsResponse>(`${this.baseUrl}/streams`).pipe(
      catchError(err => {
        console.error('Error fetching available streams:', err);
        return of(null);
      })
    );
  }

  /**
   * Get events for a specific stream
   */
  getStreamEvents(objectType: string, objectId: string): Observable<StreamEventsResponse | null> {
    return this.http.get<StreamEventsResponse>(`${this.baseUrl}/streams/${objectType}/${objectId}/events`).pipe(
      catchError(err => {
        console.error('Error fetching stream events:', err);
        return of(null);
      })
    );
  }

  /**
   * Execute a live migration with real-time SignalR progress updates.
   * Progress is received via SignalR events, not through the HTTP response.
   * @param demoDelayMs Optional delay in milliseconds per event for demo purposes (0 = no delay)
   */
  executeLiveMigration(
    objectId: string,
    objectType: string,
    applyTransformation: boolean = true,
    demoDelayMs: number = 0
  ): Observable<LiveMigrationResult | null> {
    return this.http.post<LiveMigrationResult>(`${this.baseUrl}/execute-live`, {
      objectId,
      objectType,
      applyTransformation,
      demoDelayMs
    }).pipe(
      catchError(err => {
        console.error('Error executing live migration:', err);
        return of(null);
      })
    );
  }

  /**
   * Add a live event directly to a stream (used during live migration)
   */
  addLiveEventToStream(
    objectId: string,
    objectType: string,
    eventType: string,
    eventData: Record<string, unknown>
  ): Observable<AddLiveEventResponse | null> {
    return this.http.post<AddLiveEventResponse>(`${this.baseUrl}/streams/${objectType}/${objectId}/live-event`, {
      eventType,
      eventData
    }).pipe(
      catchError(err => {
        console.error('Error adding live event to stream:', err);
        return of(null);
      })
    );
  }

  /**
   * Get events from a specific target stream identifier (used during migration).
   * This reads directly from the blob stream, bypassing the object document.
   */
  getTargetStreamEvents(
    objectId: string,
    objectType: string,
    streamIdentifier: string
  ): Observable<StreamEventsResponse | null> {
    return this.http.get<StreamEventsResponse>(
      `${this.baseUrl}/streams/${objectType}/${objectId}/target-events/${encodeURIComponent(streamIdentifier)}`
    ).pipe(
      catchError(err => {
        console.error('Error fetching target stream events:', err);
        return of(null);
      })
    );
  }
}
