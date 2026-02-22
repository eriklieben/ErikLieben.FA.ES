using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.TimeSheet;

/// <summary>
/// A time entry was removed from the timesheet
/// </summary>
[EventName("TimeSheet.EntryRemoved")]
public record TimeEntryRemoved(
    string EntryId,
    string Reason,
    DateTime RemovedAt);
