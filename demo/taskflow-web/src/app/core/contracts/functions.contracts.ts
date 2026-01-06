import { z } from 'zod';

// WorkItem response from Azure Functions
export const FunctionsWorkItemResponseSchema = z.object({
  id: z.string(),
  title: z.string().nullable(),
  description: z.string().nullable(),
  status: z.string(),
  priority: z.string(),
  projectId: z.string().nullable(),
  assignedTo: z.string().nullable(),
  deadline: z.string().nullable(),
  estimatedHours: z.number().nullable(),
  tags: z.array(z.string()),
  commentsCount: z.number()
});

export type FunctionsWorkItemResponse = z.infer<typeof FunctionsWorkItemResponseSchema>;

// Kanban Board response
export const KanbanBoardResponseSchema = z.object({
  projectCount: z.number(),
  projects: z.array(z.object({
    projectId: z.string(),
    projectName: z.string()
  })).nullable(),
  checkpointFingerprint: z.string().nullable()
});

export type KanbanBoardResponse = z.infer<typeof KanbanBoardResponseSchema>;

// Active Work Items response
export const ActiveWorkItemsResponseSchema = z.object({
  activeCount: z.number(),
  items: z.array(z.object({
    id: z.string(),
    title: z.string(),
    status: z.string(),
    assignedTo: z.string().nullable()
  })).nullable(),
  checkpointFingerprint: z.string().nullable()
});

export type ActiveWorkItemsResponse = z.infer<typeof ActiveWorkItemsResponseSchema>;

// User Profiles response
export const UserProfilesResponseSchema = z.object({
  totalUsers: z.number(),
  destinationCount: z.number(),
  checkpointFingerprint: z.string().nullable()
});

export type UserProfilesResponse = z.infer<typeof UserProfilesResponseSchema>;

// Command result for assign
export const FunctionsCommandResultSchema = z.object({
  success: z.boolean(),
  message: z.string(),
  workItemId: z.string().optional()
});

export type FunctionsCommandResult = z.infer<typeof FunctionsCommandResultSchema>;

// Request types
export interface AssignWorkItemFunctionsRequest {
  memberId: string;
}

export interface CreateWorkItemFunctionsRequest {
  title: string;
  description: string;
  projectId: string;
  priority: string;
}
