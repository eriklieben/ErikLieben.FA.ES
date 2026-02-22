using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events.TimeSheet;
using TaskFlow.Domain.ValueObjects.TimeSheet;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Projection that provides dashboard metrics and summaries for all timesheets.
/// Demonstrates projecting events from Append Blob-backed aggregates.
/// </summary>
[ProjectionWithExternalCheckpoint]
[BlobJsonProjection("projections", Connection = "BlobStorage")]
public partial class TimeSheetDashboard : Projection
{
    /// <summary>
    /// Dictionary of all timesheets indexed by their ID
    /// </summary>
    public Dictionary<string, TimeSheetSummary> TimeSheets { get; } = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(TimeSheetOpened @event, string timeSheetId)
    {
        TimeSheets[timeSheetId] = new TimeSheetSummary
        {
            TimeSheetId = timeSheetId,
            UserId = @event.UserId,
            ProjectId = @event.ProjectId,
            PeriodStart = @event.PeriodStart,
            PeriodEnd = @event.PeriodEnd,
            Status = TimeSheetStatus.Open,
            CreatedAt = @event.OpenedAt
        };
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(TimeLogged @event, string timeSheetId)
    {
        if (TimeSheets.TryGetValue(timeSheetId, out var sheet))
        {
            sheet.EntryCount++;
            sheet.TotalHours += @event.Hours;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(TimeAdjusted @event, string timeSheetId)
    {
        // Total hours are recalculated from entries; for the dashboard we just track the latest total
        // The aggregate maintains the accurate total; the projection approximates.
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(TimeEntryRemoved @event, string timeSheetId)
    {
        if (TimeSheets.TryGetValue(timeSheetId, out var sheet))
        {
            sheet.EntryCount = Math.Max(0, sheet.EntryCount - 1);
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(TimeSheetSubmitted @event, string timeSheetId)
    {
        if (TimeSheets.TryGetValue(timeSheetId, out var sheet))
        {
            sheet.Status = TimeSheetStatus.Submitted;
            sheet.TotalHours = @event.TotalHours;
            sheet.SubmittedAt = @event.SubmittedAt;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(TimeSheetApproved @event, string timeSheetId)
    {
        if (TimeSheets.TryGetValue(timeSheetId, out var sheet))
        {
            sheet.Status = TimeSheetStatus.Approved;
            sheet.ApprovedBy = @event.ApprovedBy;
            sheet.ApprovedAt = @event.ApprovedAt;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(TimeSheetRejected @event, string timeSheetId)
    {
        if (TimeSheets.TryGetValue(timeSheetId, out var sheet))
        {
            sheet.Status = TimeSheetStatus.Rejected;
            sheet.RejectionReason = @event.Reason;
        }
    }

    /// <summary>
    /// Get all timesheets as a list
    /// </summary>
    public IEnumerable<TimeSheetSummary> GetAllTimeSheets()
    {
        return TimeSheets.Values.OrderByDescending(ts => ts.CreatedAt);
    }

    /// <summary>
    /// Get timesheets for a specific user
    /// </summary>
    public IEnumerable<TimeSheetSummary> GetByUser(string userId)
    {
        return TimeSheets.Values
            .Where(ts => ts.UserId == userId)
            .OrderByDescending(ts => ts.PeriodStart);
    }

    /// <summary>
    /// Get timesheets for a specific project
    /// </summary>
    public IEnumerable<TimeSheetSummary> GetByProject(string projectId)
    {
        return TimeSheets.Values
            .Where(ts => ts.ProjectId == projectId)
            .OrderByDescending(ts => ts.PeriodStart);
    }

    /// <summary>
    /// Get timesheets pending approval
    /// </summary>
    public IEnumerable<TimeSheetSummary> GetPendingApproval()
    {
        return TimeSheets.Values
            .Where(ts => ts.Status == TimeSheetStatus.Submitted)
            .OrderBy(ts => ts.SubmittedAt);
    }

    /// <summary>
    /// Get timesheet by ID
    /// </summary>
    public TimeSheetSummary? GetById(string timeSheetId)
    {
        return TimeSheets.TryGetValue(timeSheetId, out var sheet) ? sheet : null;
    }

    /// <summary>
    /// Get timesheet statistics
    /// </summary>
    public TimeSheetStatistics GetStatistics()
    {
        var sheets = TimeSheets.Values.ToList();
        return new TimeSheetStatistics
        {
            TotalTimeSheets = sheets.Count,
            OpenCount = sheets.Count(ts => ts.Status == TimeSheetStatus.Open),
            SubmittedCount = sheets.Count(ts => ts.Status == TimeSheetStatus.Submitted),
            ApprovedCount = sheets.Count(ts => ts.Status == TimeSheetStatus.Approved),
            RejectedCount = sheets.Count(ts => ts.Status == TimeSheetStatus.Rejected),
            TotalHoursLogged = sheets.Sum(ts => ts.TotalHours),
            TotalApprovedHours = sheets.Where(ts => ts.Status == TimeSheetStatus.Approved).Sum(ts => ts.TotalHours)
        };
    }
}

/// <summary>
/// Summary information for a timesheet
/// </summary>
public class TimeSheetSummary
{
    public string TimeSheetId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public TimeSheetStatus Status { get; set; } = TimeSheetStatus.Open;
    public int EntryCount { get; set; }
    public decimal TotalHours { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Timesheet statistics across all timesheets
/// </summary>
public class TimeSheetStatistics
{
    public int TotalTimeSheets { get; set; }
    public int OpenCount { get; set; }
    public int SubmittedCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public decimal TotalHoursLogged { get; set; }
    public decimal TotalApprovedHours { get; set; }
}
