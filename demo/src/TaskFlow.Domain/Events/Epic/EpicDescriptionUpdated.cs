using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Epic;

/// <summary>
/// Epic description was updated
/// </summary>
[EventName("Epic.DescriptionUpdated")]
public record EpicDescriptionUpdated(
    string NewDescription,
    string UpdatedBy,
    DateTime UpdatedAt);
