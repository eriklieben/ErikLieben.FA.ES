using ErikLieben.FA.ES.Attributes;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Work item urgency level was adjusted
/// </summary>
[EventName("WorkItem.Reprioritized")]
public record WorkItemReprioritized(
    WorkItemPriority FormerPriority,
    WorkItemPriority NewPriority,
    string Rationale,
    string ReprioritizedBy,
    DateTime ReprioritizedAt);
