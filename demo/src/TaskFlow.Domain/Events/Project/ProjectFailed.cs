using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Project failed to meet its objectives
/// </summary>
[EventName("Project.Failed")]
public record ProjectFailed(
    string Reason,
    string FailedBy,
    DateTime FailedAt);
