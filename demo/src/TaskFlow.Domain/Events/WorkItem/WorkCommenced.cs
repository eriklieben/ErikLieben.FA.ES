using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Team member began working on the work item
/// </summary>
[EventName("WorkItem.WorkCommenced")]
public record WorkCommenced(
    string CommencedBy,
    DateTime CommencedAt);
