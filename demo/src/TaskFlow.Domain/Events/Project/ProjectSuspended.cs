using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Project was suspended/put on hold
/// </summary>
[EventName("Project.Suspended")]
public record ProjectSuspended(
    string Reason,
    string SuspendedBy,
    DateTime SuspendedAt);
