using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Epic;

/// <summary>
/// A new epic was created to group related projects together
/// </summary>
[EventName("Epic.Created")]
public record EpicCreated(
    string Name,
    string Description,
    string OwnerId,
    DateTime TargetCompletionDate,
    DateTime CreatedAt);
