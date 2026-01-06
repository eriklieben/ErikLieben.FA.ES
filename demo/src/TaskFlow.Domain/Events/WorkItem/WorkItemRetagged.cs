using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Classification tags were modified
/// </summary>
[EventName("WorkItem.Retagged")]
public record WorkItemRetagged(
    string[] Tags,
    string RetaggedBy,
    DateTime RetaggedAt);
