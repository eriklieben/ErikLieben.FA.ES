using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Actions;
using TaskFlow.Domain.Events.Sprint;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.Sprint;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// Sprint aggregate - represents a time-boxed iteration for completing work items.
/// Stored in Azure CosmosDB to demonstrate the CosmosDB storage provider.
/// </summary>
[Aggregate]
[EventStreamType("cosmosdb", "cosmosdb")]
public partial class Sprint : Aggregate
{
    public Sprint(IEventStream stream) : base(stream)
    {
        stream.RegisterAction(new PublishProjectionUpdateAction());
    }

    /// <summary>
    /// Initializes a stream with all Sprint event registrations (for AOT-compatible scenarios).
    /// Use this when you need a stream without creating the aggregate.
    /// </summary>
    public static void InitializeStream(IEventStream stream)
    {
        _ = new Sprint(stream);
    }

    public string? Name { get; private set; }
    public ProjectId? ProjectId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public string? Goal { get; private set; }
    public SprintStatus Status { get; private set; } = SprintStatus.Planned;
    public DateTime CreatedAt { get; private set; }
    public UserProfileId? CreatedBy { get; private set; }
    public List<WorkItemId> WorkItems { get; } = new();
    public ObjectMetadata<SprintId>? Metadata { get; private set; }

    // Command: Create a new sprint
    public async Task<Result> CreateSprint(
        string name,
        ProjectId projectId,
        DateTime startDate,
        DateTime endDate,
        string? goal,
        UserProfileId createdBy,
        VersionToken? createdByUser = null,
        DateTime? occurredAt = null)
    {
        var nameValidation = Result<string>.Success(name)
            .ValidateWith(n => !string.IsNullOrWhiteSpace(n) && n.Length >= 3 && n.Length <= 100,
                "Sprint name must be between 3 and 100 characters", nameof(Name));

        var projectValidation = Result<ProjectId>.Success(projectId)
            .ValidateWith(id => id != null && id.Value != Guid.Empty,
                "Project ID is required", nameof(ProjectId));

        var dateValidation = Result<(DateTime start, DateTime end)>.Success((startDate, endDate))
            .ValidateWith(d => d.end > d.start,
                "End date must be after start date", "EndDate");

        if (nameValidation.IsFailure || projectValidation.IsFailure || dateValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (nameValidation.IsFailure) errors.AddRange(nameValidation.Errors);
            if (projectValidation.IsFailure) errors.AddRange(projectValidation.Errors);
            if (dateValidation.IsFailure) errors.AddRange(dateValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new SprintCreated(
                name,
                projectId!.Value.ToString(),
                startDate,
                endDate,
                goal,
                createdBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = createdByUser })));

