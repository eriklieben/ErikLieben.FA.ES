using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Previously set due date was cleared
/// </summary>
[EventName("WorkItem.DeadlineRemoved")]
public record DeadlineRemoved(
    string RemovedBy,
    DateTime RemovedAt);
