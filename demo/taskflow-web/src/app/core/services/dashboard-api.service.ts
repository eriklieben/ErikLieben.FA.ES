import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  ProjectMetrics,
  ProjectMetricsSchema,
  ProjectSummary,
  ProjectSummarySchema,
  ActiveWorkItem,
  ActiveWorkItemSchema,
  OverdueWorkItem,
  OverdueWorkItemSchema,
  ProjectAvailableLanguages,
  ProjectAvailableLanguagesSchema,
  ProjectKanbanByLanguage,
  ProjectKanbanByLanguageSchema,
} from '../contracts/dashboard.contracts';

@Injectable({
  providedIn: 'root'
})
export class DashboardApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/queries`;

  getAllProjects(): Observable<ProjectSummary[]> {
    return this.http.get(`${this.baseUrl}/projects`).pipe(
      map(response => {
        if (!Array.isArray(response)) {
          return [];
        }
        return response.map(item => ProjectSummarySchema.parse(item));
      })
    );
  }

  getProjectMetrics(projectId: string): Observable<ProjectMetrics> {
    return this.http.get(`${this.baseUrl}/projects/${projectId}/metrics`).pipe(
      map(response => ProjectMetricsSchema.parse(response))
    );
  }

  getActiveProjects(): Observable<ProjectSummary[]> {
    return this.http.get(`${this.baseUrl}/projects/active`).pipe(
      map(response => {
        if (!Array.isArray(response)) {
          return [];
        }
        return response.map(item => ProjectSummarySchema.parse(item));
      })
    );
  }

  getActiveWorkItems(): Observable<ActiveWorkItem[]> {
    return this.http.get(`${this.baseUrl}/workitems/active`).pipe(
      map(response => {
        if (!Array.isArray(response)) {
          return [];
        }
        return response.map(item => ActiveWorkItemSchema.parse(item));
      })
    );
  }

  getActiveWorkItemsByProject(projectId: string): Observable<ActiveWorkItem[]> {
    return this.http.get(`${this.baseUrl}/workitems/active/by-project/${projectId}`).pipe(
      map(response => {
        if (!Array.isArray(response)) {
          return [];
        }
        return response.map(item => ActiveWorkItemSchema.parse(item));
      })
    );
  }

  getActiveWorkItemsByAssignee(memberId: string): Observable<ActiveWorkItem[]> {
    return this.http.get(`${this.baseUrl}/workitems/active/by-assignee/${memberId}`).pipe(
      map(response => {
        if (!Array.isArray(response)) {
          return [];
        }
        return response.map(item => ActiveWorkItemSchema.parse(item));
      })
    );
  }

  getOverdueWorkItems(): Observable<OverdueWorkItem[]> {
    return this.http.get(`${this.baseUrl}/workitems/overdue`).pipe(
      map(response => {
        if (!Array.isArray(response)) {
          return [];
        }
        return response.map(item => OverdueWorkItemSchema.parse(item));
      })
    );
  }

  getProjectKanbanOrder(projectId: string): Observable<{
    projectId: string;
    plannedItemsOrder: string[];
    inProgressItemsOrder: string[];
    completedItemsOrder: string[];
  }> {
    return this.http.get(`${this.baseUrl}/projects/${projectId}/kanban-order`).pipe(
      map(response => response as any)
    );
  }

  getProjectionMetadata(projectionName: string): Observable<{
    name: string;
    lastModified: string;
    contentLength: number;
    contentType: string;
  }> {
    return this.http.get(`${environment.apiUrl}/api/admin/projections/${projectionName}/metadata`).pipe(
      map(response => response as any)
    );
  }

  getProjectAvailableLanguages(projectId: string): Observable<ProjectAvailableLanguages> {
    return this.http.get(`${this.baseUrl}/projects/${projectId}/available-languages`).pipe(
      map(response => ProjectAvailableLanguagesSchema.parse(response))
    );
  }

  getProjectKanbanByLanguage(projectId: string, languageCode: string): Observable<ProjectKanbanByLanguage> {
    return this.http.get(`${this.baseUrl}/projects/${projectId}/kanban/${languageCode}`).pipe(
      map(response => ProjectKanbanByLanguageSchema.parse(response))
    );
  }
}
