using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Sprint;

/// <summary>
/// A sprint was completed
/// </summary>
[EventName("Sprint.Completed")]
public record SprintCompleted(
    string CompletedBy,
    DateTime CompletedAt,
    string? Summary);
