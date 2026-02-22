using ErikLieben.FA.ES;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Events.UserProfile;
using TaskFlow.Domain.Specifications.UserProfile;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// Extension methods for UserProfileFactory
/// </summary>
public partial interface IUserProfileFactory
{
    /// <summary>
    /// Creates a new user profile with the specified details.
    /// This is a convenience method that combines CreateAsync and CreateProfile in one operation.
    /// </summary>
    Task<(Result result, UserProfile? userProfile)> CreateProfileAsync(
        string displayName,
        string email,
        string jobRole,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null);
}

/// <summary>
/// Extension implementation for UserProfileFactory
/// </summary>
public partial class UserProfileFactory
{
    /// <summary>
    /// Creates a new user profile with the specified details.
    /// This is a convenience method that combines CreateAsync and CreateProfile in one operation.
    /// </summary>
    public async Task<(Result result, UserProfile? userProfile)> CreateProfileAsync(
        string displayName,
        string email,
        string jobRole,
        VersionToken? createdByUser = null,
        DateTime? createdAt = null)
    {
        var userId = UserProfileId.New();

        var userIdValidation = Result<UserProfileId>.Success(userId)
            .ValidateWith(id => !string.IsNullOrWhiteSpace(id.Value), "User ID is required", "UserId");

        var displayNameValidation = Result<string>.Success(displayName)
            .ValidateWith<string, ValidDisplayNameSpecification>("Display name must be between 2 and 100 characters", nameof(displayName));

        var emailValidation = Result<string>.Success(email)
            .ValidateWith(e => !string.IsNullOrWhiteSpace(e), "Email is required", nameof(email))
            .ValidateWith<string, ValidEmailSpecification>("Email must be a valid email address", nameof(email));

        var jobRoleValidation = Result<string>.Success(jobRole)
            .ValidateWith(j => !string.IsNullOrWhiteSpace(j), "Job role is required", nameof(jobRole));

        if (userIdValidation.IsFailure || displayNameValidation.IsFailure || emailValidation.IsFailure || jobRoleValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (userIdValidation.IsFailure) errors.AddRange(userIdValidation.Errors);
            if (displayNameValidation.IsFailure) errors.AddRange(displayNameValidation.Errors);
            if (emailValidation.IsFailure) errors.AddRange(emailValidation.Errors);
            if (jobRoleValidation.IsFailure) errors.AddRange(jobRoleValidation.Errors);
            return (Result.Failure(errors.ToArray()), null);
        }

        // Create the UserProfile aggregate
        var timestamp = createdAt ?? DateTime.UtcNow;
        var userProfile = await CreateAsync(userId, new UserProfileCreated(
                userId.Value,
                displayName,
                email,
                jobRole,
                timestamp),
            new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = createdByUser });

        return (Result.Success(), userProfile);
    }
}
