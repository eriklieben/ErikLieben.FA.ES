import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AdminApiService } from '../../core/services/admin-api.service';
import { UpcastingDemoProject, ProjectOutcome, EventSummary } from '../../core/contracts/admin.contracts';

interface OutcomeGroup {
  outcome: ProjectOutcome;
  icon: string;
  cssClass: string;
  legacy: UpcastingDemoProject | null;
  modern: UpcastingDemoProject | null;
}

@Component({
  selector: 'app-event-upcasting',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatDividerModule,
    MatExpansionModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './event-upcasting.component.html',
  styleUrl: './event-upcasting.component.css'
})
export class EventUpcastingComponent implements OnInit {
  private readonly adminApi = inject(AdminApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly projects = signal<UpcastingDemoProject[]>([]);

  readonly outcomeGroups = computed(() => {
    const allProjects = this.projects();

    const outcomes: { outcome: ProjectOutcome; icon: string; cssClass: string }[] = [
      { outcome: 'Successful', icon: 'check_circle', cssClass: 'successful' },
      { outcome: 'Cancelled', icon: 'cancel', cssClass: 'cancelled' },
      { outcome: 'Failed', icon: 'error', cssClass: 'failed' },
      { outcome: 'Delivered', icon: 'local_shipping', cssClass: 'delivered' },
      { outcome: 'Suspended', icon: 'pause_circle', cssClass: 'suspended' },
    ];

    return outcomes.map(outcome => {
      // For legacy events, infer outcome from CompletionMessage since they all have Outcome='None'
      const projectsForOutcome = allProjects.filter(p => {
        if (!p.IsCompleted) return false;

        if (p.IsLegacyEvent) {
          // Infer outcome from completion message for legacy events
          const msg = (p.CompletionMessage || '').toLowerCase();
          if (outcome.outcome === 'Successful' && (msg.includes('successfully') || msg.includes('success'))) return true;
          if (outcome.outcome === 'Cancelled' && msg.includes('cancel')) return true;
          if (outcome.outcome === 'Failed' && msg.includes('fail')) return true;
          if (outcome.outcome === 'Delivered' && msg.includes('deliver')) return true;
          if (outcome.outcome === 'Suspended' && msg.includes('suspend')) return true;
          return false;
        } else {
          // Modern events have explicit Outcome enum
          return p.Outcome === outcome.outcome;
        }
      });

      const legacy = projectsForOutcome.find(p => p.IsLegacyEvent) || null;
      const modern = projectsForOutcome.find(p => !p.IsLegacyEvent) || null;

      return {
        ...outcome,
        legacy,
        modern
      };
    });
  });

  ngOnInit() {
    this.loadProjection();
  }

  private loadProjection() {
    this.loading.set(true);
    this.error.set(null);

    this.adminApi.getEventUpcastingDemonstration().subscribe({
      next: (data) => {
        const projectArray = Object.values(data.DemoProjects);
        this.projects.set(projectArray);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load EventUpcastingDemonstration:', err);
        this.error.set('Failed to load projection data. Please try again.');
        this.loading.set(false);
      }
    });
  }

  formatDate(date: Date): string {
    const now = new Date();
    const diffMs = now.getTime() - new Date(date).getTime();
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays === 0) {
      return 'Today';
    } else if (diffDays === 1) {
      return 'Yesterday';
    } else if (diffDays < 7) {
      return `${diffDays} days ago`;
    } else if (diffDays < 30) {
      const weeks = Math.floor(diffDays / 7);
      return `${weeks} week${weeks > 1 ? 's' : ''} ago`;
    } else if (diffDays < 365) {
      const months = Math.floor(diffDays / 30);
      return `${months} month${months > 1 ? 's' : ''} ago`;
    } else {
      return new Date(date).toLocaleDateString();
    }
  }

  isCompletionEvent(eventType: string): boolean {
    return eventType.includes('Completed') ||
           eventType.includes('Cancelled') ||
           eventType.includes('Failed') ||
           eventType.includes('Delivered') ||
           eventType.includes('Suspended');
  }

  hasGap(project: UpcastingDemoProject): boolean {
    return project.TotalEventCount > project.EventStreamSummaryStart.length + 1;
  }

  getGapCount(project: UpcastingDemoProject): number {
    return project.TotalEventCount - project.EventStreamSummaryStart.length - 1;
  }

  shouldShowLastEvent(project: UpcastingDemoProject): boolean {
    if (!project.EventStreamSummaryLast) return false;

    // Always show completion events separately for better visualization of upcasting
    if (this.isCompletionEvent(project.EventStreamSummaryLast.EventType)) {
      return true;
    }

    // Don't show last event if it's already in the start events
    const lastVersion = project.EventStreamSummaryLast.EventStreamVersion;
    return !project.EventStreamSummaryStart.some(e => e.EventStreamVersion === lastVersion);
  }

  getNonCompletionStartEvents(project: UpcastingDemoProject): EventSummary[] {
    // Filter out completion events from start summary since they'll be shown as the last event
    return project.EventStreamSummaryStart.filter(e => !this.isCompletionEvent(e.EventType));
  }

  getUpcastedEventType(project: UpcastingDemoProject, outcome: ProjectOutcome): string {
    // For legacy events, determine what they were upcasted to based on the outcome
    const outcomeToEventType: Record<ProjectOutcome, string> = {
      'None': 'Project.Completed',
      'Successful': 'ProjectOutcome.Successful',
      'Cancelled': 'ProjectOutcome.Cancelled',
      'Failed': 'ProjectOutcome.Failed',
      'Delivered': 'ProjectOutcome.Delivered',
      'Suspended': 'ProjectOutcome.Suspended'
    };
    return outcomeToEventType[outcome] || 'Project.Completed';
  }
}
