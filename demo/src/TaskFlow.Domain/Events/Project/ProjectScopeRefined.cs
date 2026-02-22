using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Project description and objectives were revised
/// </summary>
[EventName("Project.ScopeRefined")]
public record ProjectScopeRefined(
    string NewDescription,
    string RefinedBy,
    DateTime RefinedAt);
