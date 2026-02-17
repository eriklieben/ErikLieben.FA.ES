using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.TimeSheet;

/// <summary>
/// The timesheet was submitted for approval
/// </summary>
[EventName("TimeSheet.Submitted")]
public record TimeSheetSubmitted(
    string SubmittedBy,
    decimal TotalHours,
    DateTime SubmittedAt);
