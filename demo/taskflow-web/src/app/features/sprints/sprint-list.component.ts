import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { SprintApiService } from '../../core/services/sprint-api.service';
import { SprintListDto, SprintStatus, SprintStatistics } from '../../core/contracts/sprint.contracts';

@Component({
  selector: 'app-sprint-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatTableModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatButtonModule,
    MatTooltipModule
  ],
  templateUrl: './sprint-list.component.html',
  styleUrl: './sprint-list.component.css'
})
export class SprintListComponent implements OnInit {
  private readonly sprintApi = inject(SprintApiService);

  readonly displayedColumns = ['name', 'dates', 'status', 'workItems', 'goal', 'actions'];

  readonly isLoading = signal(true);
  readonly isSeeding = signal(false);
  readonly error = signal<string | null>(null);
  readonly sprints = signal<SprintListDto[]>([]);
  readonly statistics = signal<SprintStatistics | null>(null);

  ngOnInit(): void {
    this.loadSprints();
  }

  loadSprints(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.sprintApi.getAll().subscribe({
      next: (sprints) => {
        this.sprints.set(sprints);
        this.isLoading.set(false);
        this.loadStatistics();
      },
      error: (err) => {
        console.error('Failed to load sprints:', err);
        this.error.set('Failed to load sprints. Try seeding demo data first.');
        this.isLoading.set(false);
      }
    });
  }

  loadStatistics(): void {
    this.sprintApi.getStatistics().subscribe({
      next: (stats) => {
        this.statistics.set(stats);
      },
      error: (err) => {
        console.error('Failed to load sprint statistics:', err);
      }
    });
  }

  seedDemoSprints(): void {
    this.isSeeding.set(true);
    this.error.set(null);

    this.sprintApi.seedDemoSprints().subscribe({
      next: (result) => {
        console.log('Demo sprints seeded:', result);
        this.isSeeding.set(false);
        this.loadSprints();
      },
      error: (err) => {
        console.error('Failed to seed demo sprints:', err);
        this.error.set('Failed to seed demo sprints. Please check the API.');
        this.isSeeding.set(false);
      }
    });
  }

  getStatusColor(status: SprintStatus): string {
    switch (status) {
      case 'Active': return 'primary';
      case 'Completed': return 'accent';
      case 'Cancelled': return 'warn';
      case 'Planned': return '';
      default: return '';
    }
  }

  getStatusIcon(status: SprintStatus): string {
    switch (status) {
      case 'Active': return 'play_arrow';
      case 'Completed': return 'check_circle';
      case 'Cancelled': return 'cancel';
      case 'Planned': return 'schedule';
      default: return 'help';
    }
  }

  formatDate(dateString: string | null): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric'
    });
  }

  formatDateRange(startDate: string, endDate: string): string {
    return `${this.formatDate(startDate)} - ${this.formatDate(endDate)}`;
  }

  getProgressPercent(sprint: SprintListDto): number {
    if (sprint.workItemCount === 0) return 0;
    return Math.round((sprint.completedWorkItems / sprint.workItemCount) * 100);
  }
}
