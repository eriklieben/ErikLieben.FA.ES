using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Sprint;

/// <summary>
/// A work item was removed from the sprint backlog
/// </summary>
[EventName("Sprint.WorkItemRemoved")]
public record WorkItemRemovedFromSprint(
    string WorkItemId,
    string RemovedBy,
    DateTime RemovedAt,
    string? Reason);
