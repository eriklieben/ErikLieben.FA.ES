using ErikLieben.FA.ES.Attributes;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Work item drag was marked as accidental (any backward movement)
/// </summary>
[EventName("WorkItem.DragMarkedAsAccidental")]
public record DragMarkedAsAccidental(
    WorkItemStatus FromStatus,
    WorkItemStatus ToStatus,
    string MarkedBy,
    DateTime MarkedAt);
