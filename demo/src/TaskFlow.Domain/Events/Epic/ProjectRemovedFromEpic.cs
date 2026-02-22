using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Epic;

/// <summary>
/// A project was removed from this epic
/// </summary>
[EventName("Epic.ProjectRemoved")]
public record ProjectRemovedFromEpic(
    string ProjectId,
    string RemovedBy,
    DateTime RemovedAt);
