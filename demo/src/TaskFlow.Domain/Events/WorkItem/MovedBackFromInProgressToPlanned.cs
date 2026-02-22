using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Work item was moved backward from InProgress to Planned with a reason
/// </summary>
[EventName("WorkItem.MovedBackFromInProgressToPlanned")]
public record MovedBackFromInProgressToPlanned(
    string Reason,
    string MovedBy,
    DateTime MovedAt);
