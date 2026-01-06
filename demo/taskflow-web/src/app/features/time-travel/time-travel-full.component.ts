import { Component, inject, signal, ViewChild, ElementRef, AfterViewInit, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatDividerModule } from '@angular/material/divider';
import { FormsModule } from '@angular/forms';
import { DashboardApiService } from '../../core/services/dashboard-api.service';
import { AdminApiService } from '../../core/services/admin-api.service';
import { UserContextService } from '../../core/services/user-context.service';
import type { ProjectSummary } from '../../core/contracts/dashboard.contracts';
import type { DomainEvent } from '../../core/contracts/admin.contracts';

@Component({
  selector: 'app-time-travel-full',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSliderModule,
    MatExpansionModule,
    MatDividerModule,
    FormsModule
  ],
  templateUrl: './time-travel-full.component.html',
  styleUrl: './time-travel-full.component.css'
})
export class TimeTravelFullComponent implements OnInit {
  private readonly dashboardApi = inject(DashboardApiService);
  private readonly adminApi = inject(AdminApiService);
  private readonly userContext = inject(UserContextService);

  @ViewChild('eventList') eventListRef?: ElementRef<HTMLDivElement>;

  selectedAggregateType = '';
  selectedAggregateId = '';
  currentEventIndex = 0;

  readonly aggregateList = signal<{id: string, name: string}[]>([]);
  readonly events = signal<DomainEvent[]>([]);
  readonly currentState = signal<any>(null);
  readonly selectedEvent = signal<DomainEvent | null>(null);
  readonly isLoadingState = signal<boolean>(false);

  ngOnInit() {
    // Auto-select "Project" aggregate type
    this.selectedAggregateType = 'Project';
    this.loadAggregateList();
  }

  loadAggregateList() {
    if (this.selectedAggregateType === 'Project') {
      this.dashboardApi.getAllProjects().subscribe({
        next: (projects: ProjectSummary[]) => {
          const aggregates = projects
            .map(p => ({ id: p.projectId, name: p.name }))
            .sort((a, b) => a.name.localeCompare(b.name));

          this.aggregateList.set(aggregates);

          // Auto-select "Time Travel Test Project" if it exists and nothing is selected
          if (!this.selectedAggregateId) {
            const timeTravelProject = aggregates.find(p =>
              p.name === 'Time Travel Test Project' || p.name === 'Time Travel Demo Project'
            );

            if (timeTravelProject) {
              this.selectedAggregateId = timeTravelProject.id;
              // Automatically load the event stream
              this.loadEventStream();
            }
          }
        },
        error: (err: Error) => console.error('Failed to load projects:', err)
      });
    } else if (this.selectedAggregateType === 'WorkItem') {
      this.dashboardApi.getActiveWorkItems().subscribe({
        next: (workItems) => {
          this.aggregateList.set(
            workItems
              .map(w => ({ id: w.workItemId, name: w.title }))
              .sort((a, b) => a.name.localeCompare(b.name))
          );
        },
        error: (err: Error) => console.error('Failed to load work items:', err)
      });
    }
  }

  loadEventStream() {
    if (!this.selectedAggregateId) {
      return;
    }

    if (this.selectedAggregateType === 'Project') {
      this.adminApi.getProjectEvents(this.selectedAggregateId).subscribe({
        next: (events) => {
          this.events.set(events);
          this.currentEventIndex = events.length;
          this.updateStateAtVersion();
        },
        error: (err) => {
          console.error('Failed to load project events:', err);
          this.events.set([]);
        }
      });
    } else if (this.selectedAggregateType === 'WorkItem') {
      this.adminApi.getWorkItemEvents(this.selectedAggregateId).subscribe({
        next: (events) => {
          this.events.set(events);
          this.currentEventIndex = events.length;
          this.updateStateAtVersion();
        },
        error: (err) => {
          console.error('Failed to load work item events:', err);
          this.events.set([]);
        }
      });
    }
  }

  updateStateAtVersion() {
    if (this.currentEventIndex === 0) {
      this.currentState.set({ status: 'No events applied yet' });
      this.selectedEvent.set(null);
      this.isLoadingState.set(false);
      this.scrollToCurrentEvent();
      return;
    }

    if (!this.selectedAggregateId) {
      return;
    }

    // Get events up to current index
    const eventsToApply = this.events().slice(0, this.currentEventIndex);
    const selectedEvent = eventsToApply[eventsToApply.length - 1];
    this.selectedEvent.set(selectedEvent);

    // Debug log to check event data
    console.log('Selected event:', selectedEvent);
    console.log('Event data:', selectedEvent?.data);

    // Scroll to the current event
    this.scrollToCurrentEvent();

    // Use the API to get the state at the specific version
    const version = eventsToApply[eventsToApply.length - 1].version;

    // Set loading state
    this.isLoadingState.set(true);

    console.log(`Loading state at version ${version} (event index ${this.currentEventIndex})`);

    if (this.selectedAggregateType === 'Project') {
      this.adminApi.getProjectAtVersion(this.selectedAggregateId, version).subscribe({
        next: (versionState) => {
          const newState = {
            ...versionState.state,
            version: versionState.version,
            currentVersion: versionState.currentVersion,
            aggregateType: 'Project',
            aggregateId: versionState.projectId
          };
          this.currentState.set(newState);
          this.isLoadingState.set(false);
          console.log(`Loaded project state at version ${version}:`, versionState.state);
          console.log('Team members in loaded state:', versionState.state.teamMembers);
          console.log('Outcome in loaded state:', versionState.state.outcome);
          console.log('Full state object:', newState);
        },
        error: (err) => {
          console.error('Failed to get project at version:', err);
          // Fallback to local state building
          this.currentState.set(this.buildStateFromEvents(eventsToApply));
          this.isLoadingState.set(false);
        }
      });
    } else if (this.selectedAggregateType === 'WorkItem') {
      this.adminApi.getWorkItemAtVersion(this.selectedAggregateId, version).subscribe({
        next: (versionState) => {
          this.currentState.set({
            ...versionState.state,
            version: versionState.version,
            currentVersion: versionState.currentVersion,
            aggregateType: 'WorkItem',
            aggregateId: versionState.workItemId
          });
          this.isLoadingState.set(false);
          console.log(`Loaded work item state at version ${version}:`, versionState.state);
        },
        error: (err) => {
          console.error('Failed to get work item at version:', err);
          // Fallback to local state building
          this.currentState.set(this.buildStateFromEvents(eventsToApply));
          this.isLoadingState.set(false);
        }
      });
    }
  }

