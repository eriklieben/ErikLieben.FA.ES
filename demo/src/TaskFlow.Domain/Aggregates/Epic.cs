using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Actions;
using TaskFlow.Domain.Events.Epic;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// Epic aggregate - groups related projects together.
/// Stored in Azure Table Storage to demonstrate Table Storage provider.
/// </summary>
[Aggregate]
[EventStreamType("table", "table")]
public partial class Epic : Aggregate
{
    public Epic(IEventStream stream) : base(stream)
    {
        stream.RegisterAction(new PublishProjectionUpdateAction());
    }

    /// <summary>
    /// Initializes a stream with all Epic event registrations (for AOT-compatible scenarios).
    /// Use this when you need a stream without creating the aggregate.
    /// </summary>
    public static void InitializeStream(IEventStream stream)
    {
        _ = new Epic(stream);
    }

    public string? Name { get; private set; }
    public string? Description { get; private set; }
    public UserProfileId? OwnerId { get; private set; }
    public DateTime? TargetCompletionDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsCompleted { get; private set; }
    public EpicPriority Priority { get; private set; } = EpicPriority.Medium;
    public List<ProjectId> Projects { get; } = new();
    public ObjectMetadata<EpicId>? Metadata { get; private set; }

    // Command: Create a new epic
    public async Task<Result> CreateEpic(
        string name,
        string description,
        UserProfileId ownerId,
        DateTime targetCompletionDate,
        VersionToken? createdByUser = null,
        DateTime? occurredAt = null)
    {
        var nameValidation = Result<string>.Success(name)
            .ValidateWith(n => !string.IsNullOrWhiteSpace(n) && n.Length >= 3 && n.Length <= 100,
                "Epic name must be between 3 and 100 characters", nameof(Name));

        var ownerValidation = Result<UserProfileId>.Success(ownerId)
            .ValidateWith(id => id != null && !string.IsNullOrWhiteSpace(id.Value),
                "Owner ID is required", "OwnerId");

        if (nameValidation.IsFailure || ownerValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (nameValidation.IsFailure) errors.AddRange(nameValidation.Errors);
            if (ownerValidation.IsFailure) errors.AddRange(ownerValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new EpicCreated(
                name,
                description ?? string.Empty,
                ownerId.Value,
                targetCompletionDate,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = createdByUser })));

