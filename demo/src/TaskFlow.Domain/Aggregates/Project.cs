using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Actions;
using TaskFlow.Domain.Events.Project;
using TaskFlow.Domain.Specifications.Project;
using TaskFlow.Domain.Upcast;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.Project;

namespace TaskFlow.Domain.Aggregates;

[Aggregate]
[UseUpcaster<ProjectCompletedUpcast>]
[RetentionPolicy(MaxAge = "2y", MaxEvents = 5000, Action = RetentionAction.Migrate, KeepRecentEvents = 100)]
public partial class Project : Aggregate
{
    public Project(IEventStream stream) : base(stream)
    {
        stream.RegisterAction(new PublishProjectionUpdateAction());
    }

    /// <summary>
    /// Gets the event type registry containing all registered event type mappings.
    /// Used to look up the C# type for a given event name and schema version.
    /// </summary>
    public ErikLieben.FA.ES.EventStream.EventTypeRegistry EventTypeRegistry => Stream.EventTypeRegistry;

    public string? Name { get; private set; }
    public string? Description { get; private set; }
    public UserProfileId? OwnerId { get; private set; }
    public bool IsCompleted { get; private set; }
    public ProjectOutcome Outcome { get; private set; } = ProjectOutcome.None;
    public Dictionary<UserProfileId, string> TeamMembers { get; } = new();

    /// <summary>
    /// The required languages for work item titles in this project.
    /// Defaults to "en-US" only. Additional languages can be configured.
    /// </summary>
    public List<string> RequiredLanguages { get; } = new() { "en-US" };

    // Kanban board item ordering (per status column)
    public List<WorkItemId> PlannedItemsOrder { get; } = [];
    public List<WorkItemId> InProgressItemsOrder { get; } = [];
    public List<WorkItemId> CompletedItemsOrder { get; } = [];

    public ObjectMetadata<ProjectId> Metadata { get; private set; } = null!;

    // Command: Initiate a new project
    public async Task<Result> InitiateProject(string name, string description, UserProfileId ownerId, VersionToken? initiatedByUser = null, DateTime? occurredAt = null)
    {
        var nameValidation = Result<string>.Success(name)
            .ValidateWith<string, ValidProjectNameSpecification>("Project name must be between 3 and 100 characters", nameof(Name));

        var ownerValidation = Result<UserProfileId>.Success(ownerId)
            .ValidateWith(id => id != null && !string.IsNullOrWhiteSpace(id.Value), "Owner ID is required", "OwnerId");

        if (nameValidation.IsFailure || ownerValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (nameValidation.IsFailure) errors.AddRange(nameValidation.Errors);
            if (ownerValidation.IsFailure) errors.AddRange(ownerValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectInitiated(
                name,
                description ?? string.Empty,
                ownerId.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = initiatedByUser })));

