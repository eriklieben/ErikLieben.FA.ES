using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Epic;

/// <summary>
/// Epic was completed successfully
/// </summary>
[EventName("Epic.Completed")]
public record EpicCompleted(
    string Summary,
    string CompletedBy,
    DateTime CompletedAt);
