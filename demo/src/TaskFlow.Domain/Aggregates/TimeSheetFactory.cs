using ErikLieben.FA.ES;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Events.TimeSheet;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// Extension methods for TimeSheetFactory
/// </summary>
public partial interface ITimeSheetFactory
{
    /// <summary>
    /// Creates a new timesheet with the specified details.
    /// </summary>
    Task<(Result result, TimeSheet? timeSheet)> CreateTimeSheetAsync(
        UserProfileId userId,
        ProjectId projectId,
        DateTime periodStart,
        DateTime periodEnd,
        VersionToken? originatedFromUser = null,
        DateTime? createdAt = null);

    /// <summary>
    /// Creates a new timesheet with the specified ID and details.
    /// Use this for seeding demo data with known IDs.
    /// </summary>
    Task<(Result result, TimeSheet? timeSheet)> CreateTimeSheetWithIdAsync(
        TimeSheetId timeSheetId,
        UserProfileId userId,
        ProjectId projectId,
        DateTime periodStart,
        DateTime periodEnd,
        VersionToken? originatedFromUser = null,
        DateTime? createdAt = null);
}

/// <summary>
/// Extension implementation for TimeSheetFactory
/// </summary>
public partial class TimeSheetFactory
{
    public async Task<(Result result, TimeSheet? timeSheet)> CreateTimeSheetAsync(
        UserProfileId userId,
        ProjectId projectId,
        DateTime periodStart,
        DateTime periodEnd,
        VersionToken? originatedFromUser = null,
        DateTime? createdAt = null)
    {
        var timeSheetId = TimeSheetId.New();
        return await CreateTimeSheetWithIdAsync(timeSheetId, userId, projectId, periodStart, periodEnd, originatedFromUser, createdAt);
    }

    public async Task<(Result result, TimeSheet? timeSheet)> CreateTimeSheetWithIdAsync(
        TimeSheetId timeSheetId,
        UserProfileId userId,
        ProjectId projectId,
        DateTime periodStart,
        DateTime periodEnd,
        VersionToken? originatedFromUser = null,
        DateTime? createdAt = null)
    {
        var idValidation = Result<TimeSheetId>.Success(timeSheetId)
            .ValidateWith(id => id != null && !string.IsNullOrWhiteSpace(id.Value),
                "TimeSheet ID is required", "TimeSheetId");

        var userValidation = Result<UserProfileId>.Success(userId)
            .ValidateWith(id => id != null && !string.IsNullOrWhiteSpace(id.Value),
                "User ID is required", nameof(userId));

        var projectValidation = Result<ProjectId>.Success(projectId)
            .ValidateWith(id => id != null && id.Value != Guid.Empty,
                "Project ID is required", nameof(projectId));

        var periodValidation = Result<(DateTime start, DateTime end)>.Success((periodStart, periodEnd))
            .ValidateWith(p => p.end > p.start,
                "Period end must be after period start", "Period");

        if (idValidation.IsFailure || userValidation.IsFailure || projectValidation.IsFailure || periodValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (idValidation.IsFailure) errors.AddRange(idValidation.Errors);
            if (userValidation.IsFailure) errors.AddRange(userValidation.Errors);
            if (projectValidation.IsFailure) errors.AddRange(projectValidation.Errors);
            if (periodValidation.IsFailure) errors.AddRange(periodValidation.Errors);
            return (Result.Failure(errors.ToArray()), null);
        }

        var timestamp = createdAt ?? DateTime.UtcNow;
        var timeSheet = await CreateAsync(timeSheetId, new TimeSheetOpened(
                userId.Value,
                projectId!.Value.ToString(),
                periodStart,
                periodEnd,
                timestamp),
            new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = originatedFromUser });

        return (Result.Success(), timeSheet);
    }
}
