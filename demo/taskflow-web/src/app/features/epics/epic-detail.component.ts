import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { MatExpansionModule } from '@angular/material/expansion';
import { EpicApiService } from '../../core/services/epic-api.service';
import { EpicDto, EpicPriority } from '../../core/contracts/epic.contracts';

@Component({
  selector: 'app-epic-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatButtonModule,
    MatListModule,
    MatDividerModule,
    MatExpansionModule
  ],
  templateUrl: './epic-detail.component.html',
  styleUrl: './epic-detail.component.css'
})
export class EpicDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly epicApi = inject(EpicApiService);

  readonly isLoading = signal(true);
  readonly isLoadingEvents = signal(false);
  readonly error = signal<string | null>(null);
  readonly epic = signal<EpicDto | null>(null);
  readonly events = signal<any[]>([]);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadEpic(id);
    }
  }

  loadEpic(id: string): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.epicApi.getById(id).subscribe({
      next: (epic) => {
        this.epic.set(epic);
        this.isLoading.set(false);
        this.loadEvents(id);
      },
      error: (err) => {
        console.error('Failed to load epic:', err);
        this.error.set('Failed to load epic details.');
        this.isLoading.set(false);
      }
    });
  }

  loadEvents(id: string): void {
    this.isLoadingEvents.set(true);

    this.epicApi.getEvents(id).subscribe({
      next: (result) => {
        this.events.set(result.events || []);
        this.isLoadingEvents.set(false);
      },
      error: (err) => {
        console.error('Failed to load epic events:', err);
        this.isLoadingEvents.set(false);
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

  formatJson(data: any): string {
    return JSON.stringify(data, null, 2);
  }
}
