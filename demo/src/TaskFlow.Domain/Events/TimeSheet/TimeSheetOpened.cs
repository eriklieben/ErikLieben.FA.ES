using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.TimeSheet;

/// <summary>
/// A new timesheet was opened for a user and period
/// </summary>
[EventName("TimeSheet.Opened")]
public record TimeSheetOpened(
    string UserId,
    string ProjectId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime OpenedAt);
