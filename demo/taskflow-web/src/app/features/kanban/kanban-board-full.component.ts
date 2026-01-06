import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CdkDragDrop, moveItemInArray, transferArrayItem, DragDropModule } from '@angular/cdk/drag-drop';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatDialog } from '@angular/material/dialog';
import { FormsModule } from '@angular/forms';
import { DashboardApiService } from '../../core/services/dashboard-api.service';
import { WorkItemApiService } from '../../core/services/workitem-api.service';
import { ProjectApiService } from '../../core/services/project-api.service';
import { SignalRService } from '../../core/services/signalr.service';
import { MoveBackDialogComponent, type MoveBackDialogResult } from './move-back-dialog.component';
import type { ActiveWorkItem, ProjectAvailableLanguages } from '../../core/contracts/dashboard.contracts';
import type { ProjectSummary } from '../../core/contracts/dashboard.contracts';
import { forkJoin } from 'rxjs';

interface KanbanColumn {
  id: string;
  title: string;
  status: string;
  items: ActiveWorkItem[];
}

@Component({
  selector: 'app-kanban-board-full',
  imports: [
    CommonModule,
    DragDropModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatFormFieldModule,
    MatSelectModule,
    FormsModule
  ],
  templateUrl: './kanban-board-full.component.html',
  styleUrl: './kanban-board-full.component.css'
})
export class KanbanBoardFullComponent implements OnInit {
  private readonly dashboardApi = inject(DashboardApiService);
  private readonly workItemApi = inject(WorkItemApiService);
  private readonly projectApi = inject(ProjectApiService);
  private readonly signalrService = inject(SignalRService);
  private readonly dialog = inject(MatDialog);

  readonly projects = signal<ProjectSummary[]>([]);
  readonly columns = signal<KanbanColumn[]>([
    { id: 'planned', title: 'Planned', status: 'Planned', items: [] },
    { id: 'inprogress', title: 'In Progress', status: 'InProgress', items: [] },
    { id: 'completed', title: 'Completed', status: 'Completed', items: [] }
  ]);
  readonly availableLanguages = signal<string[]>(['en-US']);

  selectedProject = '';
  selectedLanguage = 'en-US';
  private readonly STORAGE_KEY = 'kanban-selected-project';
  private readonly LANGUAGE_STORAGE_KEY = 'kanban-selected-language';

  ngOnInit() {
    this.loadProjects();
    this.subscribeToUpdates();
  }

  private loadProjects() {
    this.dashboardApi.getAllProjects().subscribe({
      next: (projects: ProjectSummary[]) => {
        this.projects.set(projects);

        // Try to restore last selected project from localStorage
        const savedProjectId = localStorage.getItem(this.STORAGE_KEY);

        if (savedProjectId && projects.some(p => p.projectId === savedProjectId)) {
          // Saved project exists, select it
          this.selectedProject = savedProjectId;
        } else if (projects.length > 0) {
          // No saved project or it doesn't exist anymore, select first project
          this.selectedProject = projects[0].projectId;
          localStorage.setItem(this.STORAGE_KEY, this.selectedProject);
        }

        // Load available languages and work items for the selected project
        if (this.selectedProject) {
          this.dashboardApi.getProjectAvailableLanguages(this.selectedProject).subscribe({
            next: (result) => {
              this.availableLanguages.set(result.availableLanguages);
              // Restore saved language if available for this project
              const savedLanguage = localStorage.getItem(this.LANGUAGE_STORAGE_KEY);
              if (savedLanguage && result.availableLanguages.includes(savedLanguage)) {
                this.selectedLanguage = savedLanguage;
              } else {
                this.selectedLanguage = 'en-US';
              }
              // Load work items with the appropriate language
              this.loadWorkItemsWithLanguage();
            },
            error: () => {
              // Fallback to default language
              this.availableLanguages.set(['en-US']);
              this.selectedLanguage = 'en-US';
              this.loadWorkItems();
            }
          });
        }
      },
      error: (err: Error) => console.error('Failed to load projects:', err)
    });
  }

