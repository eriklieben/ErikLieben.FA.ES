using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Previously completed/cancelled/failed/suspended project was reopened
/// </summary>
[EventName("Project.Reactivated")]
public record ProjectReactivated(
    string Rationale,
    string ReactivatedBy,
    DateTime ReactivatedAt);
