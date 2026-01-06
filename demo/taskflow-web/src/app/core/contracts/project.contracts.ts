import { z } from 'zod';

// Enums
export const WorkItemPrioritySchema = z.enum(['Low', 'Medium', 'High', 'Critical']);
export type WorkItemPriority = z.infer<typeof WorkItemPrioritySchema>;

export const WorkItemStatusSchema = z.enum(['Planned', 'InProgress', 'Completed']);
export type WorkItemStatus = z.infer<typeof WorkItemStatusSchema>;

// Project Request Schemas
export const InitiateProjectRequestSchema = z.object({
  name: z.string().min(3).max(100),
  description: z.string(),
  ownerId: z.string(),
});
export type InitiateProjectRequest = z.infer<typeof InitiateProjectRequestSchema>;

export const RebrandProjectRequestSchema = z.object({
  newName: z.string().min(3).max(100),
});
export type RebrandProjectRequest = z.infer<typeof RebrandProjectRequestSchema>;

export const RefineScopeRequestSchema = z.object({
  newDescription: z.string(),
});
export type RefineScopeRequest = z.infer<typeof RefineScopeRequestSchema>;

export const CompleteProjectRequestSchema = z.object({
  outcome: z.string(),
});
export type CompleteProjectRequest = z.infer<typeof CompleteProjectRequestSchema>;

export const ReactivateProjectRequestSchema = z.object({
  rationale: z.string(),
});
export type ReactivateProjectRequest = z.infer<typeof ReactivateProjectRequestSchema>;

export const AddTeamMemberRequestSchema = z.object({
  memberId: z.string(),
  role: z.string(),
});
export type AddTeamMemberRequest = z.infer<typeof AddTeamMemberRequestSchema>;

export const ReorderWorkItemRequestSchema = z.object({
  workItemId: z.string(),
  status: WorkItemStatusSchema,
  newPosition: z.number().int().min(0),
});
export type ReorderWorkItemRequest = z.infer<typeof ReorderWorkItemRequestSchema>;

// Project Response Schemas
export const ProjectDtoSchema = z.object({
  projectId: z.string(),
  name: z.string(),
  description: z.string(),
  ownerId: z.string(),
  isCompleted: z.boolean(),
  teamMembers: z.record(z.string()),
  version: z.number(),
});
export type ProjectDto = z.infer<typeof ProjectDtoSchema>;

export const ProjectListDtoSchema = z.object({
  projectId: z.string(),
  name: z.string(),
  ownerId: z.string(),
  isCompleted: z.boolean(),
  teamMemberCount: z.number(),
});
export type ProjectListDto = z.infer<typeof ProjectListDtoSchema>;

export const CommandResultSchema = z.object({
  success: z.boolean(),
  message: z.string().nullable().optional(),
  aggregateId: z.string().nullable().optional(),
});
export type CommandResult = z.infer<typeof CommandResultSchema>;