  loadWorkItems() {
    if (!this.selectedProject) {
      // Clear columns if no project selected
      this.organizeItemsByStatus([], {});
      return;
    }

    // Load both work items and their order
    this.dashboardApi.getActiveWorkItemsByProject(this.selectedProject).subscribe({
      next: (items: ActiveWorkItem[]) => {
        // Fetch the kanban order from the projection
        this.dashboardApi.getProjectKanbanOrder(this.selectedProject).subscribe({
          next: (order) => {
            this.organizeItemsByStatus(items, order);
          },
          error: () => {
            // Fallback to no ordering if order fetch fails
            this.organizeItemsByStatus(items, {});
          }
        });
      },
      error: (err: Error) => console.error('Failed to load work items:', err)
    });
  }

  onProjectChange(projectId: string) {
    // Save to localStorage
    if (projectId) {
      localStorage.setItem(this.STORAGE_KEY, projectId);
    }

    // Reset language to default and load available languages
    this.selectedLanguage = 'en-US';
    this.availableLanguages.set(['en-US']);

    if (projectId) {
      this.dashboardApi.getProjectAvailableLanguages(projectId).subscribe({
        next: (result) => {
          this.availableLanguages.set(result.availableLanguages);
          // Restore saved language if available for this project
          const savedLanguage = localStorage.getItem(this.LANGUAGE_STORAGE_KEY);
          if (savedLanguage && result.availableLanguages.includes(savedLanguage)) {
            this.selectedLanguage = savedLanguage;
          }
        },
        error: (err: Error) => console.error('Failed to load available languages:', err)
      });
    }

    // Load work items for the new project
    this.loadWorkItems();
  }

  onLanguageChange(languageCode: string) {
    localStorage.setItem(this.LANGUAGE_STORAGE_KEY, languageCode);
    this.loadWorkItemsWithLanguage();
  }

  private organizeItemsByStatus(items: ActiveWorkItem[], order: any) {
    const newColumns = this.columns().map(col => {
      const columnItems = items.filter(item => item.status === col.status);

      // Apply order if available
      const orderKey = col.status === 'Planned' ? 'plannedItemsOrder' :
                       col.status === 'InProgress' ? 'inProgressItemsOrder' :
                       'completedItemsOrder';

      if (order[orderKey] && Array.isArray(order[orderKey])) {
        const orderedIds = order[orderKey];
        const orderedItems: ActiveWorkItem[] = [];
        const unorderedItems: ActiveWorkItem[] = [];

        // First, add items in the saved order
        orderedIds.forEach((id: string) => {
          const item = columnItems.find(i => i.workItemId === id);
          if (item) {
            orderedItems.push(item);
          }
        });

        // Then add any new items that weren't in the saved order
        columnItems.forEach(item => {
          if (!orderedIds.includes(item.workItemId)) {
            unorderedItems.push(item);
          }
        });

        return {
          ...col,
          items: [...orderedItems, ...unorderedItems]
        };
      }

      return {
        ...col,
        items: columnItems
      };
    });

    this.columns.set(newColumns);
  }

  private subscribeToUpdates() {
    this.signalrService.onWorkItemPlanned.subscribe(() => {
      this.loadWorkItemsWithLanguage();
    });

    this.signalrService.onWorkItemChanged.subscribe(() => {
      this.loadWorkItemsWithLanguage();
    });

    this.signalrService.onWorkCompleted.subscribe(() => {
      this.loadWorkItemsWithLanguage();
    });

    // Reload when projections are completed (idle state)
    this.signalrService.onProjectionUpdated.subscribe((event) => {
      if (event.projections.some(p => p.status === 'idle')) {
        this.loadProjects();
      }
    });
  }

