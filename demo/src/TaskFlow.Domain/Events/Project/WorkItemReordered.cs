using ErikLieben.FA.ES.Attributes;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Work item was reordered within its status column on the kanban board
/// </summary>
[EventName("Project.WorkItemReordered")]
public record WorkItemReordered(
    string WorkItemId,
    WorkItemStatus Status,
    int NewPosition,
    string ReorderedBy,
    DateTime ReorderedAt);
