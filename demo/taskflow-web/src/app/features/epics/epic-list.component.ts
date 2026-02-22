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
import { EpicApiService } from '../../core/services/epic-api.service';
import { EpicListDto, EpicPriority } from '../../core/contracts/epic.contracts';

@Component({
  selector: 'app-epic-list',
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
  templateUrl: './epic-list.component.html',
  styleUrl: './epic-list.component.css'
})
export class EpicListComponent implements OnInit {
  private readonly epicApi = inject(EpicApiService);

  readonly displayedColumns = ['name', 'priority', 'projectCount', 'targetDate', 'status', 'actions'];

  readonly isLoading = signal(true);
  readonly isSeeding = signal(false);
  readonly error = signal<string | null>(null);
  readonly epics = signal<EpicListDto[]>([]);

  ngOnInit(): void {
    this.loadEpics();
  }

  loadEpics(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.epicApi.getAll().subscribe({
      next: (epics) => {
        this.epics.set(epics);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load epics:', err);
        this.error.set('Failed to load epics. Try seeding demo data first.');
        this.isLoading.set(false);
      }
    });
  }

  seedDemoEpics(): void {
    this.isSeeding.set(true);
    this.error.set(null);

    this.epicApi.seedDemoEpics().subscribe({
      next: (result) => {
        console.log('Demo epics seeded:', result);
        this.isSeeding.set(false);
        this.loadEpics();
      },
      error: (err) => {
        console.error('Failed to seed demo epics:', err);
        this.error.set('Failed to seed demo epics. Please check the API.');
        this.isSeeding.set(false);
      }
    });
  }

  getPriorityColor(priority: EpicPriority): string {
    switch (priority) {
      case 'Critical': return 'warn';
      case 'High': return 'accent';
      case 'Medium': return 'primary';
      case 'Low': return '';
      default: return '';
    }
  }

  getPriorityIcon(priority: EpicPriority): string {
    switch (priority) {
      case 'Critical': return 'error';
      case 'High': return 'priority_high';
      case 'Medium': return 'remove';
      case 'Low': return 'arrow_downward';
      default: return 'remove';
    }
  }

  formatDate(dateString: string | null): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }
}
