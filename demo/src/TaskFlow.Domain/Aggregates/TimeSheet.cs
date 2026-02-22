using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Actions;
using TaskFlow.Domain.Events.TimeSheet;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.TimeSheet;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// TimeSheet aggregate - tracks time entries for a user on a project within a period.
/// Stored in Azure Append Blob Storage to demonstrate O(1) atomic appends.
/// Time logging is naturally append-heavy, making it an ideal use case for append blobs.
/// </summary>
[Aggregate]
[EventStreamType("appendblob", "appendblob")]
public partial class TimeSheet : Aggregate
{
    public TimeSheet(IEventStream stream) : base(stream)
    {
        stream.RegisterAction(new PublishProjectionUpdateAction());
    }

    /// <summary>
    /// Initializes a stream with all TimeSheet event registrations (for AOT-compatible scenarios).
    /// </summary>
    public static void InitializeStream(IEventStream stream)
    {
        _ = new TimeSheet(stream);
    }

    public UserProfileId? UserId { get; private set; }
    public ProjectId? ProjectId { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public TimeSheetStatus Status { get; private set; } = TimeSheetStatus.Open;
    public List<TimeEntry> Entries { get; } = new();
    public decimal TotalHours { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public ObjectMetadata<TimeSheetId>? Metadata { get; private set; }

    // Command: Open a new timesheet
    public async Task<Result> Open(
        UserProfileId userId,
        ProjectId projectId,
        DateTime periodStart,
        DateTime periodEnd,
        VersionToken? originatedFromUser = null,
        DateTime? occurredAt = null)
    {
        var userValidation = Result<UserProfileId>.Success(userId)
            .ValidateWith(id => id != null && !string.IsNullOrWhiteSpace(id.Value),
                "User ID is required", nameof(UserId));

        var projectValidation = Result<ProjectId>.Success(projectId)
            .ValidateWith(id => id != null && id.Value != Guid.Empty,
                "Project ID is required", nameof(ProjectId));

        var periodValidation = Result<(DateTime start, DateTime end)>.Success((periodStart, periodEnd))
            .ValidateWith(p => p.end > p.start,
                "Period end must be after period start", "Period");

        if (userValidation.IsFailure || projectValidation.IsFailure || periodValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (userValidation.IsFailure) errors.AddRange(userValidation.Errors);
            if (projectValidation.IsFailure) errors.AddRange(projectValidation.Errors);
            if (periodValidation.IsFailure) errors.AddRange(periodValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new TimeSheetOpened(
                userId.Value,
                projectId!.Value.ToString(),
                periodStart,
                periodEnd,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = originatedFromUser })));

        return Result.Success();
    }

    // Command: Log time against a work item
    public async Task<Result> LogTime(
        WorkItemId workItemId,
        decimal hours,
        string description,
        DateTime date,
        VersionToken? originatedFromUser = null,
        DateTime? occurredAt = null)
    {
        var statusValidation = Result<TimeSheet>.Success(this)
            .ValidateWith(ts => ts.Status == TimeSheetStatus.Open || ts.Status == TimeSheetStatus.Rejected,
                "Time can only be logged on open or rejected timesheets", "Status");

        var hoursValidation = Result<decimal>.Success(hours)
            .ValidateWith(h => h > 0 && h <= 24,
                "Hours must be between 0 and 24", nameof(hours));

        var dateValidation = Result<DateTime>.Success(date)
            .ValidateWith(d => d.Date >= PeriodStart.Date && d.Date <= PeriodEnd.Date,
                "Date must be within the timesheet period", nameof(date));

        if (statusValidation.IsFailure || hoursValidation.IsFailure || dateValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (statusValidation.IsFailure) errors.AddRange(statusValidation.Errors);
            if (hoursValidation.IsFailure) errors.AddRange(hoursValidation.Errors);
            if (dateValidation.IsFailure) errors.AddRange(dateValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var entryId = Guid.NewGuid().ToString();
        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new TimeLogged(
                entryId,
                workItemId.Value.ToString(),
                hours,
                description ?? string.Empty,
                date,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = originatedFromUser })));

        return Result.Success();
    }

    // Command: Adjust a time entry
    public async Task<Result> AdjustTime(
        string entryId,
        decimal newHours,
        string newDescription,
        string reason,
        VersionToken? originatedFromUser = null,
        DateTime? occurredAt = null)
    {
        var statusValidation = Result<TimeSheet>.Success(this)
            .ValidateWith(ts => ts.Status == TimeSheetStatus.Open || ts.Status == TimeSheetStatus.Rejected,
                "Time can only be adjusted on open or rejected timesheets", "Status");

        var entryValidation = Result<string>.Success(entryId)
            .ValidateWith(id => Entries.Any(e => e.EntryId == id && !e.Removed),
                "Time entry not found", nameof(entryId));

        var hoursValidation = Result<decimal>.Success(newHours)
            .ValidateWith(h => h > 0 && h <= 24,
                "Hours must be between 0 and 24", nameof(newHours));

        if (statusValidation.IsFailure || entryValidation.IsFailure || hoursValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (statusValidation.IsFailure) errors.AddRange(statusValidation.Errors);
            if (entryValidation.IsFailure) errors.AddRange(entryValidation.Errors);
            if (hoursValidation.IsFailure) errors.AddRange(hoursValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new TimeAdjusted(
                entryId,
                newHours,
                newDescription ?? string.Empty,
                reason ?? string.Empty,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = originatedFromUser })));