        return Result.Success();
    }

    // Command: Rebrand the project with a new name
    public async Task<Result> RebrandProject(string newName, UserProfileId rebrandedBy, VersionToken? rebrandedByUser = null, DateTime? occurredAt = null)
    {
        var projectValidation = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Cannot rebrand a completed project", "ProjectCompleted");

        var nameValidation = Result<string>.Success(newName)
            .ValidateWith<string, ValidProjectNameSpecification>("Project name must be between 3 and 100 characters", "NewName");

        if (projectValidation.IsFailure || nameValidation.IsFailure)
        {
            var errors = new List<ValidationError>();
            if (projectValidation.IsFailure) errors.AddRange(projectValidation.Errors);
            if (nameValidation.IsFailure) errors.AddRange(nameValidation.Errors);
            return Result.Failure(errors.ToArray());
        }

        if (Name == newName)
            return Result.Success(); // No change needed

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectRebranded(
                Name!,
                newName,
                rebrandedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = rebrandedByUser })));

        return Result.Success();
    }

    // Command: Refine project scope/description
    public async Task<Result> RefineScope(string newDescription, UserProfileId refinedBy, VersionToken? refinedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Cannot refine scope of a completed project", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        if (Description == newDescription)
            return Result.Success(); // No change needed

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectScopeRefined(
                newDescription ?? string.Empty,
                refinedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = refinedByUser })));

        return Result.Success();
    }

    // Command: Complete/archive the project (LEGACY - for backwards compatibility demo)
    [Obsolete("Use CompleteProjectSuccessfully(), FailProject(), etc. instead", false)]
    public async Task<Result> CompleteProject(string outcome, UserProfileId completedBy, VersionToken? completedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith(p => !new CompletedProjectSpecification().IsSatisfiedBy(p), "Project is already completed", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectCompleted(
                outcome ?? "Project completed",
                completedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = completedByUser })));

        return Result.Success();
    }

    // Command: Complete project successfully
    public async Task<Result> CompleteProjectSuccessfully(string summary, UserProfileId completedBy, VersionToken? completedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Project is already completed", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectCompletedSuccessfully(
                summary ?? "Project completed successfully with all objectives met",
                completedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = completedByUser })));

        return Result.Success();
    }

    // Command: Cancel project
    public async Task<Result> CancelProject(string reason, UserProfileId cancelledBy, VersionToken? cancelledByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Project is already completed", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectCancelled(
                reason ?? "Project was cancelled",
                cancelledBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = cancelledByUser })));

        return Result.Success();
    }

    // Command: Mark project as failed
    public async Task<Result> FailProject(string reason, UserProfileId failedBy, VersionToken? failedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Project is already completed", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectFailed(
                reason ?? "Project failed to meet objectives",
                failedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = failedByUser })));

        return Result.Success();
    }

    // Command: Deliver project
    public async Task<Result> DeliverProject(string deliveryNotes, UserProfileId deliveredBy, VersionToken? deliveredByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Project is already completed", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectDelivered(
                deliveryNotes ?? "Project delivered to production/client",
                deliveredBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = deliveredByUser })));

        return Result.Success();
    }

    // Command: Suspend project
    public async Task<Result> SuspendProject(string reason, UserProfileId suspendedBy, VersionToken? suspendedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Project is already completed", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectSuspended(
                reason ?? "Project was suspended",
                suspendedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = suspendedByUser })));

        return Result.Success();
    }

    // Command: Merge project into another
    public async Task<Result> MergeProject(string targetProjectId, string reason, UserProfileId mergedBy, VersionToken? mergedByUser = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Project is already completed", "IsCompleted")
            .ValidateWith(p => !string.IsNullOrWhiteSpace(targetProjectId), "Target project ID is required", nameof(targetProjectId));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        await Stream.Session(context =>
            Fold(context.Append(new ProjectMerged(
                targetProjectId,
                reason ?? "Project was merged",
                mergedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = mergedByUser })));

        return Result.Success();
    }

    // Command: Reactivate a completed project
    public async Task<Result> ReactivateProject(string rationale, UserProfileId reactivatedBy, VersionToken? reactivatedByUser = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, CompletedProjectSpecification>("Only completed projects can be reactivated", "IsCompleted");

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        await Stream.Session(context =>
            Fold(context.Append(new ProjectReactivated(
                rationale,
                reactivatedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = reactivatedByUser })));

        return Result.Success();
    }

    // Command: Configure required languages for work item titles
    public async Task<Result> ConfigureLanguages(string[] requiredLanguages, UserProfileId configuredBy, VersionToken? configuredByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Cannot configure languages for a completed project", "IsCompleted")
            .ValidateWith(p => requiredLanguages != null && requiredLanguages.Length > 0, "At least one language must be specified", nameof(requiredLanguages))
            .ValidateWith(p => requiredLanguages!.Contains("en-US"), "English (en-US) must always be included as a required language", nameof(requiredLanguages));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new ProjectLanguagesConfigured(
                requiredLanguages!,
                configuredBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = configuredByUser })));

        return Result.Success();
    }

    // Command: Add team member to project (LEGACY - uses V1 event for demo purposes)
    [Obsolete("Use AddTeamMemberWithPermissions instead for new code", false)]
    public async Task<Result> AddTeamMember(UserProfileId memberId, string role, UserProfileId invitedBy, VersionToken? invitedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Cannot add members to a completed project", "IsCompleted")
            .ValidateWith(p => memberId != null && !string.IsNullOrWhiteSpace(memberId.Value), "Member ID is required", nameof(memberId))
            .ValidateWith(p => !new HasTeamMemberSpecification(memberId).IsSatisfiedBy(p), $"Member {memberId?.Value} is already part of the project", nameof(memberId));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new MemberJoinedProjectV1(
                memberId!.Value,
                role ?? "Member",
                invitedBy!.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = invitedByUser })));

        return Result.Success();
    }

    // Command: Add team member to project with permissions (current - uses V2 event)
    public async Task<Result> AddTeamMemberWithPermissions(UserProfileId memberId, string role, MemberPermissions permissions, UserProfileId invitedBy, VersionToken? invitedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Cannot add members to a completed project", "IsCompleted")
            .ValidateWith(p => memberId != null && !string.IsNullOrWhiteSpace(memberId.Value), "Member ID is required", nameof(memberId))
            .ValidateWith(p => !new HasTeamMemberSpecification(memberId).IsSatisfiedBy(p), $"Member {memberId?.Value} is already part of the project", nameof(memberId));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new MemberJoinedProject(
                memberId!.Value,
                role ?? "Member",
                permissions,
                invitedBy!.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = invitedByUser })));

        return Result.Success();
    }

    // Command: Remove team member from project
    public async Task<Result> RemoveTeamMember(UserProfileId memberId, UserProfileId removedBy, VersionToken? removedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith<Project, ActiveProjectSpecification>("Cannot remove members from a completed project", "IsCompleted")
            .ValidateWith(p => new HasTeamMemberSpecification(memberId).IsSatisfiedBy(p), $"Member {memberId?.Value} is not part of the project", nameof(memberId));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new MemberLeftProject(
                memberId!.Value,
                removedBy!.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = removedByUser })));

        return Result.Success();
    }

    // Command: Add work item to project
    public async Task<Result> AddWorkItem(WorkItemId workItemId, UserProfileId addedBy, VersionToken? addedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith(p => workItemId != null && workItemId.Value != Guid.Empty, "Work item ID is required", nameof(workItemId))
            .ValidateWith(p => !PlannedItemsOrder.Contains(workItemId) &&
                              !InProgressItemsOrder.Contains(workItemId) &&
                              !CompletedItemsOrder.Contains(workItemId),
                              $"Work item {workItemId?.Value} is already in the project", nameof(workItemId));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new WorkItemAddedToProject(
                workItemId!.Value.ToString(),
                addedBy!.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = addedByUser })));

        return Result.Success();
    }

    // Command: Update work item status in project (move between kanban columns)
    public async Task<Result> UpdateWorkItemStatus(WorkItemId workItemId, WorkItemStatus fromStatus, WorkItemStatus toStatus, UserProfileId changedBy, VersionToken? changedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith(p => workItemId != null && workItemId.Value != Guid.Empty, "Work item ID is required", nameof(workItemId))
            .ValidateWith(p => fromStatus != toStatus, "Status must change", nameof(toStatus));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        // Verify the work item is in the expected source list
        var sourceList = fromStatus switch
        {
            WorkItemStatus.Planned => PlannedItemsOrder,
            WorkItemStatus.InProgress => InProgressItemsOrder,
            WorkItemStatus.Completed => CompletedItemsOrder,
            _ => throw new InvalidOperationException($"Unknown status: {fromStatus}")
        };

        if (!sourceList.Contains(workItemId))
        {
            return Result.Failure(new ValidationError(nameof(workItemId),
                $"Work item {workItemId.Value} is not in the {fromStatus} list"));
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new WorkItemStatusChangedInProject(
                workItemId.Value.ToString(),
                fromStatus,
                toStatus,
                changedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = changedByUser })));

        return Result.Success();
    }

    // Command: Reorder work item within its status column on kanban board
    public async Task<Result> ReorderWorkItem(WorkItemId workItemId, WorkItemStatus status, int newPosition, UserProfileId reorderedBy, VersionToken? reorderedByUser = null)
    {
        var validationResult = Result<Project>.Success(this)
            .ValidateWith(p => workItemId != null && workItemId.Value != Guid.Empty, "Work item ID is required", nameof(workItemId))
            .ValidateWith(p => newPosition >= 0, "Position must be non-negative", nameof(newPosition));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        await Stream.Session(context =>
            Fold(context.Append(new WorkItemReordered(
                workItemId.Value.ToString(),
                status,
                newPosition,
                reorderedBy.Value,
                DateTime.UtcNow), new ActionMetadata { OriginatedFromUser = reorderedByUser })));

        return Result.Success();
    }

    // Command: Add a demo note (always allowed, regardless of project state)
    // Used for demonstrating live migration scenarios where events are added during migration
    public async Task<Result> AddDemoNote(string note, UserProfileId addedBy, VersionToken? addedByUser = null, DateTime? occurredAt = null)
    {
        var validationResult = Result<string>.Success(note)
            .ValidateWith(n => !string.IsNullOrWhiteSpace(n), "Note cannot be empty", nameof(note));

        if (validationResult.IsFailure)
            return validationResult.ToResult();

        var timestamp = occurredAt ?? DateTime.UtcNow;
        await Stream.Session(context =>
            Fold(context.Append(new DemoNoteAdded(
                note,
                addedBy.Value,
                timestamp), new ActionMetadata { EventOccuredAt = timestamp, OriginatedFromUser = addedByUser })));

        return Result.Success();
    }

    // Event handlers (When methods)
    private void When(ProjectInitiated @event)
    {
        Name = @event.Name;
        Description = @event.Description;
        OwnerId = UserProfileId.From(@event.OwnerId);
        IsCompleted = false;
    }

    private void When(ProjectRebranded @event)
    {
        Name = @event.NewName;
    }

    private void When(ProjectScopeRefined @event)
    {
        Description = @event.NewDescription;
    }

    [When<ProjectCompleted>]

    private void WhenProjectCompleted()
    {
        IsCompleted = true;
        // Note: Outcome is not set for legacy ProjectCompleted events
        // They will be upcasted to specific outcome events when read
    }

    [When<ProjectCompletedSuccessfully>]

    private void WhenProjectCompletedSuccessfully()
    {
        IsCompleted = true;
        Outcome = ProjectOutcome.Successful;
    }

    [When<ProjectCancelled>]

    private void WhenProjectCancelled()
    {
        IsCompleted = true;
        Outcome = ProjectOutcome.Cancelled;
    }

    [When<ProjectFailed>]

    private void WhenProjectFailed()
    {
        IsCompleted = true;
        Outcome = ProjectOutcome.Failed;
    }

    [When<ProjectDelivered>]

    private void WhenProjectDelivered()
    {
        IsCompleted = true;
        Outcome = ProjectOutcome.Delivered;
    }

    [When<ProjectSuspended>]

    private void WhenProjectSuspended()
    {
        IsCompleted = true;
        Outcome = ProjectOutcome.Suspended;
    }

    [When<ProjectMerged>]

    private void WhenProjectMerged()
    {
        IsCompleted = true;
        Outcome = ProjectOutcome.Merged;
    }

    [When<ProjectReactivated>]
    private void WhenProjectReactivated()
    {
        IsCompleted = false;
        Outcome = ProjectOutcome.None;
    }

    private void When(ProjectLanguagesConfigured @event)
    {
        RequiredLanguages.Clear();
        RequiredLanguages.AddRange(@event.RequiredLanguages);
    }

    // Handle V1 (legacy) MemberJoined events
    private void When(MemberJoinedProjectV1 @event)
    {
        TeamMembers[UserProfileId.From(@event.MemberId)] = @event.Role;
    }

    // Handle V2 (current) MemberJoined events with permissions
    private void When(MemberJoinedProject @event)
    {
        TeamMembers[UserProfileId.From(@event.MemberId)] = @event.Role;
        // Note: Permissions are available in @event.Permissions for V2 events
        // The aggregate stores just the role, but projections can use the full permissions
    }

    private void When(MemberLeftProject @event)
    {
        TeamMembers.Remove(UserProfileId.From(@event.MemberId));
    }

    private void When(WorkItemAddedToProject @event)
    {
        var workItemId = WorkItemId.From(@event.WorkItemId);
        // Add to the planned items order at the end (new items start as planned)
        if (!PlannedItemsOrder.Contains(workItemId))
        {
            PlannedItemsOrder.Add(workItemId);
        }
    }

    private void When(WorkItemStatusChangedInProject @event)
    {
        var workItemId = WorkItemId.From(@event.WorkItemId);

        // Remove from source list
        var sourceList = @event.FromStatus switch
        {
            WorkItemStatus.Planned => PlannedItemsOrder,
            WorkItemStatus.InProgress => InProgressItemsOrder,
            WorkItemStatus.Completed => CompletedItemsOrder,
            _ => throw new InvalidOperationException($"Unknown status: {@event.FromStatus}")
        };
        sourceList.Remove(workItemId);

        // Add to target list
        var targetList = @event.ToStatus switch
        {
            WorkItemStatus.Planned => PlannedItemsOrder,
            WorkItemStatus.InProgress => InProgressItemsOrder,
            WorkItemStatus.Completed => CompletedItemsOrder,
            _ => throw new InvalidOperationException($"Unknown status: {@event.ToStatus}")
        };
        targetList.Add(workItemId);
    }

    private void When(WorkItemReordered @event)
    {
        var targetList = @event.Status switch
        {
            WorkItemStatus.Planned => PlannedItemsOrder,
            WorkItemStatus.InProgress => InProgressItemsOrder,
            WorkItemStatus.Completed => CompletedItemsOrder,
            _ => throw new InvalidOperationException($"Unknown status: {@event.Status}")
        };

        var workItemId = WorkItemId.From(@event.WorkItemId);

        // Remove the item from its current position if it exists
        targetList.Remove(workItemId);

        // Insert at the new position
        var insertIndex = Math.Min(@event.NewPosition, targetList.Count);
        targetList.Insert(insertIndex, workItemId);
    }

    // Demo note events don't affect aggregate state - they're just for migration demos
    [When<DemoNoteAdded>]
    private void WhenDemoNoteAdded()
    {
        // No state change - demo notes are only for demonstrating live migration
    }

    private void PostWhen(IObjectDocument document, IEvent @event)
    {
        Metadata = ObjectMetadata<ProjectId>.From(document, @event, ProjectId.From(document.ObjectId));
    }
}
