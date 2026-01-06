using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Epic;

/// <summary>
/// Epic name was changed
/// </summary>
[EventName("Epic.Renamed")]
public record EpicRenamed(
    string FormerName,
    string NewName,
    string RenamedBy,
    DateTime RenamedAt);
