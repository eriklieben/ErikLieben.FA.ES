using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Previously completed work item was reopened for more work
/// </summary>
[EventName("WorkItem.Revived")]
public record WorkItemRevived(
    string Rationale,
    string RevivedBy,
    DateTime RevivedAt);
