using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Project identity and name were changed
/// </summary>
[EventName("Project.Rebranded")]
public record ProjectRebranded(
    string FormerName,
    string NewName,
    string RebrandedBy,
    DateTime RebrandedAt);
