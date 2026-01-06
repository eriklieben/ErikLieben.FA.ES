import { Component, Input, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { DomainEvent } from '../../../core/contracts/admin.contracts';

interface EventSummary {
  first: DomainEvent[];
  middle: number;
  last?: DomainEvent;
  completionEventType?: string; // The upcasted event type if applicable
}

@Component({
  selector: 'app-event-stream',
  standalone: true,
  imports: [
    CommonModule,
    MatExpansionModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './event-stream.component.html',
  styleUrl: './event-stream.component.css'
})
export class EventStreamComponent implements OnInit {
  @Input({ required: true }) projectId!: string;
  @Input() completionEventType?: string; // The upcasted type, e.g. "Project.CompletedSuccessfully"

  private readonly adminApi = inject(AdminApiService);

  readonly isLoading = signal(false);
  readonly isEmpty = signal(false);
  readonly eventSummary = signal<EventSummary | null>(null);

  ngOnInit() {
    this.loadEvents();
  }

  private loadEvents() {
    this.isLoading.set(true);
    this.isEmpty.set(false);

    this.adminApi.getProjectEvents(this.projectId).subscribe({
      next: (events) => {
        if (events.length === 0) {
          this.isEmpty.set(true);
          this.isLoading.set(false);
          return;
        }

        // Sort by version to ensure correct order
        const sortedEvents = [...events].sort((a, b) => a.version - b.version);

        // Build summary: first 2, middle count, last
        const summary: EventSummary = {
          first: sortedEvents.slice(0, 2),
          middle: Math.max(0, sortedEvents.length - 3),
          last: sortedEvents[sortedEvents.length - 1],
          completionEventType: this.completionEventType
        };

        this.eventSummary.set(summary);
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error('Error loading events:', error);
        this.isEmpty.set(true);
        this.isLoading.set(false);
      }
    });
  }

  get isLegacyEvent(): boolean {
    const lastEvent = this.eventSummary()?.last;
    return lastEvent?.eventType === 'Project.Completed';
  }

  get isNewEvent(): boolean {
    const lastEvent = this.eventSummary()?.last;
    const newCompletionEvents = [
      'Project.CompletedSuccessfully',
      'Project.Cancelled',
      'Project.Failed',
      'Project.Delivered',
      'Project.Suspended'
    ];
    return lastEvent ? newCompletionEvents.includes(lastEvent.eventType) : false;
  }

  formatEventType(eventType: string): string {
    // Remove namespace prefix if present
    return eventType.split('.').slice(-2).join('.');
  }

  formatTimestamp(timestamp: string): string {
    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays === 0) return 'Today';
    if (diffDays === 1) return 'Yesterday';
    if (diffDays < 7) return `${diffDays} days ago`;
    if (diffDays < 30) return `${Math.floor(diffDays / 7)} weeks ago`;
    if (diffDays < 365) return `${Math.floor(diffDays / 30)} months ago`;
    return `${Math.floor(diffDays / 365)} years ago`;
  }
}
