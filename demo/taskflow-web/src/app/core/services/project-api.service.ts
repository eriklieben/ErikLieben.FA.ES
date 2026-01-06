import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  ProjectDto,
  ProjectDtoSchema,
  InitiateProjectRequest,
  RebrandProjectRequest,
  RefineScopeRequest,
  CompleteProjectRequest,
  ReactivateProjectRequest,
  AddTeamMemberRequest,
  ReorderWorkItemRequest,
  CommandResult,
  CommandResultSchema,
} from '../contracts/project.contracts';

@Injectable({
  providedIn: 'root'
})
export class ProjectApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/projects`;

  initiateProject(request: InitiateProjectRequest): Observable<CommandResult> {
    return this.http.post(this.baseUrl, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  rebrandProject(id: string, request: RebrandProjectRequest): Observable<CommandResult> {
    return this.http.put(`${this.baseUrl}/${id}/rebrand`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  refineScope(id: string, request: RefineScopeRequest): Observable<CommandResult> {
    return this.http.put(`${this.baseUrl}/${id}/scope`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  completeProject(id: string, request: CompleteProjectRequest): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${id}/complete`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  reactivateProject(id: string, request: ReactivateProjectRequest): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${id}/reactivate`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  addTeamMember(id: string, request: AddTeamMemberRequest): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${id}/team`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  removeTeamMember(id: string, memberId: string): Observable<CommandResult> {
    return this.http.delete(`${this.baseUrl}/${id}/team/${memberId}`).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  reorderWorkItem(projectId: string, request: ReorderWorkItemRequest): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${projectId}/reorder-workitem`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  getProject(id: string): Observable<ProjectDto> {
    return this.http.get(`${this.baseUrl}/${id}`).pipe(
      map(response => ProjectDtoSchema.parse(response))
    );
  }

  listProjects(): Observable<ProjectDto[]> {
    return this.http.get(this.baseUrl).pipe(
      map(response => {
        if (!Array.isArray(response)) {
          return [];
        }
        return response.map(item => ProjectDtoSchema.parse(item));
      })
    );
  }
}
