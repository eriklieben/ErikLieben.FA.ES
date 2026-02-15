using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.CosmosDb;
using ErikLieben.FA.ES.Documents;
using TaskFlow.Domain.Events.WorkItem;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// CosmosDB multi-document projection that maintains a complete audit log of work item changes.
/// Each event creates a new document, providing a full history of all changes.
/// </summary>
/// <remarks>
/// This projection demonstrates the CosmosDB multi-document projection pattern:
/// - Each event appends a new document (append-only log)
/// - PartitionKey is the WorkItemId for efficient per-item history queries
/// - All events are stored, not just final state
///
/// Use cases:
/// - Compliance and audit requirements
/// - Debugging and troubleshooting
/// - Timeline/activity feed for work items
/// - Change tracking and rollback scenarios
/// </remarks>
[CosmosDbMultiDocumentProjection("workitemauditlog")]
public partial class WorkItemAuditLog : MultiDocumentProjection
{
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "WorkItemPlanned",
            Timestamp = @event.PlannedAt,
            UserId = @event.PlannedBy,
            Description = $"Work item '{@event.Title}' was planned",
            Details = new Dictionary<string, object?>
            {
                ["projectId"] = @event.ProjectId,
                ["title"] = @event.Title,
                ["description"] = @event.Description,
                ["priority"] = @event.Priority.ToString()
            }
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ResponsibilityAssigned @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "ResponsibilityAssigned",
            Timestamp = @event.AssignedAt,
            UserId = @event.AssignedBy,
            Description = $"Responsibility assigned to {(@event.MemberId)}",
            Details = new Dictionary<string, object?>
            {
                ["memberId"] = @event.MemberId,
                ["assignedBy"] = @event.AssignedBy
            }
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ResponsibilityRelinquished @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "ResponsibilityRelinquished",
            Timestamp = @event.RelinquishedAt,
            UserId = @event.RelinquishedBy,
            Description = "Responsibility was relinquished"
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCommenced @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "WorkCommenced",
            Timestamp = @event.CommencedAt,
            UserId = @event.CommencedBy,
            Description = "Work commenced"
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCompleted @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "WorkCompleted",
            Timestamp = @event.CompletedAt,
            UserId = @event.CompletedBy,
            Description = $"Work completed: {@event.Outcome}",
            Details = new Dictionary<string, object?>
            {
                ["outcome"] = @event.Outcome
            }
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemReprioritized @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "WorkItemReprioritized",
            Timestamp = @event.ReprioritizedAt,
            UserId = @event.ReprioritizedBy,
            Description = $"Priority changed from {@event.FormerPriority} to {@event.NewPriority}: {@event.Rationale}",
            Details = new Dictionary<string, object?>
            {
                ["formerPriority"] = @event.FormerPriority.ToString(),
                ["newPriority"] = @event.NewPriority.ToString(),
                ["rationale"] = @event.Rationale
            }
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(DeadlineEstablished @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "DeadlineEstablished",
            Timestamp = @event.EstablishedAt,
            UserId = @event.EstablishedBy,
            Description = $"Deadline set to {@event.Deadline:yyyy-MM-dd}",
            Details = new Dictionary<string, object?>
            {
                ["deadline"] = @event.Deadline
            }
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(DeadlineRemoved @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "DeadlineRemoved",
            Timestamp = @event.RemovedAt,
            UserId = @event.RemovedBy,
            Description = "Deadline was removed"
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromCompletedToInProgress @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "MovedBackFromCompletedToInProgress",
            Timestamp = @event.MovedAt,
            UserId = @event.MovedBy,
            Description = $"Moved back from Completed to In Progress: {@event.Reason}",
            Details = new Dictionary<string, object?>
            {
                ["reason"] = @event.Reason
            }
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromCompletedToPlanned @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "MovedBackFromCompletedToPlanned",
            Timestamp = @event.MovedAt,
            UserId = @event.MovedBy,
            Description = $"Moved back from Completed to Planned: {@event.Reason}",
            Details = new Dictionary<string, object?>
            {
                ["reason"] = @event.Reason
            }
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromInProgressToPlanned @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "MovedBackFromInProgressToPlanned",
            Timestamp = @event.MovedAt,
            UserId = @event.MovedBy,
            Description = $"Moved back from In Progress to Planned: {@event.Reason}",
            Details = new Dictionary<string, object?>
            {
                ["reason"] = @event.Reason
            }
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemRevived @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "WorkItemRevived",
            Timestamp = @event.RevivedAt,
            UserId = @event.RevivedBy,
            Description = $"Work item revived: {@event.Rationale}",
            Details = new Dictionary<string, object?>
            {
                ["rationale"] = @event.Rationale
            }
        });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(FeedbackProvided @event, string workItemId)
    {
        AppendDocument(new AuditLogEntry
        {
            PartitionKey = workItemId,
            WorkItemId = workItemId,
            EventType = "FeedbackProvided",
            Timestamp = @event.ProvidedAt,
            UserId = @event.ProvidedBy,
            Description = $"Feedback provided: {@event.Content}",
            Details = new Dictionary<string, object?>
            {
                ["feedbackId"] = @event.FeedbackId,
                ["content"] = @event.Content
            }
        });
    }
}
