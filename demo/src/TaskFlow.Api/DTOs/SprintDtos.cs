using TaskFlow.Domain.ValueObjects.Sprint;

namespace TaskFlow.Api.DTOs;

// Command DTOs (Requests)

public record CreateSprintRequest(
    string Name,
    string ProjectId,
    DateTime StartDate,
    DateTime EndDate,
    string? Goal);

public record StartSprintRequest();

public record CompleteSprintRequest(
    string? Summary);

public record CancelSprintRequest(
    string? Reason);

public record UpdateSprintGoalRequest(
    string? NewGoal);

public record ChangeSprintDatesRequest(
    DateTime NewStartDate,
    DateTime NewEndDate,
    string? Reason);

public record AddWorkItemToSprintRequest(
    string WorkItemId);

public record RemoveWorkItemFromSprintRequest(
    string? Reason);

// Response DTOs

public record SprintDto(
    string SprintId,
    string Name,
    string ProjectId,
    DateTime StartDate,
    DateTime EndDate,
    string? Goal,
    SprintStatus Status,
    string CreatedBy,
    DateTime CreatedAt,
    int WorkItemCount,
    List<string> WorkItemIds,
    int DurationDays,
    int? DaysRemaining,
    bool IsOverdue);

public record SprintListDto(
    string SprintId,
    string Name,
    string ProjectId,
    DateTime StartDate,
    DateTime EndDate,
    SprintStatus Status,
    int WorkItemCount);

public record SprintStatisticsDto(
    int TotalSprints,
    int PlannedSprints,
    int ActiveSprints,
    int CompletedSprints,
    int CancelledSprints,
    int TotalWorkItems,
    double AverageWorkItemsPerSprint,
    double CompletionRate);
