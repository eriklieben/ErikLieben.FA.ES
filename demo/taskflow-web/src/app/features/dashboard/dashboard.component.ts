import { Component, inject, signal, OnInit, OnDestroy, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatGridListModule } from '@angular/material/grid-list';
import { MatIconModule } from '@angular/material/icon';
import { DashboardApiService } from '../../core/services/dashboard-api.service';
import { SignalRService } from '../../core/services/signalr.service';
import type { ProjectSummary, ActiveWorkItem } from '../../core/contracts/dashboard.contracts';

@Component({
  selector: 'app-dashboard',
  imports: [
    CommonModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatGridListModule,
    MatIconModule
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit, OnDestroy {
  private readonly dashboardApi = inject(DashboardApiService);
  private readonly signalrService = inject(SignalRService);

  readonly loading = signal(true);
  readonly projects = signal<ProjectSummary[]>([]);
  readonly activeWorkItems = signal<ActiveWorkItem[]>([]);

  // Project metrics
  readonly totalProjectCount = signal(0);
  readonly activeProjectCount = signal(0);
  readonly completedProjectCount = signal(0);
  readonly projectCompletionRate = signal(0);

  // Task metrics
  readonly totalWorkItemCount = signal(0);
  readonly completedTaskCount = signal(0);
  readonly taskCompletionRate = signal(0);
  readonly highPriorityCount = signal(0);
  readonly overdueCount = signal(0);

  // Track which metrics are currently animating
  readonly animatingMetrics = signal<Set<string>>(new Set());

  // Previous values to detect changes
  private previousValues = new Map<string, number>();

  // Last updated tracking
  readonly lastUpdatedTime = signal<Date | null>(null);
  readonly lastUpdatedText = signal<string>('');
  private updateIntervalId?: number;

  ngOnInit() {
    this.loadDashboardData();
    this.loadProjectionMetadata();
    this.subscribeToUpdates();
    this.startUpdateInterval();
  }

  ngOnDestroy() {
    if (this.updateIntervalId) {
      clearInterval(this.updateIntervalId);
    }
  }

  hasNoData(): boolean {
    return this.totalProjectCount() === 0 && this.totalWorkItemCount() === 0;
  }

  isMetricAnimating(metricName: string): boolean {
    return this.animatingMetrics().has(metricName);
  }

  private loadDashboardData() {
    this.loading.set(true);

    this.dashboardApi.getAllProjects().subscribe({
      next: (projects: ProjectSummary[]) => {
        this.projects.set(projects);
        this.calculateMetrics();
      },
      error: (err: Error) => console.error('Failed to load projects:', err)
    });

    this.dashboardApi.getActiveWorkItems().subscribe({
      next: (items: ActiveWorkItem[]) => {
        this.activeWorkItems.set(items);
        this.calculateMetrics();
        this.loading.set(false);
      },
      error: (err: Error) => {
        console.error('Failed to load active work items:', err);
        this.loading.set(false);
      }
    });
  }

  private calculateMetrics() {
    const projects = this.projects();
    const activeItems = this.activeWorkItems();

    const newValues = new Map<string, number>();

    if (projects.length > 0) {
      // Project metrics
      const completedProjects = projects.filter(p => p.isCompleted).length;
      const activeProjects = projects.length - completedProjects;

      newValues.set('totalProjectCount', projects.length);
      newValues.set('activeProjectCount', activeProjects);
      newValues.set('completedProjectCount', completedProjects);
      newValues.set('projectCompletionRate',
        projects.length > 0 ? Math.round((completedProjects / projects.length) * 100) : 0
      );

      // Task metrics
      let totalWorkItems = 0;
      let completedWorkItems = 0;

      projects.forEach(project => {
        totalWorkItems += project.metrics.totalWorkItems;
        completedWorkItems += project.metrics.completedWorkItems;
      });

      newValues.set('totalWorkItemCount', totalWorkItems);
      newValues.set('completedTaskCount', completedWorkItems);
      newValues.set('taskCompletionRate',
        totalWorkItems > 0 ? Math.round((completedWorkItems / totalWorkItems) * 100) : 0
      );
    }

    // Active items count
    newValues.set('activeWorkItems', activeItems.length);

    if (activeItems.length > 0) {
      const highPriority = activeItems.filter((item: ActiveWorkItem) => item.priority === 'High').length;
      newValues.set('highPriorityCount', highPriority);

      const now = new Date();
      const overdue = activeItems.filter((item: ActiveWorkItem) =>
        item.deadline && new Date(item.deadline) < now
      ).length;
      newValues.set('overdueCount', overdue);
    } else {
      newValues.set('highPriorityCount', 0);
      newValues.set('overdueCount', 0);
    }

    // Detect changes and trigger animations
    const changedMetrics = new Set<string>();
    newValues.forEach((newValue, metricName) => {
      const previousValue = this.previousValues.get(metricName);
      if (previousValue !== undefined && previousValue !== newValue) {
        changedMetrics.add(metricName);
      }
    });

    // Update the animating metrics
    if (changedMetrics.size > 0) {
      this.animatingMetrics.set(changedMetrics);
      setTimeout(() => this.animatingMetrics.set(new Set()), 600);
    }

    // Update all values
    newValues.forEach((value, metricName) => {
      this.previousValues.set(metricName, value);

      switch(metricName) {
        case 'totalProjectCount': this.totalProjectCount.set(value); break;
        case 'activeProjectCount': this.activeProjectCount.set(value); break;
        case 'completedProjectCount': this.completedProjectCount.set(value); break;
        case 'projectCompletionRate': this.projectCompletionRate.set(value); break;
        case 'totalWorkItemCount': this.totalWorkItemCount.set(value); break;
        case 'completedTaskCount': this.completedTaskCount.set(value); break;
        case 'taskCompletionRate': this.taskCompletionRate.set(value); break;
        case 'highPriorityCount': this.highPriorityCount.set(value); break;
        case 'overdueCount': this.overdueCount.set(value); break;
      }
    });
  }

  private subscribeToUpdates() {
    this.signalrService.onProjectionUpdated.subscribe((event) => {
      // Only reload data when projections are completed (idle state)
      if (event.projections.some(p => p.status === 'idle')) {
        this.loadDashboardData();
        this.loadProjectionMetadata(); // Reload metadata to get updated timestamp
      }
    });
  }

  private loadProjectionMetadata() {
    this.dashboardApi.getProjectionMetadata('projectdashboard').subscribe({
      next: (metadata) => {
        const lastModified = new Date(metadata.lastModified);
        this.lastUpdatedTime.set(lastModified);
        this.updateLastUpdatedText();
      },
      error: (err) => console.error('Failed to load projection metadata:', err)
    });
  }

  private startUpdateInterval() {
    // Update the "x seconds ago" text every second
    this.updateIntervalId = window.setInterval(() => {
      this.updateLastUpdatedText();
    }, 1000);
  }

  private updateLastUpdatedText() {
    const lastUpdated = this.lastUpdatedTime();
    if (!lastUpdated) {
      this.lastUpdatedText.set('');
      return;
    }

    const now = new Date();
    const diffMs = now.getTime() - lastUpdated.getTime();
    const diffSeconds = Math.floor(diffMs / 1000);
    const diffMinutes = Math.floor(diffSeconds / 60);
    const diffHours = Math.floor(diffMinutes / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffSeconds < 5) {
      this.lastUpdatedText.set('just now');
    } else if (diffSeconds < 60) {
      this.lastUpdatedText.set(`${diffSeconds} second${diffSeconds !== 1 ? 's' : ''} ago`);
    } else if (diffMinutes < 60) {
      this.lastUpdatedText.set(`${diffMinutes} minute${diffMinutes !== 1 ? 's' : ''} ago`);
    } else if (diffHours < 24) {
      this.lastUpdatedText.set(`${diffHours} hour${diffHours !== 1 ? 's' : ''} ago`);
    } else {
      this.lastUpdatedText.set(`${diffDays} day${diffDays !== 1 ? 's' : ''} ago`);
    }
  }
}