  private buildStateFromEvents(events: DomainEvent[]): any {
    const state: any = {
      aggregateType: this.selectedAggregateType,
      aggregateId: this.selectedAggregateId,
      version: events.length,
      appliedEvents: events.length,
      outcome: 0 // Default to None
    };

    // Apply each event to build up the state
    events.forEach(event => {
      Object.assign(state, event.data);

      // Handle derived properties based on event type
      if (event.eventType === 'ProjectCompletedSuccessfully') {
        state.outcome = 1; // Successful
        state.isCompleted = true;
      } else if (event.eventType === 'ProjectCancelled') {
        state.outcome = 2; // Cancelled
        state.isCompleted = true;
      } else if (event.eventType === 'ProjectFailed') {
        state.outcome = 3; // Failed
        state.isCompleted = true;
      } else if (event.eventType === 'ProjectDelivered') {
        state.outcome = 4; // Delivered
        state.isCompleted = true;
      } else if (event.eventType === 'ProjectSuspended') {
        state.outcome = 5; // Suspended
        state.isCompleted = true;
      } else if (event.eventType === 'ProjectMerged') {
        state.outcome = 6; // Merged
        state.isCompleted = true;
      } else if (event.eventType === 'ProjectCompleted') {
        // Legacy event - outcome stays as None
        state.isCompleted = true;
      }
    });

    return state;
  }

  getStateProperties(): {key: string, value: string}[] {
    const state = this.currentState();
    if (!state) return [];

    const properties = Object.entries(state)
      .filter(([key]) => !['aggregateType', 'aggregateId', 'teamMembers', 'workItemCounts'].includes(key))
      .map(([key, value]) => ({
        key,
        value: typeof value === 'object' ? JSON.stringify(value) : String(value)
      }));

    console.log('State properties to display:', properties);
    return properties;
  }

  getWorkItemCounts(): {planned: number, inProgress: number, completed: number, total: number} | null {
    const state = this.currentState();
    console.log('getWorkItemCounts - Full state:', state);
    console.log('getWorkItemCounts - workItemCounts property:', state?.workItemCounts);

    if (!state || !state.workItemCounts) {
      console.log('No work item counts found in state');
      return null;
    }
    return state.workItemCounts;
  }

  getTeamMembers(): {id: string, displayName: string, role: string}[] {
    const state = this.currentState();
    if (!state || !state.teamMembers) {
      console.log('No team members found in state:', state);
      return [];
    }

    // teamMembers is a record/object with userId as key and role as value
    const members = Object.entries(state.teamMembers).map(([id, role]) => ({
      id,
      displayName: this.resolveUserDisplayName(id),
      role: String(role)
    }));

    console.log('Team members at this version:', members);
    return members;
  }

  resolveUserDisplayName(userId: string): string {
    const teamMembers = this.userContext.teamMembers();
    const member = teamMembers.find(m => m.userId === userId);
    return member?.displayName ?? userId;
  }

  getOutcomeLabel(outcome: string): string {
    // ProjectOutcome enum values:
    // 0 = None, 1 = Successful, 2 = Cancelled, 3 = Failed, 4 = Delivered, 5 = Suspended, 6 = Merged
    const outcomeMap: { [key: string]: string } = {
      '0': 'None (Active)',
      '1': 'Successful',
      '2': 'Cancelled',
      '3': 'Failed',
      '4': 'Delivered',
      '5': 'Suspended',
      '6': 'Merged'
    };

    return outcomeMap[outcome] || `Unknown (${outcome})`;
  }

  formatEventType(eventType: string): string {
    return eventType.replace(/([A-Z])/g, ' $1').trim();
  }

  formatTimestamp(timestamp: string): string {
    return new Date(timestamp).toLocaleString();
  }

  getCurrentEventTime(): string {
    if (this.currentEventIndex === 0) return '';
    const event = this.events()[this.currentEventIndex - 1];
    return event ? this.formatTimestamp(event.timestamp) : '';
  }

  formatSliderLabel(value: number): string {
    return `${value}`;
  }

  jumpToEvent(eventIndex: number) {
    this.currentEventIndex = eventIndex;
    this.updateStateAtVersion();
  }

  private scrollToCurrentEvent() {
    if (!this.eventListRef) {
      return;
    }

    // Use setTimeout to ensure DOM is updated
    setTimeout(() => {
      const eventList = this.eventListRef?.nativeElement;
      if (!eventList) {
        return;
      }

      // Find the current event element
      const currentEventElement = eventList.querySelector('.event-item.current') as HTMLElement;
      if (currentEventElement) {
        // Scroll the event into view smoothly
        currentEventElement.scrollIntoView({
          behavior: 'smooth',
          block: 'nearest',
          inline: 'nearest'
        });
      }
    }, 0);
  }
}
