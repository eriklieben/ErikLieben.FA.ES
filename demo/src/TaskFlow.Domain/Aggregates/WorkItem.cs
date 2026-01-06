using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Actions;
using TaskFlow.Domain.Events;
using TaskFlow.Domain.Events.WorkItem;
using TaskFlow.Domain.Specifications.WorkItem;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.WorkItem;
using LocalizedTitle = TaskFlow.Domain.ValueObjects.WorkItem.LocalizedTitle;

namespace TaskFlow.Domain.Aggregates;

[Aggregate]
public partial class WorkItem : Aggregate
{
    public WorkItem(IEventStream stream) : base(stream)
    {
        stream.RegisterAction(new PublishProjectionUpdateAction());
    }

    /// <summary>
    /// Initializes a stream with all WorkItem event registrations (for AOT-compatible scenarios).
    /// Use this when you need a stream without creating the aggregate.
    /// This creates a temporary aggregate instance to trigger event registration, then disposes it.
    /// </summary>
    /// <param name="stream">The stream to initialize</param>
    public static void InitializeStream(IEventStream stream)
    {
        // Create a temporary instance to trigger the generated event registrations
        // The constructor calls GeneratedSetup() which registers all events
        _ = new WorkItem(stream);
    }

    public Guid WorkItemId => Metadata?.Id?.Value ?? Guid.Empty;

    public string? ProjectId { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public WorkItemPriority Priority { get; private set; }
    public WorkItemStatus Status { get; private set; }
    public string? AssignedTo { get; private set; }
    public DateTime? Deadline { get; private set; }
    public int? EstimatedHours { get; private set; }
    public List<string> Tags { get; } = new();
    public List<WorkItemComment> Comments { get; } = new();

    /// <summary>
    /// Translated titles for this work item in different languages.
    /// The default title (Title property) is always in en-US.
    /// </summary>
    public List<LocalizedTitle> TitleTranslations { get; } = new();

    public ObjectMetadata<WorkItemId> Metadata { get; private set; } = null!;

    // Command: Plan a new task
    public async Task<Result> PlanTask(
        string projectId,
        string title,
        string description,
        WorkItemPriority priority,
        UserProfileId plannedBy,
        VersionToken? plannedByUser = null,
        DateTime? occurredAt = null,
        Dictionary<string, string>? titleTranslations = null)
    {
        var projectIdValidation = Result<string>.Success(projectId)
            .ValidateWith(p => !string.IsNullOrWhiteSpace(p), "Project ID is required", nameof(projectId));

        var titleValidation = Result<string>.Success(title)
            .ValidateWith<string, ValidWorkItemTitleSpecification>("Task title must be between 5 and 200 characters", nameof(title));

        if (projectIdValidation.IsFailure || titleValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (projectIdValidation.IsFailure) errors.AddRange(projectIdValidation.Errors);
            if (titleValidation.IsFailure) errors.AddRange(titleValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new WorkItemPlanned(
                projectId,
                title,
                description ?? string.Empty,
                priority,
                plannedBy.Value,
                timestamp,
                titleTranslations), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = plannedByUser })));

