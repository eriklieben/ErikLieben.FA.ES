using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Actions;
using TaskFlow.Domain.Events.Release;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.Release;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// Release aggregate - represents a versioned release of software.
/// Stored in S3 to demonstrate the S3 storage provider.
/// </summary>
[Aggregate]
[EventStreamType("s3", "s3")]
public partial class Release : Aggregate
{
    public Release(IEventStream stream) : base(stream)
    {
        stream.RegisterAction(new PublishProjectionUpdateAction());
    }

    /// <summary>
    /// Initializes a stream with all Release event registrations (for AOT-compatible scenarios).
    /// Use this when you need a stream without creating the aggregate.
    /// </summary>
    public static void InitializeStream(IEventStream stream)
    {
        _ = new Release(stream);
    }

    public string? Name { get; private set; }
    public string? Version { get; private set; }
    public ProjectId? ProjectId { get; private set; }
    public ReleaseStatus Status { get; private set; } = ReleaseStatus.Draft;
    public List<string> Notes { get; } = new();
    public DateTime? DeployedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public UserProfileId? CreatedBy { get; private set; }
    public ObjectMetadata<ReleaseId>? Metadata { get; private set; }

    // Command: Create a new release
    public async Task<Result> Create(
        string name,
        string version,
        ProjectId projectId,
        UserProfileId createdBy,
        VersionToken? createdByUser = null,
        DateTime? occurredAt = null)
    {
        var nameValidation = Result<string>.Success(name)
            .ValidateWith(n => !string.IsNullOrWhiteSpace(n) && n.Length >= 1 && n.Length <= 200,
                "Release name must be between 1 and 200 characters", nameof(Name));

        var versionValidation = Result<string>.Success(version)
            .ValidateWith(v => !string.IsNullOrWhiteSpace(v) && v.Length >= 1 && v.Length <= 50,
                "Version must be between 1 and 50 characters", nameof(Version));

        var projectValidation = Result<ProjectId>.Success(projectId)
            .ValidateWith(id => id != null && id.Value != Guid.Empty,
                "Project ID is required", nameof(ProjectId));

        if (nameValidation.IsFailure || versionValidation.IsFailure || projectValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (nameValidation.IsFailure) errors.AddRange(nameValidation.Errors);
            if (versionValidation.IsFailure) errors.AddRange(versionValidation.Errors);
            if (projectValidation.IsFailure) errors.AddRange(projectValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ReleaseCreated(
                name,
                version,
                projectId!.Value.ToString(),
                createdBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = createdByUser })));

        return Result.Success();
    }

    // Command: Add a note to the release
    public async Task<Result> AddNote(
        string note,
        UserProfileId addedBy,
        VersionToken? addedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Release>.Success(this)
            .ValidateWith(r => r.Name != null,
                "Release must be created before adding notes", "Status");

        var noteValidation = Result<string>.Success(note)
            .ValidateWith(n => !string.IsNullOrWhiteSpace(n),
                "Note cannot be empty", nameof(note));

        if (validationResult.IsFailure || noteValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (validationResult.IsFailure) errors.AddRange(validationResult.Errors);
            if (noteValidation.IsFailure) errors.AddRange(noteValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ReleaseNoteAdded(
                note,
                addedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = addedByUser })));

        return Result.Success();
    }

    // Command: Stage the release for deployment
    public async Task<Result> Stage(
        UserProfileId stagedBy,
        VersionToken? stagedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Release>.Success(this)
            .ValidateWith(r => r.Status == ReleaseStatus.Draft,
                "Release can only be staged when it is in Draft status", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ReleaseStaged(
                stagedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = stagedByUser })));

        return Result.Success();
    }

    // Command: Deploy the release
    public async Task<Result> Deploy(
        UserProfileId deployedBy,
        VersionToken? deployedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Release>.Success(this)
            .ValidateWith(r => r.Status == ReleaseStatus.Staged,
                "Release can only be deployed when it is in Staged status", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ReleaseDeployed(
                deployedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = deployedByUser })));

        return Result.Success();
    }

    // Command: Complete the release
    public async Task<Result> Complete(
        UserProfileId completedBy,
        VersionToken? completedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Release>.Success(this)
            .ValidateWith(r => r.Status == ReleaseStatus.Deployed,
                "Release can only be completed when it is in Deployed status", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ReleaseCompleted(
                completedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = completedByUser })));

        return Result.Success();
    }

    // Command: Roll back the release
    public async Task<Result> Rollback(
        string reason,
        UserProfileId rolledBackBy,
        VersionToken? rolledBackByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Release>.Success(this)
            .ValidateWith(r => r.Status == ReleaseStatus.Staged || r.Status == ReleaseStatus.Deployed,
                "Release can only be rolled back when it is in Staged or Deployed status", "Status");

        var reasonValidation = Result<string>.Success(reason)
            .ValidateWith(r => !string.IsNullOrWhiteSpace(r),
                "Rollback reason is required", nameof(reason));

        if (validationResult.IsFailure || reasonValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (validationResult.IsFailure) errors.AddRange(validationResult.Errors);
            if (reasonValidation.IsFailure) errors.AddRange(reasonValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ReleaseRolledBack(
                reason,
                rolledBackBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = rolledBackByUser })));

        return Result.Success();
    }

    // Event handlers (When methods)
    private void When(ReleaseCreated @event)
    {
        Name = @event.Name;
        Version = @event.Version;
        ProjectId = ValueObjects.ProjectId.From(@event.ProjectId);
        CreatedBy = UserProfileId.From(@event.CreatedBy);
        CreatedAt = @event.CreatedAt;
        Status = ReleaseStatus.Draft;
    }

    private void When(ReleaseNoteAdded @event)
    {
        Notes.Add(@event.Note);
    }

    [When<ReleaseStaged>]
    private void WhenReleaseStaged()
    {
        Status = ReleaseStatus.Staged;
    }

    private void When(ReleaseDeployed @event)
    {
        Status = ReleaseStatus.Deployed;
        DeployedAt = @event.DeployedAt;
    }

    private void When(ReleaseCompleted @event)
    {
        Status = ReleaseStatus.Completed;
        CompletedAt = @event.CompletedAt;
    }

    [When<ReleaseRolledBack>]
    private void WhenReleaseRolledBack()
    {
        Status = ReleaseStatus.RolledBack;
    }

    private void PostWhen(IObjectDocument document, IEvent @event)
    {
        Metadata = ObjectMetadata<ReleaseId>.From(document, @event, ReleaseId.From(document.ObjectId));
    }
}
