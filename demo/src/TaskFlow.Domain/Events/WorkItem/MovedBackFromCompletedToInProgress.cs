using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Work item was moved backward from Completed to InProgress with a reason
/// </summary>
[EventName("WorkItem.MovedBackFromCompletedToInProgress")]
public record MovedBackFromCompletedToInProgress(
    string Reason,
    string MovedBy,
    DateTime MovedAt);
