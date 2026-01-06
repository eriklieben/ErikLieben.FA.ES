using ErikLieben.FA.ES;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Events.Sprint;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// Extension methods for SprintFactory
/// </summary>
public partial interface ISprintFactory
{
    /// <summary>
    /// Creates a new sprint with the specified details.
    /// This is a convenience method that combines CreateAsync and CreateSprint in one operation.
    /// </summary>
    Task<(Result result, Sprint? sprint)> CreateSprintAsync(
        string name,
        ProjectId projectId,
        DateTime startDate,
        DateTime endDate,
        string? goal,
        UserProfileId createdBy,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null);

    /// <summary>
    /// Creates a new sprint with the specified ID and details.
    /// Use this for seeding demo data with known IDs.
    /// </summary>
    Task<(Result result, Sprint? sprint)> CreateSprintWithIdAsync(
        SprintId sprintId,
        string name,
        ProjectId projectId,
        DateTime startDate,
        DateTime endDate,
        string? goal,
        UserProfileId createdBy,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null);
}

/// <summary>
/// Extension implementation for SprintFactory
/// </summary>
public partial class SprintFactory
{
    /// <summary>
    /// Creates a new sprint with the specified details.
    /// This is a convenience method that combines CreateAsync and CreateSprint in one operation.
    /// </summary>
    public async Task<(Result result, Sprint? sprint)> CreateSprintAsync(
        string name,
        ProjectId projectId,
        DateTime startDate,
        DateTime endDate,
        string? goal,
        UserProfileId createdBy,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null)
    {
        var sprintId = SprintId.New();
        return await CreateSprintWithIdAsync(sprintId, name, projectId, startDate, endDate, goal, createdBy, createdByUser, createdAt);
    }

    /// <summary>
    /// Creates a new sprint with the specified ID and details.
    /// Use this for seeding demo data with known IDs.
    /// </summary>
    public async Task<(Result result, Sprint? sprint)> CreateSprintWithIdAsync(
        SprintId sprintId,
        string name,
        ProjectId projectId,
        DateTime startDate,
        DateTime endDate,
        string? goal,
        UserProfileId createdBy,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null)
    {
        var sprintIdValidation = Result<SprintId>.Success(sprintId)
            .ValidateWith(id => id != null && !string.IsNullOrWhiteSpace(id.Value), "Sprint ID is required", "SprintId");

        var nameValidation = Result<string>.Success(name)
            .ValidateWith(n => !string.IsNullOrWhiteSpace(n) && n.Length >= 3 && n.Length <= 100,
                "Sprint name must be between 3 and 100 characters", nameof(name));

        var projectValidation = Result<ProjectId>.Success(projectId)
            .ValidateWith(id => id != null && id.Value != Guid.Empty,
                "Project ID is required", nameof(projectId));

        var dateValidation = Result<(DateTime start, DateTime end)>.Success((startDate, endDate))
            .ValidateWith(d => d.end > d.start,
                "End date must be after start date", "EndDate");

        if (sprintIdValidation.IsFailure || nameValidation.IsFailure || projectValidation.IsFailure || dateValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (sprintIdValidation.IsFailure) errors.AddRange(sprintIdValidation.Errors);
            if (nameValidation.IsFailure) errors.AddRange(nameValidation.Errors);
            if (projectValidation.IsFailure) errors.AddRange(projectValidation.Errors);
            if (dateValidation.IsFailure) errors.AddRange(dateValidation.Errors);
            return (Result.Failure(errors.ToArray()), null);
        }

        // Create the Sprint aggregate
        var timestamp = createdAt ?? DateTime.UtcNow;
        var sprint = await CreateAsync(sprintId, new SprintCreated(
                name,
                projectId!.Value.ToString(),
                startDate,
                endDate,
                goal,
                createdBy.Value,
                timestamp),
            new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = createdByUser });

        return (Result.Success(), sprint);
    }
}
