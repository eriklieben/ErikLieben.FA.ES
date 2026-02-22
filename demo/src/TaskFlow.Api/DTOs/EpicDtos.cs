using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Api.DTOs;

// Command DTOs (Requests)

public record CreateEpicRequest(
    string Name,
    string Description,
    string OwnerId,
    DateTime TargetCompletionDate);

public record RenameEpicRequest(
    string NewName);

public record UpdateEpicDescriptionRequest(
    string NewDescription);

public record AddProjectToEpicRequest(
    string ProjectId);

public record RemoveProjectFromEpicRequest(
    string ProjectId);

public record ChangeEpicTargetDateRequest(
    DateTime NewTargetDate);

public record ChangeEpicPriorityRequest(
    EpicPriority NewPriority);

public record CompleteEpicRequest(
    string Summary);

// Response DTOs

public record EpicDto(
    string EpicId,
    string Name,
    string Description,
    string OwnerId,
    DateTime? TargetCompletionDate,
    DateTime CreatedAt,
    bool IsCompleted,
    EpicPriority Priority,
    List<string> ProjectIds,
    int Version);

public record EpicListDto(
    string EpicId,
    string Name,
    string OwnerId,
    bool IsCompleted,
    EpicPriority Priority,
    int ProjectCount,
    DateTime? TargetCompletionDate);