  drop(event: CdkDragDrop<ActiveWorkItem[]>, column: KanbanColumn) {
    if (event.previousContainer === event.container) {
      // Reordering within the same column
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);

      // Save the new position to the backend
      const item = event.container.data[event.currentIndex];
      if (this.selectedProject) {
        this.projectApi.reorderWorkItem(this.selectedProject, {
          workItemId: item.workItemId,
          status: column.status as 'Planned' | 'InProgress' | 'Completed',
          newPosition: event.currentIndex
        }).subscribe({
          next: () => console.log('Item reordered:', item.workItemId),
          error: (err: Error) => {
            console.error('Failed to reorder item:', err);
            // Reload to revert the UI change
            this.loadWorkItems();
          }
        });
      }
    } else {
      const item = event.previousContainer.data[event.previousIndex];
      const previousStatus = item.status;
      const newStatus = column.status;

      // Check if this is a backward movement
      const isBackwardMove = this.isBackwardMovement(previousStatus, newStatus);

      if (isBackwardMove) {
        // Show dialog and handle based on user input
        const dialogRef = this.dialog.open(MoveBackDialogComponent);

        dialogRef.afterClosed().subscribe((result: MoveBackDialogResult) => {
          if (result && result.confirmed) {
            // User confirmed - proceed with backward movement
            transferArrayItem(
              event.previousContainer.data,
              event.container.data,
              event.previousIndex,
              event.currentIndex
            );

            // If marked as accidental, call accidental API
            if (result.isAccidental) {
              this.markDragAsAccidental(item, previousStatus, newStatus);
            } else {
              // Call the appropriate move back API
              this.moveBackward(item, previousStatus, newStatus, result.reason, event.currentIndex);
            }
          }
          // If cancelled, do nothing - item stays in original position
        });
      } else {
        // Forward movement - proceed normally
        transferArrayItem(
          event.previousContainer.data,
          event.container.data,
          event.previousIndex,
          event.currentIndex
        );

        // Update the item status via API (which will also set position)
        this.updateWorkItemStatus(item, column.status, event.currentIndex);
      }
    }
  }

  private isBackwardMovement(fromStatus: string, toStatus: string): boolean {
    const statusOrder: Record<string, number> = {
      'Planned': 0,
      'InProgress': 1,
      'Completed': 2
    };

    return statusOrder[toStatus] < statusOrder[fromStatus];
  }

  private moveBackward(item: ActiveWorkItem, fromStatus: string, toStatus: string, reason: string, newPosition: number) {
    // Determine which API to call based on the movement
    let apiCall;

    if (fromStatus === 'Completed' && toStatus === 'InProgress') {
      apiCall = this.workItemApi.moveBackToInProgress(item.workItemId, { reason });
    } else if (fromStatus === 'Completed' && toStatus === 'Planned') {
      apiCall = this.workItemApi.moveBackToPlannedFromCompleted(item.workItemId, { reason });
    } else if (fromStatus === 'InProgress' && toStatus === 'Planned') {
      apiCall = this.workItemApi.moveBackToPlannedFromInProgress(item.workItemId, { reason });
    } else {
      console.error('Unexpected backward movement:', fromStatus, '->', toStatus);
      this.loadWorkItems();
      return;
    }

    apiCall.subscribe({
      next: () => {
        console.log('Item moved backward:', item.workItemId);
        // Set the position
        if (this.selectedProject) {
          this.projectApi.reorderWorkItem(this.selectedProject, {
            workItemId: item.workItemId,
            status: toStatus as 'Planned' | 'InProgress' | 'Completed',
            newPosition: newPosition
          }).subscribe();
        }
      },
      error: (err: Error) => {
        console.error('Failed to move backward:', err);
        this.loadWorkItems();
      }
    });
  }

  private markDragAsAccidental(item: ActiveWorkItem, fromStatus: string, toStatus: string) {
    this.workItemApi.markDragAccidental(item.workItemId, {
      fromStatus: fromStatus as 'Planned' | 'InProgress' | 'Completed',
      toStatus: toStatus as 'Planned' | 'InProgress' | 'Completed'
    }).subscribe({
      next: () => {
        console.log('Drag marked as accidental');
        // Reload to get the correct state
        this.loadWorkItems();
      },
      error: (err: Error) => {
        console.error('Failed to mark as accidental:', err);
        this.loadWorkItems();
      }
    });
  }

  private updateWorkItemStatus(item: ActiveWorkItem, newStatus: string, newPosition: number) {
    // Map status to appropriate API call
    if (newStatus === 'InProgress') {
      this.workItemApi.commenceWork(item.workItemId).subscribe({
        next: () => {
          console.log('Work started:', item.workItemId);
          // After status change, set the position
          if (this.selectedProject) {
            this.projectApi.reorderWorkItem(this.selectedProject, {
              workItemId: item.workItemId,
              status: newStatus as 'Planned' | 'InProgress' | 'Completed',
              newPosition: newPosition
            }).subscribe();
          }
        },
        error: (err: Error) => {
          console.error('Failed to start work:', err);
          // Reload to revert the UI change
          this.loadWorkItems();
        }
      });
    } else if (newStatus === 'Completed') {
      this.workItemApi.completeWork(item.workItemId, {
        outcome: 'Completed via Kanban board'
      }).subscribe({
        next: () => {
          console.log('Work completed:', item.workItemId);
          // After status change, set the position
          if (this.selectedProject) {
            this.projectApi.reorderWorkItem(this.selectedProject, {
              workItemId: item.workItemId,
              status: newStatus as 'Planned' | 'InProgress' | 'Completed',
              newPosition: newPosition
            }).subscribe();
          }
        },
        error: (err: Error) => {
          console.error('Failed to complete work:', err);
          this.loadWorkItems();
        }
      });
    }
  }

  getConnectedLists(): string[] {
    return this.columns().map(col => col.id);
  }

  getColumnIcon(columnId: string): string {
    switch (columnId) {
      case 'planned': return 'schedule';
      case 'inprogress': return 'pending';
      case 'completed': return 'check_circle';
      default: return 'inbox';
    }
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }

  isOverdue(dateString: string): boolean {
    const deadline = new Date(dateString);
    const now = new Date();
    return deadline < now;
  }

  getLanguageDisplayName(languageCode: string): string {
    const languageNames: Record<string, string> = {
      'en-US': 'English',
      'nl-NL': 'Nederlands',
      'de-DE': 'Deutsch',
      'fr-FR': 'Français',
      'es-ES': 'Español',
    };
    return languageNames[languageCode] || languageCode;
  }

  private loadWorkItemsWithLanguage() {
    if (!this.selectedProject) {
      this.organizeItemsByStatus([], {});
      return;
    }

    // If using default language (en-US), use the regular work items endpoint
    if (this.selectedLanguage === 'en-US') {
      this.loadWorkItems();
      return;
    }

    // Load language-specific kanban data
    forkJoin({
      kanbanData: this.dashboardApi.getProjectKanbanByLanguage(this.selectedProject, this.selectedLanguage),
      order: this.dashboardApi.getProjectKanbanOrder(this.selectedProject)
    }).subscribe({
      next: ({ kanbanData, order }) => {
        // Convert kanban work items to ActiveWorkItem format for the UI
        const items: ActiveWorkItem[] = kanbanData.workItems.map(w => ({
          workItemId: w.workItemId,
          projectId: this.selectedProject,
          title: w.title,
          priority: 'Medium' as const, // Default - not available in language projection
          status: w.status as 'Planned' | 'InProgress' | 'Completed',
          assignedTo: w.assignedTo,
          deadline: null
        }));
        this.organizeItemsByStatus(items, order);
      },
      error: (err: Error) => {
        console.error('Failed to load language-specific kanban data:', err);
        // Fallback to regular work items
        this.loadWorkItems();
      }
    });
  }
}
