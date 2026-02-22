using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Sprint;

/// <summary>
/// A sprint was started/activated
/// </summary>
[EventName("Sprint.Started")]
public record SprintStarted(
    string StartedBy,
    DateTime StartedAt);
