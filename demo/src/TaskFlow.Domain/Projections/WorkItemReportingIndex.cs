using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.Documents;
using TaskFlow.Domain.Events.WorkItem;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Table Storage projection that maintains an index of work items for reporting.
/// Each work item is stored as a separate table row, enabling efficient queries
/// by project, status, priority, and assignee.
/// </summary>
/// <remarks>
/// This projection demonstrates the Table Storage projection pattern:
/// - Each event creates/updates/deletes a table row
/// - PartitionKey is the ProjectId for efficient project-scoped queries
/// - RowKey is the WorkItemId for unique identification
///
/// Use cases:
/// - Reporting dashboards that need to filter/sort work items
/// - Export to spreadsheets or BI tools
/// - Cross-project work item searches
/// </remarks>
[TableProjection("workitemindex", ConnectionName = "tables")]
public partial class WorkItemReportingIndex : TableProjection
{
    // Track work items by ID so we can update them efficiently across events
    private readonly Dictionary<string, WorkItemReportingIndexEntity> _trackedEntities = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {
        var entity = new WorkItemReportingIndexEntity
        {
            PartitionKey = @event.ProjectId,
            RowKey = workItemId,
            Title = @event.Title,
            Description = @event.Description,
            Status = WorkItemStatus.Planned.ToString(),
            Priority = @event.Priority.ToString(),
            PlannedAt = @event.PlannedAt,
            LastUpdatedAt = @event.PlannedAt,
            LastUpdatedBy = @event.PlannedBy
        };

        _trackedEntities[workItemId] = entity;
        UpsertEntity(entity);
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ResponsibilityAssigned @event, string workItemId)
    {
        UpdateTrackedEntity(workItemId, entity =>
        {
            entity.AssignedTo = @event.MemberId;
            entity.LastUpdatedAt = @event.AssignedAt;
            entity.LastUpdatedBy = @event.AssignedBy;
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ResponsibilityRelinquished @event, string workItemId)
    {
        UpdateTrackedEntity(workItemId, entity =>
        {
            entity.AssignedTo = null;
            entity.LastUpdatedAt = @event.RelinquishedAt;
            entity.LastUpdatedBy = @event.RelinquishedBy;
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCommenced @event, string workItemId)
    {
        UpdateTrackedEntity(workItemId, entity =>
        {
            entity.Status = WorkItemStatus.InProgress.ToString();
            entity.CommencedAt = @event.CommencedAt;
            entity.LastUpdatedAt = @event.CommencedAt;
            entity.LastUpdatedBy = @event.CommencedBy;
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCompleted @event, string workItemId)
    {
        UpdateTrackedEntity(workItemId, entity =>
        {
            entity.Status = WorkItemStatus.Completed.ToString();
            entity.CompletedAt = @event.CompletedAt;
            entity.LastUpdatedAt = @event.CompletedAt;
            entity.LastUpdatedBy = @event.CompletedBy;
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemReprioritized @event, string workItemId)
    {
        UpdateTrackedEntity(workItemId, entity =>
        {
            entity.Priority = @event.NewPriority.ToString();
            entity.LastUpdatedAt = @event.ReprioritizedAt;
            entity.LastUpdatedBy = @event.ReprioritizedBy;
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(DeadlineEstablished @event, string workItemId)
    {
        UpdateTrackedEntity(workItemId, entity =>
        {
            entity.Deadline = @event.Deadline;
            entity.LastUpdatedAt = @event.EstablishedAt;
            entity.LastUpdatedBy = @event.EstablishedBy;
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(DeadlineRemoved @event, string workItemId)
    {
        UpdateTrackedEntity(workItemId, entity =>
        {
            entity.Deadline = null;
            entity.LastUpdatedAt = @event.RemovedAt;
            entity.LastUpdatedBy = @event.RemovedBy;
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromCompletedToInProgress @event, string workItemId)
    {
        UpdateTrackedEntity(workItemId, entity =>
        {
            entity.Status = WorkItemStatus.InProgress.ToString();
            entity.CompletedAt = null;
            entity.LastUpdatedAt = @event.MovedAt;
            entity.LastUpdatedBy = @event.MovedBy;
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromCompletedToPlanned @event, string workItemId)
    {
        UpdateTrackedEntity(workItemId, entity =>
        {
            entity.Status = WorkItemStatus.Planned.ToString();
            entity.CompletedAt = null;
            entity.CommencedAt = null;
            entity.LastUpdatedAt = @event.MovedAt;
            entity.LastUpdatedBy = @event.MovedBy;
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromInProgressToPlanned @event, string workItemId)
    {
        UpdateTrackedEntity(workItemId, entity =>
        {
            entity.Status = WorkItemStatus.Planned.ToString();
            entity.CommencedAt = null;
            entity.LastUpdatedAt = @event.MovedAt;
            entity.LastUpdatedBy = @event.MovedBy;
        });
    }

    private void UpdateTrackedEntity(string workItemId, Action<WorkItemReportingIndexEntity> update)
    {
        if (_trackedEntities.TryGetValue(workItemId, out var entity))
        {
            update(entity);
            UpsertEntity(entity);
        }
        // If entity not tracked, it means WorkItemPlanned wasn't processed yet
        // This is expected when processing events out of order or resuming from checkpoint
    }
}
