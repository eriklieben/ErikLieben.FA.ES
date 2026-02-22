using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.TimeSheet;

/// <summary>
/// A time entry was adjusted (hours or description changed)
/// </summary>
[EventName("TimeSheet.TimeAdjusted")]
public record TimeAdjusted(
    string EntryId,
    decimal NewHours,
    string NewDescription,
    string Reason,
    DateTime AdjustedAt);
