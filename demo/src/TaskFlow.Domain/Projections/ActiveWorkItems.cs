using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events;
using TaskFlow.Domain.Events.WorkItem;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Projection that tracks all active (non-completed) work items per project
/// This demonstrates a simple filtered view of work items
/// </summary>
[ProjectionWithExternalCheckpoint]
[ProjectionVersion(1)]
[BlobJsonProjection("projections", Connection = "BlobStorage")]
public partial class ActiveWorkItems : Projection
{
    public Dictionary<string, WorkItemSummary> WorkItems { get; } = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {


        WorkItems[workItemId] = new WorkItemSummary
        {
            WorkItemId = workItemId,
            ProjectId = @event.ProjectId,
            Title = @event.Title,
            Description = @event.Description,
            Priority = @event.Priority,
            Status = WorkItemStatus.Planned,
            PlannedAt = @event.PlannedAt
        };
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ResponsibilityAssigned @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.AssignedTo = @event.MemberId;
            item.AssignedAt = @event.AssignedAt;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ResponsibilityRelinquished @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.AssignedTo = null;
            item.AssignedAt = null;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCommenced @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.Status = WorkItemStatus.InProgress;
            item.CommencedAt = @event.CommencedAt;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCompleted @event, string workItemId)
    {
        // Keep completed items in the projection (for Kanban board)
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.Status = WorkItemStatus.Completed;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemRevived @event, string workItemId)
    {
        // Re-add to active items when revived
        // We'll need to replay events to rebuild the full state
        // For now, just create a minimal entry
        var workItemIdStr = workItemId;
        if (!WorkItems.ContainsKey(workItemIdStr))
        {
            WorkItems[workItemIdStr] = new WorkItemSummary
            {
                WorkItemId = workItemIdStr,
                Status = WorkItemStatus.Planned
            };
        }
        else
        {
            WorkItems[workItemIdStr].Status = WorkItemStatus.Planned;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemReprioritized @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.Priority = @event.NewPriority;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemRelocated @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.ProjectId = @event.NewProjectId;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(DeadlineEstablished @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.Deadline = @event.Deadline;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(DeadlineRemoved @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.Deadline = null;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromCompletedToInProgress @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.Status = WorkItemStatus.InProgress;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromCompletedToPlanned @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.Status = WorkItemStatus.Planned;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromInProgressToPlanned @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.Status = WorkItemStatus.Planned;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(DragMarkedAsAccidental @event, string workItemId)
    {
        // This is a marker event - no state change needed in projection
    }

    /// <summary>
    /// Get all active work items for a specific project
    /// </summary>
    public IEnumerable<WorkItemSummary> GetByProject(string projectId)
    {
        return WorkItems.Values.Where(w => w.ProjectId == projectId);
    }

    /// <summary>
    /// Get all active work items assigned to a specific member
    /// </summary>
    public IEnumerable<WorkItemSummary> GetByAssignee(string memberId)
    {
        return WorkItems.Values.Where(w => w.AssignedTo == memberId);
    }

    /// <summary>
    /// Get all overdue work items
    /// </summary>
    public IEnumerable<WorkItemSummary> GetOverdue()
    {
        var now = DateTime.UtcNow;
        return WorkItems.Values.Where(w => w.Deadline.HasValue && w.Deadline.Value < now);
    }
}

/// <summary>
/// Summary view of a work item for the projection
/// </summary>
public class WorkItemSummary
{
    public string WorkItemId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkItemPriority Priority { get; set; }
    public WorkItemStatus Status { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime? PlannedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? CommencedAt { get; set; }
}
