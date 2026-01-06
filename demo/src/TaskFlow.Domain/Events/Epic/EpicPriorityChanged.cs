using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Epic;

/// <summary>
/// Epic priority was changed
/// </summary>
[EventName("Epic.PriorityChanged")]
public record EpicPriorityChanged(
    string OldPriority,
    string NewPriority,
    string ChangedBy,
    DateTime ChangedAt);
