using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Task ownership was released from team member
/// </summary>
[EventName("WorkItem.ResponsibilityRelinquished")]
public record ResponsibilityRelinquished(
    string RelinquishedBy,
    DateTime RelinquishedAt);
