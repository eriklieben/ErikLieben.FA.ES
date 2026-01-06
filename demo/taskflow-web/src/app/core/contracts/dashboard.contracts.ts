import { z } from 'zod';
import { WorkItemPrioritySchema, WorkItemStatusSchema } from './project.contracts';

// Dashboard/Query Response Schemas
export const ProjectMetricsSchema = z.object({
  projectId: z.string(),
  name: z.string(),
  ownerId: z.string(),
  isCompleted: z.boolean(),
  initiatedAt: z.string().datetime(),
  completedAt: z.string().datetime().nullable(),
  teamMemberCount: z.number(),
  workItemMetrics: z.object({
    total: z.number(),
    planned: z.number(),
    inProgress: z.number(),
    completed: z.number(),
    completionPercentage: z.number(),
    inProgressPercentage: z.number(),
  }),
  priorityBreakdown: z.object({
    low: z.number(),
    medium: z.number(),
    high: z.number(),
    critical: z.number(),
  }),
});
export type ProjectMetrics = z.infer<typeof ProjectMetricsSchema>;

export const ProjectSummarySchema = z.object({
  projectId: z.string(),
  name: z.string(),
  ownerId: z.string(),
  isCompleted: z.boolean(),
  initiatedAt: z.string().datetime(),
  completedAt: z.string().datetime().nullable().optional(),
  teamMemberCount: z.number(),
  metrics: z.object({
    totalWorkItems: z.number(),
    plannedWorkItems: z.number(),
    inProgressWorkItems: z.number(),
    completedWorkItems: z.number(),
    completionPercentage: z.number(),
    inProgressPercentage: z.number(),
    priorityBreakdown: z.object({
      low: z.number(),
      medium: z.number(),
      high: z.number(),
      critical: z.number(),
    }),
  }),
});
export type ProjectSummary = z.infer<typeof ProjectSummarySchema>;

export const ActiveWorkItemSchema = z.object({
  workItemId: z.string(),
  projectId: z.string(),
  title: z.string(),
  priority: WorkItemPrioritySchema,
  status: WorkItemStatusSchema,
  assignedTo: z.string().nullable(),
  deadline: z.string().datetime().nullable(),
});
export type ActiveWorkItem = z.infer<typeof ActiveWorkItemSchema>;

export const OverdueWorkItemSchema = z.object({
  workItemId: z.string(),
  projectId: z.string(),
  title: z.string(),
  priority: WorkItemPrioritySchema,
  status: WorkItemStatusSchema,
  assignedTo: z.string().nullable(),
  deadline: z.string().datetime().nullable(),
  daysOverdue: z.number(),
});
export type OverdueWorkItem = z.infer<typeof OverdueWorkItemSchema>;

export const ProjectAvailableLanguagesSchema = z.object({
  projectId: z.string(),
  availableLanguages: z.array(z.string()),
  defaultLanguage: z.string(),
});
export type ProjectAvailableLanguages = z.infer<typeof ProjectAvailableLanguagesSchema>;

export const KanbanWorkItemSchema = z.object({
  workItemId: z.string(),
  title: z.string(),
  status: z.string(),
  assignedTo: z.string().nullable(),
});
export type KanbanWorkItem = z.infer<typeof KanbanWorkItemSchema>;

export const ProjectKanbanByLanguageSchema = z.object({
  projectId: z.string(),
  languageCode: z.string(),
  workItems: z.array(KanbanWorkItemSchema),
});
export type ProjectKanbanByLanguage = z.infer<typeof ProjectKanbanByLanguageSchema>;
