using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Target completion date was set
/// </summary>
[EventName("WorkItem.DeadlineEstablished")]
public record DeadlineEstablished(
    DateTime Deadline,
    string EstablishedBy,
    DateTime EstablishedAt);