        return Result.Success();
    }

    // Command: Start the sprint
    public async Task<Result> StartSprint(
        UserProfileId startedBy,
        VersionToken? startedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Sprint>.Success(this)
            .ValidateWith(s => s.Status == SprintStatus.Planned,
                "Sprint can only be started when it is in Planned status", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new SprintStarted(
                startedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = startedByUser })));

        return Result.Success();
    }

    // Command: Complete the sprint
    public async Task<Result> CompleteSprint(
        UserProfileId completedBy,
        string? summary = null,
        VersionToken? completedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Sprint>.Success(this)
            .ValidateWith(s => s.Status == SprintStatus.Active,
                "Sprint can only be completed when it is Active", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new SprintCompleted(
                completedBy.Value,
                timestamp,
                summary), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = completedByUser })));

        return Result.Success();
    }

    // Command: Cancel the sprint
    public async Task<Result> CancelSprint(
        UserProfileId cancelledBy,
        string? reason = null,
        VersionToken? cancelledByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Sprint>.Success(this)
            .ValidateWith(s => s.Status != SprintStatus.Completed && s.Status != SprintStatus.Cancelled,
                "Sprint cannot be cancelled when already completed or cancelled", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new SprintCancelled(
                cancelledBy.Value,
                timestamp,
                reason), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = cancelledByUser })));

        return Result.Success();
    }

    // Command: Add work item to sprint
    public async Task<Result> AddWorkItem(
        WorkItemId workItemId,
        UserProfileId addedBy,
        VersionToken? addedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Sprint>.Success(this)
            .ValidateWith(s => s.Status == SprintStatus.Planned || s.Status == SprintStatus.Active,
                "Work items can only be added to Planned or Active sprints", "Status")
            .ValidateWith(s => !s.WorkItems.Contains(workItemId),
                $"Work item {workItemId?.Value} is already in this sprint", nameof(workItemId));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new WorkItemAddedToSprint(
                workItemId!.Value.ToString(),
                addedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = addedByUser })));

        return Result.Success();
    }

    // Command: Remove work item from sprint
    public async Task<Result> RemoveWorkItem(
        WorkItemId workItemId,
        UserProfileId removedBy,
        string? reason = null,
        VersionToken? removedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Sprint>.Success(this)
            .ValidateWith(s => s.Status == SprintStatus.Planned || s.Status == SprintStatus.Active,
                "Work items can only be removed from Planned or Active sprints", "Status")
            .ValidateWith(s => s.WorkItems.Contains(workItemId),
                $"Work item {workItemId?.Value} is not in this sprint", nameof(workItemId));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new WorkItemRemovedFromSprint(
                workItemId!.Value.ToString(),
                removedBy.Value,
                timestamp,
                reason), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = removedByUser })));

        return Result.Success();
    }

    // Command: Update sprint goal
    public async Task<Result> UpdateGoal(
        string? newGoal,
        UserProfileId updatedBy,
        VersionToken? updatedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Sprint>.Success(this)
            .ValidateWith(s => s.Status != SprintStatus.Completed && s.Status != SprintStatus.Cancelled,
                "Cannot update goal of a completed or cancelled sprint", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        if (Goal == newGoal)
            return Result.Success(); // No change needed

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new SprintGoalUpdated(
                Goal,
                newGoal,
                updatedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = updatedByUser })));

        return Result.Success();
    }

    // Command: Change sprint dates
    public async Task<Result> ChangeDates(
        DateTime newStartDate,
        DateTime newEndDate,
        UserProfileId changedBy,
        string? reason = null,
        VersionToken? changedByUser = null,
        DateTime? occurredAt = null)
    {
        var validationResult = Result<Sprint>.Success(this)
            .ValidateWith(s => s.Status == SprintStatus.Planned,
                "Sprint dates can only be changed when sprint is in Planned status", "Status");

        var dateValidation = Result<(DateTime start, DateTime end)>.Success((newStartDate, newEndDate))
            .ValidateWith(d => d.end > d.start,
                "End date must be after start date", "EndDate");

        if (validationResult.IsFailure || dateValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (validationResult.IsFailure) errors.AddRange(validationResult.Errors);
            if (dateValidation.IsFailure) errors.AddRange(dateValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        if (StartDate == newStartDate && EndDate == newEndDate)
            return Result.Success(); // No change needed

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new SprintDatesChanged(
                StartDate,
                EndDate,
                newStartDate,
                newEndDate,
                changedBy.Value,
                timestamp,
                reason), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = changedByUser })));

        return Result.Success();
    }

    // Event handlers (When methods)
    private void When(SprintCreated @event)
    {
        Name = @event.Name;
        ProjectId = ValueObjects.ProjectId.From(@event.ProjectId);
        StartDate = @event.StartDate;
        EndDate = @event.EndDate;
        Goal = @event.Goal;
        CreatedBy = UserProfileId.From(@event.CreatedBy);
        CreatedAt = @event.CreatedAt;
        Status = SprintStatus.Planned;
    }

    [When<SprintStarted>]
    private void WhenSprintStarted()
    {
        Status = SprintStatus.Active;
    }

    [When<SprintCompleted>]
    private void WhenSprintCompleted()
    {
        Status = SprintStatus.Completed;
    }

    [When<SprintCancelled>]
    private void WhenSprintCancelled()
    {
        Status = SprintStatus.Cancelled;
    }

    private void When(WorkItemAddedToSprint @event)
    {
        var workItemId = WorkItemId.From(@event.WorkItemId);
        if (!WorkItems.Contains(workItemId))
        {
            WorkItems.Add(workItemId);
        }
    }

    private void When(WorkItemRemovedFromSprint @event)
    {
        var workItemId = WorkItemId.From(@event.WorkItemId);
        WorkItems.Remove(workItemId);
    }

    private void When(SprintGoalUpdated @event)
    {
        Goal = @event.NewGoal;
    }

    private void When(SprintDatesChanged @event)
    {
        StartDate = @event.NewStartDate;
        EndDate = @event.NewEndDate;
    }

    private void PostWhen(IObjectDocument document, IEvent @event)
    {
        Metadata = ObjectMetadata<SprintId>.From(document, @event, SprintId.From(document.ObjectId));
    }
}
