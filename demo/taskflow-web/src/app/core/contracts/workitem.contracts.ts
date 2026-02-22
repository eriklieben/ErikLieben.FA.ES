import { z } from 'zod';
import { WorkItemPrioritySchema, WorkItemStatusSchema } from './project.contracts';

// WorkItem Request Schemas
export const PlanWorkItemRequestSchema = z.object({
  projectId: z.string(),
  title: z.string().min(5).max(200),
  description: z.string(),
  priority: WorkItemPrioritySchema,
});
export type PlanWorkItemRequest = z.infer<typeof PlanWorkItemRequestSchema>;

export const AssignResponsibilityRequestSchema = z.object({
  memberId: z.string(),
});
export type AssignResponsibilityRequest = z.infer<typeof AssignResponsibilityRequestSchema>;

export const CompleteWorkRequestSchema = z.object({
  outcome: z.string(),
});
export type CompleteWorkRequest = z.infer<typeof CompleteWorkRequestSchema>;

export const ReviveWorkItemRequestSchema = z.object({
  rationale: z.string(),
});
export type ReviveWorkItemRequest = z.infer<typeof ReviveWorkItemRequestSchema>;

export const ReprioritizeRequestSchema = z.object({
  newPriority: WorkItemPrioritySchema,
  rationale: z.string(),
});
export type ReprioritizeRequest = z.infer<typeof ReprioritizeRequestSchema>;

export const ReestimateEffortRequestSchema = z.object({
  estimatedHours: z.number().min(0),
});
export type ReestimateEffortRequest = z.infer<typeof ReestimateEffortRequestSchema>;

export const RefineRequirementsRequestSchema = z.object({
  newDescription: z.string(),
});
export type RefineRequirementsRequest = z.infer<typeof RefineRequirementsRequestSchema>;

export const ProvideFeedbackRequestSchema = z.object({
  content: z.string().min(1).max(2000),
});
export type ProvideFeedbackRequest = z.infer<typeof ProvideFeedbackRequestSchema>;

export const RelocateWorkItemRequestSchema = z.object({
  newProjectId: z.string(),
  rationale: z.string(),
});
export type RelocateWorkItemRequest = z.infer<typeof RelocateWorkItemRequestSchema>;

export const RetagRequestSchema = z.object({
  tags: z.array(z.string()),
});
export type RetagRequest = z.infer<typeof RetagRequestSchema>;

export const EstablishDeadlineRequestSchema = z.object({
  deadline: z.string().datetime(),
});
export type EstablishDeadlineRequest = z.infer<typeof EstablishDeadlineRequestSchema>;

export const MoveBackRequestSchema = z.object({
  reason: z.string().min(1),
});
export type MoveBackRequest = z.infer<typeof MoveBackRequestSchema>;

export const MarkDragAccidentalRequestSchema = z.object({
  fromStatus: WorkItemStatusSchema,
  toStatus: WorkItemStatusSchema,
});
export type MarkDragAccidentalRequest = z.infer<typeof MarkDragAccidentalRequestSchema>;

// WorkItem Response Schemas
export const WorkItemDtoSchema = z.object({
  workItemId: z.string(),
  projectId: z.string(),
  title: z.string(),
  description: z.string(),
  priority: WorkItemPrioritySchema,
  status: WorkItemStatusSchema,
  assignedTo: z.string().nullable(),
  deadline: z.string().datetime().nullable(),
  estimatedHours: z.number().nullable(),
  tags: z.array(z.string()),
  commentCount: z.number(),
  version: z.number(),
});
export type WorkItemDto = z.infer<typeof WorkItemDtoSchema>;

export const WorkItemListDtoSchema = z.object({
  workItemId: z.string(),
  projectId: z.string(),
  title: z.string(),
  priority: WorkItemPrioritySchema,
  status: WorkItemStatusSchema,
  assignedTo: z.string().nullable(),
  deadline: z.string().datetime().nullable(),
});
export type WorkItemListDto = z.infer<typeof WorkItemListDtoSchema>;

export const WorkItemCommentDtoSchema = z.object({
  feedbackId: z.string(),
  content: z.string(),
  providedBy: z.string(),
  providedAt: z.string().datetime(),
});
export type WorkItemCommentDto = z.infer<typeof WorkItemCommentDtoSchema>;
