using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Sprint;

/// <summary>
/// A work item was added to the sprint backlog
/// </summary>
[EventName("Sprint.WorkItemAdded")]
public record WorkItemAddedToSprint(
    string WorkItemId,
    string AddedBy,
    DateTime AddedAt);
