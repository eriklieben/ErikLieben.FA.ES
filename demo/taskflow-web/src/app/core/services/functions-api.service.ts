import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import {
  FunctionsWorkItemResponse,
  FunctionsWorkItemResponseSchema,
  KanbanBoardResponse,
  KanbanBoardResponseSchema,
  ActiveWorkItemsResponse,
  ActiveWorkItemsResponseSchema,
  UserProfilesResponse,
  UserProfilesResponseSchema,
  FunctionsCommandResult,
  FunctionsCommandResultSchema,
  AssignWorkItemFunctionsRequest,
  CreateWorkItemFunctionsRequest
} from '../contracts/functions.contracts';

@Injectable({
  providedIn: 'root'
})
export class FunctionsApiService {
  private readonly http = inject(HttpClient);

  // Use relative URL - proxy will handle routing to Azure Functions
  private readonly baseUrl = '/functions-api';

  // ====================
  // WorkItem Functions
  // ====================

  /**
   * Get a work item by ID using the EventStream input binding
   */
  getWorkItem(id: string): Observable<FunctionsWorkItemResponse | null> {
    return this.http.get(`${this.baseUrl}/workitems/${id}`).pipe(
      map(response => FunctionsWorkItemResponseSchema.parse(response)),
      catchError(err => {
        console.error('Error fetching work item:', err);
        return of(null);
      })
    );
  }

  /**
   * Assign a work item to a member
   */
  assignWorkItem(id: string, request: AssignWorkItemFunctionsRequest): Observable<FunctionsCommandResult | null> {
    return this.http.post(`${this.baseUrl}/workitems/${id}/assign`, request).pipe(
      map(response => FunctionsCommandResultSchema.parse(response)),
      catchError(err => {
        console.error('Error assigning work item:', err);
        return of(null);
      })
    );
  }

  /**
   * Create a new work item
   */
  createWorkItem(request: CreateWorkItemFunctionsRequest): Observable<FunctionsCommandResult | null> {
    return this.http.post(`${this.baseUrl}/workitems`, request).pipe(
      map(response => FunctionsCommandResultSchema.parse(response)),
      catchError(err => {
        console.error('Error creating work item:', err);
        return of(null);
      })
    );
  }

  // ====================
  // Projection Functions
  // ====================

  /**
   * Get the Kanban Board projection using the Projection input binding
   */
  getKanbanBoard(): Observable<KanbanBoardResponse | null> {
    return this.http.get(`${this.baseUrl}/projections/kanban`).pipe(
      map(response => KanbanBoardResponseSchema.parse(response)),
      catchError(err => {
        console.error('Error fetching kanban board:', err);
        return of(null);
      })
    );
  }

  /**
   * Get the Active Work Items projection
   */
  getActiveWorkItems(): Observable<ActiveWorkItemsResponse | null> {
    return this.http.get(`${this.baseUrl}/projections/active-workitems`).pipe(
      map(response => ActiveWorkItemsResponseSchema.parse(response)),
      catchError(err => {
        console.error('Error fetching active work items:', err);
        return of(null);
      })
    );
  }

  /**
   * Get the User Profiles projection
   */
  getUserProfiles(): Observable<UserProfilesResponse | null> {
    return this.http.get(`${this.baseUrl}/projections/userprofiles`).pipe(
      map(response => UserProfilesResponseSchema.parse(response)),
      catchError(err => {
        console.error('Error fetching user profiles:', err);
        return of(null);
      })
    );
  }

  /**
   * Trigger projection refresh using [ProjectionOutput<T>] binding
   */
  refreshProjections(): Observable<FunctionsCommandResult | null> {
    return this.http.post(`${this.baseUrl}/projections/refresh`, {}).pipe(
      map(response => FunctionsCommandResultSchema.parse(response)),
      catchError(err => {
        console.error('Error refreshing projections:', err);
        return of(null);
      })
    );
  }

  // ====================
  // Health Check
  // ====================

  /**
   * Check if Azure Functions is available using the simple health endpoint
   */
  checkHealth(): Observable<boolean> {
    return this.http.get<{ status: string; timestamp: string; service: string }>(`${this.baseUrl}/health`).pipe(
      map(response => response.status === 'healthy'),
      catchError(err => {
        console.error('Health check failed:', err);
        return of(false);
      })
    );
  }

  /**
   * Get the raw health check response for debugging
   */
  getHealthStatus(): Observable<{ status: string; timestamp: string; service: string } | null> {
    return this.http.get<{ status: string; timestamp: string; service: string }>(`${this.baseUrl}/health`).pipe(
      catchError(err => {
        console.error('Health check failed:', err);
        return of(null);
      })
    );
  }
}
