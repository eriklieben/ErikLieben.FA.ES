using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Epic;

/// <summary>
/// A project was added to this epic
/// </summary>
[EventName("Epic.ProjectAdded")]
public record ProjectAddedToEpic(
    string ProjectId,
    string AddedBy,
    DateTime AddedAt);
