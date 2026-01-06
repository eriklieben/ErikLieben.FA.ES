using TaskFlow.Domain.Events;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Api.DTOs;

// Command DTOs (Requests)

public record PlanWorkItemRequest(
    string ProjectId,
    string Title,
    string Description,
    WorkItemPriority Priority);

public record AssignResponsibilityRequest(
    string MemberId);

public record CompleteWorkRequest(
    string Outcome);

public record ReviveWorkItemRequest(
    string Rationale);

public record ReprioritizeRequest(
    WorkItemPriority NewPriority,
    string Rationale);

public record ReestimateEffortRequest(
    int EstimatedHours);

public record RefineRequirementsRequest(
    string NewDescription);

public record ProvideFeedbackRequest(
    string Content);

public record RelocateWorkItemRequest(
    string NewProjectId,
    string Rationale);

public record RetagRequest(
    string[] Tags);

public record EstablishDeadlineRequest(
    DateTime Deadline);

public record MoveBackRequest(
    string Reason);

public record MarkDragAccidentalRequest(
    WorkItemStatus FromStatus,
    WorkItemStatus ToStatus);

// Response DTOs

public record WorkItemDto(
    string WorkItemId,
    string ProjectId,
    string Title,
    string Description,
    WorkItemPriority Priority,
    WorkItemStatus Status,
    string? AssignedTo,
    DateTime? Deadline,
    int? EstimatedHours,
    string[] Tags,
    int CommentCount,
    int Version);

public record WorkItemListDto(
    string WorkItemId,
    string ProjectId,
    string Title,
    WorkItemPriority Priority,
    WorkItemStatus Status,
    string? AssignedTo,
    DateTime? Deadline);

public record WorkItemCommentDto(
    string FeedbackId,
    string Content,
    string ProvidedBy,
    DateTime ProvidedAt);
