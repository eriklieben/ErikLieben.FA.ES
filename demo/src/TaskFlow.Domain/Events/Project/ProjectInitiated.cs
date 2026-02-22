using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// A new project was established with goals and ownership
/// </summary>
[EventName("Project.Initiated")]
public record ProjectInitiated(
    string Name,
    string Description,
    string OwnerId,
    DateTime InitiatedAt);