        return Result.Success();
    }

    // Command: Assign responsibility to a team member
    public async Task<Result> AssignResponsibility(string memberId, UserProfileId assignedBy, VersionToken? assignedByUser = null, DateTime? occurredAt = null)
    {
        var workItemValidation = Result<WorkItem>.Success(this)
            .ValidateWith<WorkItem, ActiveWorkItemSpecification>("Cannot assign a completed task", "Status");

        var memberIdValidation = Result<string>.Success(memberId)
            .ValidateWith(m => !string.IsNullOrWhiteSpace(m), "Member ID is required", nameof(memberId));

        if (workItemValidation.IsFailure || memberIdValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (workItemValidation.IsFailure) errors.AddRange(workItemValidation.Errors);
            if (memberIdValidation.IsFailure) errors.AddRange(memberIdValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        if (AssignedTo == memberId)
            return Result.Success(); // Already assigned

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ResponsibilityAssigned(
                memberId,
                assignedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = assignedByUser })));

        return Result.Success();
    }

    // Command: Relinquish task responsibility
    public async Task<Result> RelinquishResponsibility(UserProfileId relinquishedBy, VersionToken? relinquishedByUser = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith<WorkItem, AssignedWorkItemSpecification>("Task is not currently assigned", "AssignedTo")
            .ValidateWith<WorkItem, ActiveWorkItemSpecification>("Cannot unassign a completed task", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        await Stream.Session(context =>
            Fold(context.Append(new ResponsibilityRelinquished(
                relinquishedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = relinquishedByUser })));

        return Result.Success();
    }

    // Command: Commence work on the task
    public async Task<Result> CommenceWork(UserProfileId commencedBy, VersionToken? commencedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith<WorkItem, AssignedWorkItemSpecification>("Task must be assigned before work can commence", "AssignedTo")
            .ValidateWith<WorkItem, NotInProgressWorkItemSpecification>("Work has already commenced on this task", "Status")
            .ValidateWith<WorkItem, ActiveWorkItemSpecification>("Cannot start work on a completed task", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new WorkCommenced(
                commencedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = commencedByUser })));

        return Result.Success();
    }

    // Command: Complete the task
    public async Task<Result> CompleteWork(string outcome, UserProfileId completedBy, VersionToken? completedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith<WorkItem, ActiveWorkItemSpecification>("Task is already completed", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new WorkCompleted(
                outcome ?? "Work completed successfully",
                completedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = completedByUser })));

        return Result.Success();
    }

    // Command: Revive a completed task
    public async Task<Result> ReviveTask(string rationale, UserProfileId revivedBy, VersionToken? revivedByUser = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith<WorkItem, CompletedWorkItemSpecification>("Only completed tasks can be revived", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        await Stream.Session(context =>
            Fold(context.Append(new WorkItemRevived(
                rationale,
                revivedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = revivedByUser })));

        return Result.Success();
    }

    // Command: Reprioritize the task
    public async Task<Result> Reprioritize(
        WorkItemPriority newPriority,
        string rationale,
        UserProfileId reprioritizedBy,
        VersionToken? reprioritizedByUser = null)
    {
        if (Priority == newPriority)
            return Result.Success(); // No change needed

        await Stream.Session(context =>
            Fold(context.Append(new WorkItemReprioritized(
                Priority,
                newPriority,
                rationale ?? "Priority adjusted",
                reprioritizedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = reprioritizedByUser })));

        return Result.Success();
    }

    // Command: Reestimate effort
    public async Task<Result> ReestimateEffort(int estimatedHours, UserProfileId reestimatedBy, VersionToken? reestimatedByUser = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith(w => estimatedHours >= 0, "Estimated hours cannot be negative", nameof(estimatedHours))
            .ValidateWith<WorkItem, ActiveWorkItemSpecification>("Cannot reestimate effort for a completed task", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        await Stream.Session(context =>
            Fold(context.Append(new EffortReestimated(
                estimatedHours,
                reestimatedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = reestimatedByUser })));

        return Result.Success();
    }

    // Command: Refine requirements/description
    public async Task<Result> RefineRequirements(string newDescription, UserProfileId refinedBy, VersionToken? refinedByUser = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith<WorkItem, ActiveWorkItemSpecification>("Cannot refine requirements of a completed task", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        if (Description == newDescription)
            return Result.Success(); // No change needed

        await Stream.Session(context =>
            Fold(context.Append(new RequirementsRefined(
                newDescription ?? string.Empty,
                refinedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = refinedByUser })));

        return Result.Success();
    }

    // Command: Provide feedback/comment
    public async Task<Result> ProvideFeedback(string content, UserProfileId providedBy, VersionToken? providedByUser = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith(w => !string.IsNullOrWhiteSpace(content), "Feedback content is required", nameof(content))
            .ValidateWith(w => content?.Length <= 2000, "Feedback must not exceed 2000 characters", nameof(content));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var feedbackId = Guid.NewGuid().ToString("N")[..8];

        await Stream.Session(context =>
            Fold(context.Append(new FeedbackProvided(
                feedbackId,
                content,
                providedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = providedByUser })));

        return Result.Success();
    }

    // Command: Relocate task to different project
    public async Task<Result> RelocateToProject(
        string newProjectId,
        string rationale,
        UserProfileId relocatedBy,
        VersionToken? relocatedByUser = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith(w => !string.IsNullOrWhiteSpace(newProjectId), "New project ID is required", nameof(newProjectId));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        if (ProjectId == newProjectId)
            return Result.Success(); // Already in that project

        await Stream.Session(context =>
            Fold(context.Append(new WorkItemRelocated(
                ProjectId!,
                newProjectId,
                rationale ?? "Task relocated to different project",
                relocatedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = relocatedByUser })));

        return Result.Success();
    }

    // Command: Retag the task
    public async Task<Result> Retag(string[] tags, UserProfileId retaggedBy, VersionToken? retaggedByUser = null)
    {
        await Stream.Session(context =>
            Fold(context.Append(new WorkItemRetagged(
                tags ?? Array.Empty<string>(),
                retaggedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = retaggedByUser })));

        return Result.Success();
    }

    // Command: Establish a deadline
    public async Task<Result> EstablishDeadline(DateTime deadline, UserProfileId establishedBy, VersionToken? establishedByUser = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith(w => deadline >= DateTime.UtcNow, "Deadline cannot be in the past", nameof(deadline))
            .ValidateWith<WorkItem, ActiveWorkItemSpecification>("Cannot set deadline for a completed task", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        await Stream.Session(context =>
            Fold(context.Append(new DeadlineEstablished(
                deadline,
                establishedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = establishedByUser })));

        return Result.Success();
    }

    // Command: Remove the deadline
    public async Task<Result> RemoveDeadline(UserProfileId removedBy, VersionToken? removedByUser = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith(w => w.Deadline != null, "Task does not have a deadline set", nameof(Deadline));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        await Stream.Session(context =>
            Fold(context.Append(new DeadlineRemoved(
                removedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = removedByUser })));

        return Result.Success();
    }

    // Command: Move back from Completed to InProgress
    public async Task<Result> MoveBackFromCompletedToInProgress(string reason, UserProfileId movedBy, VersionToken? movedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith<WorkItem, CompletedWorkItemSpecification>("Can only move back from Completed status", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new MovedBackFromCompletedToInProgress(
                reason,
                movedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = movedByUser })));

        return Result.Success();
    }

    // Command: Move back from Completed to Planned
    public async Task<Result> MoveBackFromCompletedToPlanned(string reason, UserProfileId movedBy, VersionToken? movedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith<WorkItem, CompletedWorkItemSpecification>("Can only move back from Completed status", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new MovedBackFromCompletedToPlanned(
                reason,
                movedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = movedByUser })));

        return Result.Success();
    }

    // Command: Move back from InProgress to Planned
    public async Task<Result> MoveBackFromInProgressToPlanned(string reason, UserProfileId movedBy, VersionToken? movedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<WorkItem>.Success(this)
            .ValidateWith<WorkItem, InProgressWorkItemSpecification>("Can only move back from InProgress status", "Status");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new MovedBackFromInProgressToPlanned(
                reason,
                movedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = movedByUser })));

        return Result.Success();
    }

    // Command: Mark drag as accidental
    public async Task<Result> MarkDragAsAccidental(
        WorkItemStatus fromStatus,
        WorkItemStatus toStatus,
        UserProfileId markedBy,
        VersionToken? markedByUser = null,
        DateTime? occurredAt = null)
    {
        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new DragMarkedAsAccidental(
                fromStatus,
                toStatus,
                markedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = markedByUser })));

        return Result.Success();
    }

    // Event handlers (When methods)
    private void When(WorkItemPlanned @event)
    {
        ProjectId = @event.ProjectId;
        Title = @event.Title;
        Description = @event.Description;
        Priority = @event.Priority;
        Status = WorkItemStatus.Planned;

        // Populate title translations if provided
        if (@event.TitleTranslations != null)
        {
            TitleTranslations.Clear();
            foreach (var translation in @event.TitleTranslations)
            {
                TitleTranslations.Add(new LocalizedTitle(translation.Key, translation.Value));
            }
        }
    }

    private void When(ResponsibilityAssigned @event)
    {
        AssignedTo = @event.MemberId;
    }

    private void When(ResponsibilityRelinquished @event)
    {
        AssignedTo = null;
    }

    private void When(WorkCommenced @event)
    {
        Status = WorkItemStatus.InProgress;
    }

    private void When(WorkCompleted @event)
    {
        Status = WorkItemStatus.Completed;
    }

    private void When(WorkItemRevived @event)
    {
        Status = WorkItemStatus.Planned;
    }

    private void When(WorkItemReprioritized @event)
    {
        Priority = @event.NewPriority;
    }

    private void When(EffortReestimated @event)
    {
        EstimatedHours = @event.EstimatedHours;
    }

    private void When(RequirementsRefined @event)
    {
        Description = @event.NewDescription;
    }

    private void When(FeedbackProvided @event)
    {
        Comments.Add(new WorkItemComment(
            @event.FeedbackId,
            @event.Content,
            @event.ProvidedBy,
            @event.ProvidedAt));
    }

    private void When(WorkItemRelocated @event)
    {
        ProjectId = @event.NewProjectId;
    }

    private void When(WorkItemRetagged @event)
    {
        Tags.Clear();
        Tags.AddRange(@event.Tags);
    }

    private void When(DeadlineEstablished @event)
    {
        Deadline = @event.Deadline;
    }

    private void When(DeadlineRemoved @event)
    {
        Deadline = null;
    }

    private void When(MovedBackFromCompletedToInProgress @event)
    {
        Status = WorkItemStatus.InProgress;
    }

    private void When(MovedBackFromCompletedToPlanned @event)
    {
        Status = WorkItemStatus.Planned;
    }

    private void When(MovedBackFromInProgressToPlanned @event)
    {
        Status = WorkItemStatus.Planned;
    }

    private void When(DragMarkedAsAccidental @event)
    {
        // This is a marker event - no state change needed, just audit trail
    }

    private void PostWhen(IObjectDocument document, IEvent @event)
    {
        Metadata = ObjectMetadata<ValueObjects.WorkItemId>.From(document, @event, ValueObjects.WorkItemId.From(document.ObjectId));
    }

    /// <summary>
    /// Creates a snapshot of the current aggregate state at the specified version.
    /// </summary>
    /// <remarks>
    /// Snapshots improve performance for aggregates with many events by allowing
    /// the system to load from the latest snapshot instead of replaying all events.
    /// Consider creating snapshots for aggregates that:
    /// - Have more than 50-100 events
    /// - Are frequently loaded
    /// - Have expensive event folding operations
    /// </remarks>
    /// <param name="untilVersion">The version number to create the snapshot at.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task CreateSnapshotAsync(int? untilVersion = null)
    {
        var version = untilVersion ?? Metadata?.VersionInStream ?? 0;
        return Stream.Snapshot<WorkItem>(version);
    }

    /// <summary>
    /// Restores the aggregate state from a snapshot.
    /// This partial method is called by the generated ProcessSnapshot method
    /// when loading an aggregate that has a snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot object containing the saved state.</param>
    partial void ProcessSnapshotImpl(object snapshot)
    {
        if (snapshot is not WorkItemSnapshot workItemSnapshot)
        {
            throw new InvalidOperationException($"Expected WorkItemSnapshot but got {snapshot?.GetType().Name ?? "null"}");
        }

        // Restore all state from the snapshot
        ProjectId = workItemSnapshot.ProjectId;
        Title = workItemSnapshot.Title;
        Description = workItemSnapshot.Description;
        Priority = workItemSnapshot.Priority;
        Status = workItemSnapshot.Status;
        AssignedTo = workItemSnapshot.AssignedTo;
        Deadline = workItemSnapshot.Deadline;
        EstimatedHours = workItemSnapshot.EstimatedHours;

        Tags.Clear();
        if (workItemSnapshot.Tags != null)
        {
            Tags.AddRange(workItemSnapshot.Tags);
        }

        Comments.Clear();
        if (workItemSnapshot.Comments != null)
        {
            Comments.AddRange(workItemSnapshot.Comments);
        }

        TitleTranslations.Clear();
        if (workItemSnapshot.TitleTranslations != null)
        {
            TitleTranslations.AddRange(workItemSnapshot.TitleTranslations);
        }

        Metadata = workItemSnapshot.Metadata!;
    }
}
