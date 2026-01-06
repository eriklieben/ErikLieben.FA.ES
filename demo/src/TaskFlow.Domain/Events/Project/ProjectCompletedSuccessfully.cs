using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Project completed successfully with all objectives met
/// </summary>
[EventName("Project.CompletedSuccessfully")]
public record ProjectCompletedSuccessfully(
    string Summary,
    string CompletedBy,
    DateTime CompletedAt);
