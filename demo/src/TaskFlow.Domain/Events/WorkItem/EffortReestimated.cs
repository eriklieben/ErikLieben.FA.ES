using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Expected work effort was reassessed
/// </summary>
[EventName("WorkItem.EffortReestimated")]
public record EffortReestimated(
    int EstimatedHours,
    string ReestimatedBy,
    DateTime ReestimatedAt);
