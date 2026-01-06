using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Work item was moved backward from Completed to Planned with a reason
/// </summary>
[EventName("WorkItem.MovedBackFromCompletedToPlanned")]
public record MovedBackFromCompletedToPlanned(
    string Reason,
    string MovedBy,
    DateTime MovedAt);
