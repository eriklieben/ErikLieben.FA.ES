export type SprintStatus = 'Planned' | 'Active' | 'Completed' | 'Cancelled';

export interface SprintListDto {
  sprintId: string;
  name: string;
  projectId: string;
  startDate: string;
  endDate: string;
  goal: string | null;
  status: SprintStatus;
  workItemCount: number;
  completedWorkItems: number;
  ownerId: string | null;
}

export interface SprintDto {
  sprintId: string;
  name: string;
  projectId: string;
  startDate: string;
  endDate: string;
  goal: string | null;
  status: SprintStatus;
  workItems: string[];
  ownerId: string | null;
  completionSummary: string | null;
  cancellationReason: string | null;
  createdAt: string;
  lastModified: string;
}

export interface SprintStatistics {
  totalSprints: number;
  plannedSprints: number;
  activeSprints: number;
  completedSprints: number;
  cancelledSprints: number;
  totalWorkItems: number;
  averageWorkItemsPerSprint: number;
}

export interface CreateSprintRequest {
  name: string;
  projectId: string;
  startDate: string;
  endDate: string;
  goal?: string;
}

export interface CommandResult {
  success: boolean;
  message?: string;
  errors?: string[];
}
