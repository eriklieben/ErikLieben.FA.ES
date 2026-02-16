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
import { ReleaseApiService } from '../../core/services/release-api.service';
import { ReleaseListDto, ReleaseStatus, ReleaseStatisticsDto } from '../../core/contracts/release.contracts';

@Component({
  selector: 'app-release-list',
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
  templateUrl: './release-list.component.html',
  styleUrl: './release-list.component.css'
})
export class ReleaseListComponent implements OnInit {
  private readonly releaseApi = inject(ReleaseApiService);

  readonly displayedColumns = ['name', 'version', 'status', 'projectId', 'createdAt', 'actions'];

  readonly isLoading = signal(true);
  readonly isSeeding = signal(false);
  readonly error = signal<string | null>(null);
  readonly releases = signal<ReleaseListDto[]>([]);
  readonly statistics = signal<ReleaseStatisticsDto | null>(null);

  ngOnInit(): void {
    this.loadReleases();
  }

  loadReleases(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.releaseApi.getAll().subscribe({
      next: (releases) => {
        this.releases.set(releases);
        this.isLoading.set(false);
        this.loadStatistics();
      },
      error: (err) => {
        console.error('Failed to load releases:', err);
        this.error.set('Failed to load releases. Try seeding demo data first.');
        this.isLoading.set(false);
      }
    });
  }

  loadStatistics(): void {
    this.releaseApi.getStatistics().subscribe({
      next: (stats) => {
        this.statistics.set(stats);
      },
      error: (err) => {
        console.error('Failed to load release statistics:', err);
      }
    });
  }

  seedDemoReleases(): void {
    this.isSeeding.set(true);
    this.error.set(null);

    this.releaseApi.seedDemoReleases().subscribe({
      next: (result) => {
        console.log('Demo releases seeded:', result);
        this.isSeeding.set(false);
        this.loadReleases();
      },
      error: (err) => {
        console.error('Failed to seed demo releases:', err);
        this.error.set('Failed to seed demo releases. Please check the API.');
        this.isSeeding.set(false);
      }
    });
  }

  getStatusColor(status: ReleaseStatus): string {
    switch (status) {
      case 'Draft': return '';
      case 'Staged': return 'primary';
      case 'Deployed': return 'accent';
      case 'Completed': return 'accent';
      case 'RolledBack': return 'warn';
      default: return '';
    }
  }

  getStatusIcon(status: ReleaseStatus): string {
    switch (status) {
      case 'Draft': return 'edit';
      case 'Staged': return 'pending';
      case 'Deployed': return 'rocket_launch';
      case 'Completed': return 'check_circle';
      case 'RolledBack': return 'undo';
      default: return 'help';
    }
  }

  formatDate(dateString: string | null): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric'
    });
  }
}
