using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Project was cancelled before completion
/// </summary>
[EventName("Project.Cancelled")]
public record ProjectCancelled(
    string Reason,
    string CancelledBy,
    DateTime CancelledAt);
