export interface EpicDto {
  epicId: string;
  name: string;
  description: string;
  ownerId: string;
  targetCompletionDate: string | null;
  createdAt: string;
  isCompleted: boolean;
  priority: EpicPriority;
  projectIds: string[];
  version: number;
}

export interface EpicListDto {
  epicId: string;
  name: string;
  ownerId: string;
  isCompleted: boolean;
  priority: EpicPriority;
  projectCount: number;
  targetCompletionDate: string | null;
}

export type EpicPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export interface CreateEpicRequest {
  name: string;
  description: string;
  ownerId: string;
  targetCompletionDate: string;
}

export interface CommandResult {
  success: boolean;
  message?: string;
  aggregateId?: string;
}