        return Result.Success();
    }

    // Command: Rename the epic
    public async Task<Result> RenameEpic(
        string newName,
        UserProfileId renamedBy,
        VersionToken? renamedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Epic>.Success(this)
            .ValidateWith(e => !e.IsCompleted, "Cannot rename a completed epic", "IsCompleted");

        var nameValidation = Result<string>.Success(newName)
            .ValidateWith(n => !string.IsNullOrWhiteSpace(n) && n.Length >= 3 && n.Length <= 100,
                "Epic name must be between 3 and 100 characters", "NewName");

        if (validationResult.IsFailure || nameValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (validationResult.IsFailure) errors.AddRange(validationResult.Errors);
            if (nameValidation.IsFailure) errors.AddRange(nameValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        if (Name == newName)
            return Result.Success(); // No change needed

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new EpicRenamed(
                Name!,
                newName,
                renamedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = renamedByUser })));

        return Result.Success();
    }

    // Command: Update epic description
    public async Task<Result> UpdateDescription(
        string newDescription,
        UserProfileId updatedBy,
        VersionToken? updatedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Epic>.Success(this)
            .ValidateWith(e => !e.IsCompleted, "Cannot update a completed epic", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        if (Description == newDescription)
            return Result.Success(); // No change needed

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new EpicDescriptionUpdated(
                newDescription ?? string.Empty,
                updatedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = updatedByUser })));

        return Result.Success();
    }

    // Command: Add project to epic
    public async Task<Result> AddProject(
        ProjectId projectId,
        UserProfileId addedBy,
        VersionToken? addedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Epic>.Success(this)
            .ValidateWith(e => !e.IsCompleted, "Cannot add projects to a completed epic", "IsCompleted")
            .ValidateWith(e => projectId != null && projectId.Value != Guid.Empty,
                "Project ID is required", nameof(projectId))
            .ValidateWith(e => !Projects.Contains(projectId),
                $"Project {projectId?.Value} is already in this epic", nameof(projectId));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectAddedToEpic(
                projectId!.Value.ToString(),
                addedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = addedByUser })));

        return Result.Success();
    }

    // Command: Remove project from epic
    public async Task<Result> RemoveProject(
        ProjectId projectId,
        UserProfileId removedBy,
        VersionToken? removedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Epic>.Success(this)
            .ValidateWith(e => !e.IsCompleted, "Cannot remove projects from a completed epic", "IsCompleted")
            .ValidateWith(e => Projects.Contains(projectId),
                $"Project {projectId?.Value} is not in this epic", nameof(projectId));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectRemovedFromEpic(
                projectId!.Value.ToString(),
                removedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = removedByUser })));

        return Result.Success();
    }

    // Command: Change target completion date
    public async Task<Result> ChangeTargetDate(
        DateTime newTargetDate,
        UserProfileId changedBy,
        VersionToken? changedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Epic>.Success(this)
            .ValidateWith(e => !e.IsCompleted, "Cannot change target date of a completed epic", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        if (TargetCompletionDate == newTargetDate)
            return Result.Success(); // No change needed

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new EpicTargetDateChanged(
                TargetCompletionDate ?? DateTime.MinValue,
                newTargetDate,
                changedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = changedByUser })));

        return Result.Success();
    }

    // Command: Change priority
    public async Task<Result> ChangePriority(
        EpicPriority newPriority,
        UserProfileId changedBy,
        VersionToken? changedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Epic>.Success(this)
            .ValidateWith(e => !e.IsCompleted, "Cannot change priority of a completed epic", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        if (Priority == newPriority)
            return Result.Success(); // No change needed

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new EpicPriorityChanged(
                Priority.ToString(),
                newPriority.ToString(),
                changedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = changedByUser })));

        return Result.Success();
    }

    // Command: Complete the epic
    public async Task<Result> CompleteEpic(
        string summary,
        UserProfileId completedBy,
        VersionToken? completedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Epic>.Success(this)
            .ValidateWith(e => !e.IsCompleted, "Epic is already completed", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new EpicCompleted(
                summary ?? "Epic completed",
                completedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = completedByUser })));

        return Result.Success();
    }

    // Event handlers (When methods)
    private void When(EpicCreated @event)
    {
        Name = @event.Name;
        Description = @event.Description;
        OwnerId = UserProfileId.From(@event.OwnerId);
        TargetCompletionDate = @event.TargetCompletionDate;
        CreatedAt = @event.CreatedAt;
        IsCompleted = false;
    }

    private void When(EpicRenamed @event)
    {
        Name = @event.NewName;
    }

    private void When(EpicDescriptionUpdated @event)
    {
        Description = @event.NewDescription;
    }

    private void When(ProjectAddedToEpic @event)
    {
        var projectId = ProjectId.From(@event.ProjectId);
        if (!Projects.Contains(projectId))
        {
            Projects.Add(projectId);
        }
    }

    private void When(ProjectRemovedFromEpic @event)
    {
        var projectId = ProjectId.From(@event.ProjectId);
        Projects.Remove(projectId);
    }

    private void When(EpicTargetDateChanged @event)
    {
        TargetCompletionDate = @event.NewTargetDate;
    }

    private void When(EpicPriorityChanged @event)
    {
        Priority = Enum.Parse<EpicPriority>(@event.NewPriority);
    }

    [When<EpicCompleted>]
    private void WhenEpicCompleted()
    {
        IsCompleted = true;
    }

    private void PostWhen(IObjectDocument document, IEvent @event)
    {
        Metadata = ObjectMetadata<EpicId>.From(document, @event, EpicId.From(document.ObjectId));
    }
}
