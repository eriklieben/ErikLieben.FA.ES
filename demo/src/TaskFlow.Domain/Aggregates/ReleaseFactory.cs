using ErikLieben.FA.ES;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Events.Release;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.Release;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// Extension methods for ReleaseFactory
/// </summary>
public partial interface IReleaseFactory
{
    /// <summary>
    /// Creates a new release with the specified details.
    /// This is a convenience method that combines CreateAsync and initial event in one operation.
    /// </summary>
    Task<(Result result, Release? release)> CreateReleaseAsync(
        string name,
        string version,
        ProjectId projectId,
        UserProfileId createdBy,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null);

    /// <summary>
    /// Creates a new release with the specified ID and details.
    /// Use this for seeding demo data with known IDs.
    /// </summary>
    Task<(Result result, Release? release)> CreateReleaseWithIdAsync(
        ReleaseId releaseId,
        string name,
        string version,
        ProjectId projectId,
        UserProfileId createdBy,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null);
}

/// <summary>
/// Extension implementation for ReleaseFactory
/// </summary>
public partial class ReleaseFactory
{
    /// <summary>
    /// Creates a new release with the specified details.
    /// This is a convenience method that combines CreateAsync and initial event in one operation.
    /// </summary>
    public async Task<(Result result, Release? release)> CreateReleaseAsync(
        string name,
        string version,
        ProjectId projectId,
        UserProfileId createdBy,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null)
    {
        var releaseId = ReleaseId.New();
        return await CreateReleaseWithIdAsync(releaseId, name, version, projectId, createdBy, createdByUser, createdAt);
    }

    /// <summary>
    /// Creates a new release with the specified ID and details.
    /// Use this for seeding demo data with known IDs.
    /// </summary>
    public async Task<(Result result, Release? release)> CreateReleaseWithIdAsync(
        ReleaseId releaseId,
        string name,
        string version,
        ProjectId projectId,
        UserProfileId createdBy,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null)
    {
        var releaseIdValidation = Result<ReleaseId>.Success(releaseId)
            .ValidateWith(id => id != null && !string.IsNullOrWhiteSpace(id.Value), "Release ID is required", "ReleaseId");

        var nameValidation = Result<string>.Success(name)
            .ValidateWith(n => !string.IsNullOrWhiteSpace(n) && n.Length >= 1 && n.Length <= 200,
                "Release name must be between 1 and 200 characters", nameof(name));

        var versionValidation = Result<string>.Success(version)
            .ValidateWith(v => !string.IsNullOrWhiteSpace(v) && v.Length >= 1 && v.Length <= 50,
                "Version must be between 1 and 50 characters", nameof(version));

        var projectValidation = Result<ProjectId>.Success(projectId)
            .ValidateWith(id => id != null && id.Value != Guid.Empty,
                "Project ID is required", nameof(projectId));

        if (releaseIdValidation.IsFailure || nameValidation.IsFailure || versionValidation.IsFailure || projectValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (releaseIdValidation.IsFailure) errors.AddRange(releaseIdValidation.Errors);
            if (nameValidation.IsFailure) errors.AddRange(nameValidation.Errors);
            if (versionValidation.IsFailure) errors.AddRange(versionValidation.Errors);
            if (projectValidation.IsFailure) errors.AddRange(projectValidation.Errors);
            return (Result.Failure(errors.ToArray()), null);
        }

        var timestamp = createdAt ?? DateTime.UtcNow;
        var release = await CreateAsync(releaseId, new ReleaseCreated(
                name,
                version,
                projectId!.Value.ToString(),
                createdBy.Value,
                timestamp),
            new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = createdByUser });

        return (Result.Success(), release);
    }
}
