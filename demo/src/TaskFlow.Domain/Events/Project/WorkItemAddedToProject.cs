using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Work item was added to the project
/// </summary>
[EventName("Project.WorkItemAddedToProject")]
public record WorkItemAddedToProject(
    string WorkItemId,
    string AddedBy,
    DateTime AddedAt);
