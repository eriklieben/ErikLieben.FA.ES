using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Sprint;

/// <summary>
/// A sprint was cancelled before completion
/// </summary>
[EventName("Sprint.Cancelled")]
public record SprintCancelled(
    string CancelledBy,
    DateTime CancelledAt,
    string? Reason);
