using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Sprint;

/// <summary>
/// A sprint's start or end dates were changed
/// </summary>
[EventName("Sprint.DatesChanged")]
public record SprintDatesChanged(
    DateTime PreviousStartDate,
    DateTime PreviousEndDate,
    DateTime NewStartDate,
    DateTime NewEndDate,
    string ChangedBy,
    DateTime ChangedAt,
    string? Reason);
