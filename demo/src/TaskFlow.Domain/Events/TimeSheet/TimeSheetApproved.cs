using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.TimeSheet;

/// <summary>
/// The timesheet was approved by a manager
/// </summary>
[EventName("TimeSheet.Approved")]
public record TimeSheetApproved(
    string ApprovedBy,
    DateTime ApprovedAt);
