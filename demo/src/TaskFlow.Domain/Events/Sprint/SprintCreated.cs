using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Sprint;

/// <summary>
/// A new sprint was created
/// </summary>
[EventName("Sprint.Created")]
public record SprintCreated(
    string Name,
    string ProjectId,
    DateTime StartDate,
    DateTime EndDate,
    string? Goal,
    string CreatedBy,
    DateTime CreatedAt);
