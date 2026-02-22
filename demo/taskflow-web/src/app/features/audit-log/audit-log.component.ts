import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../core/services/admin-api.service';
import { AuditLogEntry, ReportingIndexItem } from '../../core/contracts/admin.contracts';

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    MatExpansionModule
  ],
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss'
})
export class AuditLogComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly adminApi = inject(AdminApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly entries = signal<AuditLogEntry[]>([]);
  readonly workItemId = signal('');
  readonly storageType = signal('');
  readonly containerName = signal('');
  readonly message = signal<string | null>(null);

  // Work items list for dropdown
  readonly loadingWorkItems = signal(false);
  readonly workItemsError = signal<string | null>(null);
  readonly workItems = signal<ReportingIndexItem[]>([]);

  selectedWorkItemId = '';

  ngOnInit() {
    // Load work items for the dropdown
    this.loadWorkItems();

    // Check if we have a workItemId in the route params or query params
    const id = this.route.snapshot.paramMap.get('workItemId') ||
               this.route.snapshot.queryParamMap.get('workItemId');
    if (id) {
      this.selectedWorkItemId = id;
      this.loadAuditLog(id);
    } else {
      this.loading.set(false);
    }
  }

  loadWorkItems() {
    this.loadingWorkItems.set(true);
    this.workItemsError.set(null);

    this.adminApi.getWorkItemReportingIndex().subscribe({
      next: (response) => {
        this.workItems.set(response.items);
        this.loadingWorkItems.set(false);
      },
      error: (err) => {
        console.error('Failed to load work items:', err);
        this.workItemsError.set('Failed to load work items. Table Storage may not be configured.');
        this.loadingWorkItems.set(false);
      }
    });
  }

  onWorkItemSelected(workItemId: string) {
    if (workItemId) {
      this.loadAuditLog(workItemId);
    }
  }

  loadAuditLog(workItemId: string) {
    this.loading.set(true);
    this.error.set(null);
    this.message.set(null);

    this.adminApi.getWorkItemAuditLog(workItemId).subscribe({
      next: (response) => {
        this.workItemId.set(response.workItemId);
        this.entries.set(response.entries);
        this.storageType.set(response.storageType);
        this.containerName.set(response.containerName);
        this.message.set(response.message || null);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load WorkItemAuditLog:', err);
        this.error.set('Failed to load audit log. CosmosDB may not be configured or the work item may not exist.');
        this.loading.set(false);
      }
    });
  }

  getEventIcon(eventType: string | null): string {
    if (!eventType) return 'event';
    const type = eventType.toLowerCase();
    if (type.includes('planned') || type.includes('created')) return 'add_circle';
    if (type.includes('assigned')) return 'person_add';
    if (type.includes('relinquished')) return 'person_remove';
    if (type.includes('commenced') || type.includes('started')) return 'play_circle';
    if (type.includes('completed') || type.includes('finished')) return 'check_circle';
    if (type.includes('reprioritized')) return 'priority_high';
    if (type.includes('deadline')) return 'event';
    if (type.includes('moved')) return 'swap_horiz';
    return 'bolt';
  }

  getEventColor(eventType: string | null): string {
    if (!eventType) return '';
    const type = eventType.toLowerCase();
    if (type.includes('planned') || type.includes('created')) return 'event-create';
    if (type.includes('assigned')) return 'event-assign';
    if (type.includes('completed')) return 'event-complete';
    if (type.includes('commenced')) return 'event-progress';
    if (type.includes('reprioritized')) return 'event-priority';
    return '';
  }

  formatDate(date: Date | null): string {
    if (!date) return '-';
    return new Date(date).toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });
  }

  formatJson(obj: any): string {
    if (!obj) return '-';
    try {
      return JSON.stringify(obj, null, 2);
    } catch {
      return String(obj);
    }
  }

  getShortEventType(eventType: string | null): string {
    if (!eventType) return 'Unknown';
    // Remove "WorkItem" prefix if present
    return eventType.replace('WorkItem', '').replace(/([A-Z])/g, ' $1').trim();
  }
}
