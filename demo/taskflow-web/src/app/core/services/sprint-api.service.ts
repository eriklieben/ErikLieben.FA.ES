import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SprintDto, SprintListDto, SprintStatistics, CreateSprintRequest, CommandResult } from '../contracts/sprint.contracts';

@Injectable({
  providedIn: 'root'
})
export class SprintApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/sprints`;
  private readonly adminUrl = `${environment.apiUrl}/api/admin`;

  getAll(): Observable<SprintListDto[]> {
    return this.http.get<SprintListDto[]>(this.baseUrl);
  }

  getById(id: string): Observable<SprintDto> {
    return this.http.get<SprintDto>(`${this.baseUrl}/${id}`);
  }

  getStatistics(): Observable<SprintStatistics> {
    return this.http.get<SprintStatistics>(`${this.baseUrl}/statistics`);
  }

  create(request: CreateSprintRequest): Observable<CommandResult> {
    return this.http.post<CommandResult>(this.baseUrl, request);
  }

  start(id: string): Observable<CommandResult> {
    return this.http.post<CommandResult>(`${this.baseUrl}/${id}/start`, {});
  }

  complete(id: string, summary: string): Observable<CommandResult> {
    return this.http.post<CommandResult>(`${this.baseUrl}/${id}/complete`, { summary });
  }

  cancel(id: string, reason: string): Observable<CommandResult> {
    return this.http.post<CommandResult>(`${this.baseUrl}/${id}/cancel`, { reason });
  }

  addWorkItem(sprintId: string, workItemId: string): Observable<CommandResult> {
    return this.http.post<CommandResult>(`${this.baseUrl}/${sprintId}/workitems`, { workItemId });
  }

  removeWorkItem(sprintId: string, workItemId: string): Observable<CommandResult> {
    return this.http.delete<CommandResult>(`${this.baseUrl}/${sprintId}/workitems/${workItemId}`);
  }

  updateGoal(id: string, goal: string): Observable<CommandResult> {
    return this.http.put<CommandResult>(`${this.baseUrl}/${id}/goal`, { goal });
  }

  updateDates(id: string, startDate: string, endDate: string): Observable<CommandResult> {
    return this.http.put<CommandResult>(`${this.baseUrl}/${id}/dates`, { startDate, endDate });
  }

  seedDemoSprints(): Observable<any> {
    return this.http.post<any>(`${this.adminUrl}/demo/seed-sprints`, {});
  }
}