        return Result.Success();
    }

    // Command: Remove a time entry
    public async Task<Result> RemoveEntry(
        string entryId,
        string reason,
        VersionToken? originatedFromUser = null,
        DateTime? occurredAt = null)
    {
        var statusValidation = Result<TimeSheet>.Success(this)
            .ValidateWith(ts => ts.Status == TimeSheetStatus.Open || ts.Status == TimeSheetStatus.Rejected,
                "Entries can only be removed from open or rejected timesheets", "Status");

        var entryValidation = Result<string>.Success(entryId)
            .ValidateWith(id => Entries.Any(e => e.EntryId == id && !e.Removed),
                "Time entry not found", nameof(entryId));

        if (statusValidation.IsFailure || entryValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (statusValidation.IsFailure) errors.AddRange(statusValidation.Errors);
            if (entryValidation.IsFailure) errors.AddRange(entryValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new TimeEntryRemoved(
                entryId,
                reason ?? string.Empty,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = originatedFromUser })));

        return Result.Success();
    }

    // Command: Submit timesheet for approval
    public async Task<Result> Submit(
        UserProfileId submittedBy,
        VersionToken? originatedFromUser = null,
        DateTime? occurredAt = null)
    {
        var statusValidation = Result<TimeSheet>.Success(this)
            .ValidateWith(ts => ts.Status == TimeSheetStatus.Open || ts.Status == TimeSheetStatus.Rejected,
                "Timesheet can only be submitted when open or rejected", "Status");

        var entriesValidation = Result<TimeSheet>.Success(this)
            .ValidateWith(ts => ts.Entries.Any(e => !e.Removed),
                "Timesheet must have at least one active time entry to submit", "Entries");

        if (statusValidation.IsFailure || entriesValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (statusValidation.IsFailure) errors.AddRange(statusValidation.Errors);
            if (entriesValidation.IsFailure) errors.AddRange(entriesValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new TimeSheetSubmitted(
                submittedBy.Value,
                TotalHours,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = originatedFromUser })));

        return Result.Success();
    }

    // Command: Approve timesheet
    public async Task<Result> Approve(
        UserProfileId approvedBy,
        VersionToken? originatedFromUser = null,
        DateTime? occurredAt = null)
    {
        var statusValidation = Result<TimeSheet>.Success(this)
            .ValidateWith(ts => ts.Status == TimeSheetStatus.Submitted,
                "Timesheet can only be approved when submitted", "Status");

        if (statusValidation.IsFailure)
            return statusValidation.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new TimeSheetApproved(
                approvedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = originatedFromUser })));

        return Result.Success();
    }

    // Command: Reject timesheet
    public async Task<Result> Reject(
        UserProfileId rejectedBy,
        string reason,
        VersionToken? originatedFromUser = null,
        DateTime? occurredAt = null)
    {
        var statusValidation = Result<TimeSheet>.Success(this)
            .ValidateWith(ts => ts.Status == TimeSheetStatus.Submitted,
                "Timesheet can only be rejected when submitted", "Status");

        var reasonValidation = Result<string>.Success(reason)
            .ValidateWith(r => !string.IsNullOrWhiteSpace(r),
                "Rejection reason is required", nameof(reason));

        if (statusValidation.IsFailure || reasonValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (statusValidation.IsFailure) errors.AddRange(statusValidation.Errors);
            if (reasonValidation.IsFailure) errors.AddRange(reasonValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new TimeSheetRejected(
                rejectedBy.Value,
                reason,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = originatedFromUser })));

        return Result.Success();
    }

    // Event handlers
    private void When(TimeSheetOpened @event)
    {
        UserId = UserProfileId.From(@event.UserId);
        ProjectId = ValueObjects.ProjectId.From(@event.ProjectId);
        PeriodStart = @event.PeriodStart;
        PeriodEnd = @event.PeriodEnd;
        CreatedAt = @event.OpenedAt;
        Status = TimeSheetStatus.Open;
    }

    private void When(TimeLogged @event)
    {
        Entries.Add(new TimeEntry
        {
            EntryId = @event.EntryId,
            WorkItemId = @event.WorkItemId,
            Hours = @event.Hours,
            Description = @event.Description,
            Date = @event.Date,
            LoggedAt = @event.LoggedAt
        });
        TotalHours += @event.Hours;
    }

    private void When(TimeAdjusted @event)
    {
        var entry = Entries.FirstOrDefault(e => e.EntryId == @event.EntryId);
        if (entry != null)
        {
            TotalHours -= entry.Hours;
            entry.Hours = @event.NewHours;
            entry.Description = @event.NewDescription;
            TotalHours += @event.NewHours;
        }
    }

    private void When(TimeEntryRemoved @event)
    {
        var entry = Entries.FirstOrDefault(e => e.EntryId == @event.EntryId);
        if (entry != null)
        {
            entry.Removed = true;
            TotalHours -= entry.Hours;
        }
    }

    private void When(TimeSheetSubmitted @event)
    {
        Status = TimeSheetStatus.Submitted;
    }

    [When<TimeSheetApproved>]
    private void WhenTimeSheetApproved()
    {
        Status = TimeSheetStatus.Approved;
    }

    private void When(TimeSheetRejected @event)
    {
        Status = TimeSheetStatus.Rejected;
    }

    private void PostWhen(IObjectDocument document, IEvent @event)
    {
        Metadata = ObjectMetadata<TimeSheetId>.From(document, @event, TimeSheetId.From(document.ObjectId));
    }
}

/// <summary>
/// Represents a single time entry within a timesheet
/// </summary>
public class TimeEntry
{
    public string EntryId { get; set; } = string.Empty;
    public string WorkItemId { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime LoggedAt { get; set; }
    public bool Removed { get; set; }
}
