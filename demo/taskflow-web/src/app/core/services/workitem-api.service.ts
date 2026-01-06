import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  WorkItemDto,
  WorkItemDtoSchema,
  WorkItemListDto,
  WorkItemListDtoSchema,
  PlanWorkItemRequest,
  AssignResponsibilityRequest,
  CompleteWorkRequest,
  ReviveWorkItemRequest,
  ReprioritizeRequest,
  ReestimateEffortRequest,
  RefineRequirementsRequest,
  ProvideFeedbackRequest,
  RelocateWorkItemRequest,
  RetagRequest,
  EstablishDeadlineRequest,
  MoveBackRequest,
  MarkDragAccidentalRequest,
} from '../contracts/workitem.contracts';
import { CommandResult, CommandResultSchema } from '../contracts/project.contracts';

@Injectable({
  providedIn: 'root'
})
export class WorkItemApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/workitems`;

  planWorkItem(request: PlanWorkItemRequest): Observable<CommandResult> {
    return this.http.post(this.baseUrl, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  assignResponsibility(id: string, request: AssignResponsibilityRequest): Observable<CommandResult> {
    return this.http.put(`${this.baseUrl}/${id}/assign`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  relinquishResponsibility(id: string): Observable<CommandResult> {
    return this.http.put(`${this.baseUrl}/${id}/unassign`, {}).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  commenceWork(id: string): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${id}/commence`, {}).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  completeWork(id: string, request: CompleteWorkRequest): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${id}/complete`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  reviveWorkItem(id: string, request: ReviveWorkItemRequest): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${id}/revive`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  reprioritize(id: string, request: ReprioritizeRequest): Observable<CommandResult> {
    return this.http.put(`${this.baseUrl}/${id}/priority`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  reestimateEffort(id: string, request: ReestimateEffortRequest): Observable<CommandResult> {
    return this.http.put(`${this.baseUrl}/${id}/estimate`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  refineRequirements(id: string, request: RefineRequirementsRequest): Observable<CommandResult> {
    return this.http.put(`${this.baseUrl}/${id}/requirements`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  provideFeedback(id: string, request: ProvideFeedbackRequest): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${id}/feedback`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  relocateWorkItem(id: string, request: RelocateWorkItemRequest): Observable<CommandResult> {
    return this.http.put(`${this.baseUrl}/${id}/relocate`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  retag(id: string, request: RetagRequest): Observable<CommandResult> {
    return this.http.put(`${this.baseUrl}/${id}/tags`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  establishDeadline(id: string, request: EstablishDeadlineRequest): Observable<CommandResult> {
    return this.http.put(`${this.baseUrl}/${id}/deadline`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  removeDeadline(id: string): Observable<CommandResult> {
    return this.http.delete(`${this.baseUrl}/${id}/deadline`).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  moveBackToInProgress(id: string, request: MoveBackRequest): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${id}/move-back-to-inprogress`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  moveBackToPlannedFromCompleted(id: string, request: MoveBackRequest): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${id}/move-back-to-planned-from-completed`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  moveBackToPlannedFromInProgress(id: string, request: MoveBackRequest): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${id}/move-back-to-planned-from-inprogress`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  markDragAccidental(id: string, request: MarkDragAccidentalRequest): Observable<CommandResult> {
    return this.http.post(`${this.baseUrl}/${id}/mark-drag-accidental`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  getWorkItem(id: string): Observable<WorkItemDto> {
    return this.http.get(`${this.baseUrl}/${id}`).pipe(
      map(response => WorkItemDtoSchema.parse(response))
    );
  }

  listWorkItems(projectId?: string): Observable<WorkItemListDto[]> {
    const params: Record<string, string> = projectId ? { projectId } : {};
    return this.http.get(this.baseUrl, { params }).pipe(
      map(response => {
        if (!Array.isArray(response)) {
          return [];
        }
        return response.map(item => WorkItemListDtoSchema.parse(item));
      })
    );
  }
}
