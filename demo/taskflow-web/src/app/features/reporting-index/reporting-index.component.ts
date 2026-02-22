import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { RouterLink } from '@angular/router';
import { AdminApiService } from '../../core/services/admin-api.service';
import { ReportingIndexItem } from '../../core/contracts/admin.contracts';

@Component({
  selector: 'app-reporting-index',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatIconModule,
    MatTableModule,
    MatChipsModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatSelectModule,
    MatFormFieldModule
  ],
  templateUrl: './reporting-index.component.html',
  styleUrl: './reporting-index.component.scss'
})
export class ReportingIndexComponent implements OnInit {
  private readonly adminApi = inject(AdminApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<ReportingIndexItem[]>([]);
  readonly storageType = signal('');
  readonly tableName = signal('');

  readonly displayedColumns = [
    'projectId',
    'workItemId',
    'title',
    'status',
    'priority',
    'assignedTo',
    'lastUpdatedAt',
    'actions'
  ];

  // Unique projects for filtering
  readonly projects = computed(() => {
    const projectIds = new Set(this.items().map(i => i.partitionKey));
    return Array.from(projectIds).sort((a, b) => a.localeCompare(b));
  });

  readonly selectedProject = signal<string | null>(null);

  readonly filteredItems = computed(() => {
    const project = this.selectedProject();
    if (!project) {
      return this.items();
    }
    return this.items().filter(i => i.partitionKey === project);
  });

  // Status counts
  readonly statusCounts = computed(() => {
    const items = this.filteredItems();
    return {
      planned: items.filter(i => i.status === 'Planned').length,
      inProgress: items.filter(i => i.status === 'InProgress').length,
      completed: items.filter(i => i.status === 'Completed').length
    };
  });

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    this.error.set(null);

    this.adminApi.getWorkItemReportingIndex().subscribe({
      next: (response) => {
        this.items.set(response.items);
        this.storageType.set(response.storageType);
        this.tableName.set(response.tableName);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load WorkItemReportingIndex:', err);
        this.error.set('Failed to load reporting index data. Ensure demo data has been seeded.');
        this.loading.set(false);
      }
    });
  }

  filterByProject(projectId: string | null) {
    this.selectedProject.set(projectId);
  }

  getStatusClass(status: string | null): string {
    switch (status) {
      case 'Planned': return 'status-planned';
      case 'InProgress': return 'status-inprogress';
      case 'Completed': return 'status-completed';
      default: return '';
    }
  }

  getPriorityClass(priority: string | null): string {
    switch (priority) {
      case 'Critical': return 'priority-critical';
      case 'High': return 'priority-high';
      case 'Medium': return 'priority-medium';
      case 'Low': return 'priority-low';
      default: return '';
    }
  }

  formatDate(date: Date | null): string {
    if (!date) return '-';
    return new Date(date).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  truncate(text: string | null, length: number): string {
    if (!text) return '-';
    return text.length > length ? text.substring(0, length) + '...' : text;
  }
}
