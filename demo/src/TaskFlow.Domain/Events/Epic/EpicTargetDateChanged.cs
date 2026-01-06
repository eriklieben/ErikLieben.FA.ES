using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Epic;

/// <summary>
/// Epic target completion date was changed
/// </summary>
[EventName("Epic.TargetDateChanged")]
public record EpicTargetDateChanged(
    DateTime OldTargetDate,
    DateTime NewTargetDate,
    string ChangedBy,
    DateTime ChangedAt);
