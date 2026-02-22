using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Api.DTOs;

// Command DTOs (Requests)

public record InitiateProjectRequest(
    string Name,
    string Description,
    string OwnerId);

public record RebrandProjectRequest(
    string NewName);

public record RefineScopeRequest(
    string NewDescription);

public record CompleteProjectSuccessfullyRequest(
    string Summary);

public record CancelProjectRequest(
    string Reason);

public record FailProjectRequest(
    string Reason);

public record DeliverProjectRequest(
    string DeliveryNotes);

public record SuspendProjectRequest(
    string Reason);

public record MergeProjectRequest(
    string TargetProjectId,
    string Reason);

public record ReactivateProjectRequest(
    string Rationale);

public record AddTeamMemberRequest(
    string MemberId,
    string Role);

public record ReorderWorkItemRequest(
    string WorkItemId,
   WorkItemStatus Status,
    int NewPosition);

// Response DTOs

public record ProjectDto(
    string ProjectId,
    string Name,
    string Description,
    string OwnerId,
    bool IsCompleted,
    Dictionary<string, string> TeamMembers,
    int Version);

public record ProjectListDto(
    string ProjectId,
    string Name,
    string OwnerId,
    bool IsCompleted,
    int TeamMemberCount);

public record CommandResult(
    bool Success,
    string? Message = null,
    string? AggregateId = null);
