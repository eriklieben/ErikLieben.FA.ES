import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EpicDto, EpicListDto, CreateEpicRequest, CommandResult } from '../contracts/epic.contracts';

@Injectable({
  providedIn: 'root'
})
export class EpicApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/epics`;
  private readonly adminUrl = `${environment.apiUrl}/api/admin`;

  getAll(): Observable<EpicListDto[]> {
    return this.http.get<EpicListDto[]>(this.baseUrl);
  }

  getById(id: string): Observable<EpicDto> {
    return this.http.get<EpicDto>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateEpicRequest): Observable<CommandResult> {
    return this.http.post<CommandResult>(this.baseUrl, request);
  }

  rename(id: string, newName: string): Observable<CommandResult> {
    return this.http.put<CommandResult>(`${this.baseUrl}/${id}/rename`, { newName });
  }

  updateDescription(id: string, newDescription: string): Observable<CommandResult> {
    return this.http.put<CommandResult>(`${this.baseUrl}/${id}/description`, { newDescription });
  }

  addProject(id: string, projectId: string): Observable<CommandResult> {
    return this.http.post<CommandResult>(`${this.baseUrl}/${id}/projects`, { projectId });
  }

  removeProject(id: string, projectId: string): Observable<CommandResult> {
    return this.http.delete<CommandResult>(`${this.baseUrl}/${id}/projects/${projectId}`);
  }

  changeTargetDate(id: string, newTargetDate: string): Observable<CommandResult> {
    return this.http.put<CommandResult>(`${this.baseUrl}/${id}/target-date`, { newTargetDate });
  }

  changePriority(id: string, newPriority: string): Observable<CommandResult> {
    return this.http.put<CommandResult>(`${this.baseUrl}/${id}/priority`, { newPriority });
  }

  complete(id: string, summary: string): Observable<CommandResult> {
    return this.http.post<CommandResult>(`${this.baseUrl}/${id}/complete`, { summary });
  }

  seedDemoEpics(): Observable<any> {
    return this.http.post<any>(`${this.adminUrl}/demo/seed-epics`, {});
  }

  getEvents(id: string): Observable<any> {
    return this.http.get<any>(`${this.adminUrl}/events/epic/${id}`);
  }
}
