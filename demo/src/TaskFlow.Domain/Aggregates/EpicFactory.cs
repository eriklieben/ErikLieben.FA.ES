using ErikLieben.FA.ES;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Events.Epic;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// Extension methods for EpicFactory
/// </summary>
public partial interface IEpicFactory
{
    /// <summary>
    /// Creates a new epic with the specified details.
    /// This is a convenience method that combines CreateAsync and CreateEpic in one operation.
    /// </summary>
    Task<(Result result, Epic? epic)> CreateEpicAsync(
        string name,
        string description,
        UserProfileId ownerId,
        DateTime targetCompletionDate,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null);

    /// <summary>
    /// Creates a new epic with the specified ID and details.
    /// Use this for seeding demo data with known IDs.
    /// </summary>
    Task<(Result result, Epic? epic)> CreateEpicWithIdAsync(
        EpicId epicId,
        string name,
        string description,
        UserProfileId ownerId,
        DateTime targetCompletionDate,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null);
}

/// <summary>
/// Extension implementation for EpicFactory
/// </summary>
public partial class EpicFactory
{
    /// <summary>
    /// Creates a new epic with the specified details.
    /// This is a convenience method that combines CreateAsync and CreateEpic in one operation.
    /// </summary>
    public async Task<(Result result, Epic? epic)> CreateEpicAsync(
        string name,
        string description,
        UserProfileId ownerId,
        DateTime targetCompletionDate,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null)
    {
        var epicId = EpicId.New();
        return await CreateEpicWithIdAsync(epicId, name, description, ownerId, targetCompletionDate, createdByUser, createdAt);
    }

    /// <summary>
    /// Creates a new epic with the specified ID and details.
    /// Use this for seeding demo data with known IDs.
    /// </summary>
    public async Task<(Result result, Epic? epic)> CreateEpicWithIdAsync(
        EpicId epicId,
        string name,
        string description,
        UserProfileId ownerId,
        DateTime targetCompletionDate,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null)
    {
        var epicIdValidation = Result<EpicId>.Success(epicId)
            .ValidateWith(id => id != null && id.Value != Guid.Empty, "Epic ID is required", "EpicId");

        var nameValidation = Result<string>.Success(name)
            .ValidateWith(n => !string.IsNullOrWhiteSpace(n) && n.Length >= 3 && n.Length <= 100,
                "Epic name must be between 3 and 100 characters", nameof(name));

        var ownerValidation = Result<UserProfileId>.Success(ownerId)
            .ValidateWith(id => id != null && !string.IsNullOrWhiteSpace(id.Value),
                "Owner ID is required", nameof(ownerId));

        if (epicIdValidation.IsFailure || nameValidation.IsFailure || ownerValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (epicIdValidation.IsFailure) errors.AddRange(epicIdValidation.Errors);
            if (nameValidation.IsFailure) errors.AddRange(nameValidation.Errors);
            if (ownerValidation.IsFailure) errors.AddRange(ownerValidation.Errors);
            return (Result.Failure(errors.ToArray()), null);
        }

        // Create the Epic aggregate
        var timestamp = createdAt ?? DateTime.UtcNow;
        var epic = await CreateAsync(epicId, new EpicCreated(
                name,
                description ?? string.Empty,
                ownerId.Value,
                targetCompletionDate,
                timestamp),
            new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = createdByUser });

        return (Result.Success(), epic);
    }
}
