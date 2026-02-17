using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.TimeSheet;

/// <summary>
/// The timesheet was rejected and returned for correction
/// </summary>
[EventName("TimeSheet.Rejected")]
public record TimeSheetRejected(
    string RejectedBy,
    string Reason,
    DateTime RejectedAt);
