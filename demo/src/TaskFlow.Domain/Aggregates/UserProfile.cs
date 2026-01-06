using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Actions;
using TaskFlow.Domain.Events.UserProfile;
using TaskFlow.Domain.Specifications.UserProfile;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Aggregates;

[Aggregate]
[EventStreamBlobSettings("UserDataStore")]
public partial class UserProfile : Aggregate
{
    public UserProfile(IEventStream stream) : base(stream)
    {
        stream.RegisterAction(new PublishProjectionUpdateAction());
        stream.RegisterAction(new TagUserProfileByEmailAction());
    }

    /// <summary>
    /// Initializes a stream with all UserProfile event registrations (for AOT-compatible scenarios).
    /// Use this when you need a stream without creating the aggregate.
    /// </summary>
    public static void InitializeStream(IEventStream stream)
    {
        _ = new UserProfile(stream);
    }

    public string? DisplayName { get; private set; }
    public string? Email { get; private set; }
    public string? JobRole { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastUpdatedAt { get; private set; }
    public ObjectMetadata<UserProfileId>? Metadata { get; private set; }

    public async Task<Result> UpdateProfile(string displayName, string email, string jobRole, VersionToken? updatedByUser = null, DateTime? updatedAt = null)
    {
        var displayNameValidation = Result<string>.Success(displayName)
            .ValidateWith<string, ValidDisplayNameSpecification>("Display name must be between 2 and 100 characters", nameof(DisplayName));

        var emailValidation = Result<string>.Success(email)
            .ValidateWith(e => !string.IsNullOrWhiteSpace(e), "Email is required", nameof(Email))
            .ValidateWith<string, ValidEmailSpecification>("Email must be a valid email address", nameof(Email));

        var jobRoleValidation = Result<string>.Success(jobRole)
            .ValidateWith(j => !string.IsNullOrWhiteSpace(j), "Job role is required", nameof(JobRole));

        if (displayNameValidation.IsFailure || emailValidation.IsFailure || jobRoleValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (displayNameValidation.IsFailure) errors.AddRange(displayNameValidation.Errors);
            if (emailValidation.IsFailure) errors.AddRange(emailValidation.Errors);
            if (jobRoleValidation.IsFailure) errors.AddRange(jobRoleValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        // Check if anything actually changed
        if (DisplayName == displayName && Email == email && JobRole == jobRole)
            return Result.Success(); // No change needed

        var timestamp = updatedAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new UserProfileUpdated(
                displayName,
                email,
                jobRole,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = updatedByUser })));

        return Result.Success();
    }

    // Event handlers (When methods)
    private void When(UserProfileCreated @event)
    {
        DisplayName = @event.DisplayName;
        Email = @event.Email;
        JobRole = @event.JobRole;
        CreatedAt = @event.CreatedAt;
    }

    private void When(UserProfileUpdated @event)
    {
        DisplayName = @event.DisplayName;
        Email = @event.Email;
        JobRole = @event.JobRole;
        LastUpdatedAt = @event.UpdatedAt;
    }

    private void PostWhen(IObjectDocument document, IEvent @event)
    {
        Metadata = ObjectMetadata<UserProfileId>.From(document, @event, UserProfileId.From(document.ObjectId));
    }
}
