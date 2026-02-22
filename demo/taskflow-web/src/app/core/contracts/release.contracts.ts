export type ReleaseStatus = 'Draft' | 'Staged' | 'Deployed' | 'Completed' | 'RolledBack';

export interface ReleaseListDto {
  releaseId: string;
  name: string;
  version: string;
  projectId: string;
  status: ReleaseStatus;
  createdAt: string;
}

export interface ReleaseDto {
  releaseId: string;
  name: string;
  version: string;
  projectId: string;
  status: ReleaseStatus;
  createdBy: string;
  createdAt: string;
  stagedBy: string | null;
  stagedAt: string | null;
  deployedBy: string | null;
  deployedAt: string | null;
  completedBy: string | null;
  completedAt: string | null;
  rolledBackBy: string | null;
  rolledBackAt: string | null;
  rollbackReason: string | null;
}

export interface ReleaseStatisticsDto {
  totalReleases: number;
  draftCount: number;
  stagedCount: number;
  deployedCount: number;
  completedCount: number;
  rolledBackCount: number;
  completionRate: number;
  rollbackRate: number;
}
