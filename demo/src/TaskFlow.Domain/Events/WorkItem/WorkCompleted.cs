using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Work was finished successfully
/// </summary>
[EventName("WorkItem.WorkCompleted")]
public record WorkCompleted(
    string Outcome,
    string CompletedBy,
    DateTime CompletedAt);
