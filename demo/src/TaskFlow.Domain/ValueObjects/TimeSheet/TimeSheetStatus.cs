namespace TaskFlow.Domain.ValueObjects.TimeSheet;

/// <summary>
/// Represents the status of a timesheet
/// </summary>
public enum TimeSheetStatus
{
    /// <summary>Timesheet is open for time entries</summary>
    Open,

    /// <summary>Timesheet has been submitted for approval</summary>
    Submitted,

    /// <summary>Timesheet has been approved</summary>
    Approved,

    /// <summary>Timesheet was rejected and returned for correction</summary>
    Rejected
}
