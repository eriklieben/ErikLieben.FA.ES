using ErikLieben.FA.ES.Attributes;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Work item status changed in the project (moved between kanban columns)
/// </summary>
[EventName("Project.WorkItemStatusChangedInProject")]
public record WorkItemStatusChangedInProject(
    string WorkItemId,
    WorkItemStatus FromStatus,
    WorkItemStatus ToStatus,
    string ChangedBy,
    DateTime ChangedAt);
