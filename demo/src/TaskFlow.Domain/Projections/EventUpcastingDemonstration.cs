using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.VersionTokenParts;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Events;
using TaskFlow.Domain.Events.Project;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.Project;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Projection specifically for the Event Upcasting demonstration page.
/// Filters to only track specific demo projects that showcase upcasting functionality.
/// Demonstrates filtering projections based on specific aggregate instances.
/// </summary>
[BlobJsonProjection("projections", Connection = "BlobStorage")]
public partial class EventUpcastingDemonstration : Projection
{
    /// <summary>
    /// Projects that demonstrate event upcasting
    /// Key: Project ID, Value: Project summary with completion info
    /// </summary>
    public Dictionary<string, UpcastingDemoProject> DemoProjects { get; } = new();

    /// <summary>
    /// Quick lookup to check if a project ID is part of the demo
    /// </summary>
    private static readonly HashSet<string> TrackedProjectIds = new(DemoProjectIds.AllUpcastingDemoProjects);

    /// <summary>
    /// Called after each When handler to increment the event count for the project.
    /// </summary>
    private void PostWhen(IEvent @event, VersionToken versionToken)
    {
        if (!TrackedProjectIds.Contains(versionToken.ObjectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(versionToken.ObjectId, out var project))
        {
            project.TotalEventCount += 1;
        }
    }

    /// <summary>
    /// Helper method to add an event to the stream summary.
    /// Keeps the first 2 events in EventStreamSummaryStart and always updates EventStreamSummaryLast.
    /// Note: TotalEventCount is incremented automatically by PostWhen after this method runs.
    /// The EventStreamVersion uses the actual event stream version from TotalEventCount.
    /// </summary>
    private void AddEventToSummary(UpcastingDemoProject project, string eventType, DateTime timestamp, string? summary = null)
    {
        // TotalEventCount at this point contains the count before the current event,
        // which is exactly the 0-based version number of the current event
        var eventSummary = new EventSummary
        {
            EventType = eventType,
            Timestamp = timestamp,
            EventStreamVersion = project.TotalEventCount,
            Summary = summary
        };

        // Add to start summary if this is one of the first 2 events
        if (project.EventStreamSummaryStart.Count < 2)
        {
            project.EventStreamSummaryStart.Add(eventSummary);
        }

        // Always update the last event
        project.EventStreamSummaryLast = eventSummary;
    }

    // Project events - only process if projectId is in our tracked set
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectInitiated @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return; // Skip projects not in our demo list
        }

        var project = new UpcastingDemoProject
        {
            ProjectId = projectId,
            Name = @event.Name,
            InitiatedAt = @event.InitiatedAt,
            IsCompleted = false,
            IsLegacyEvent = DemoProjectIds.Legacy.All.Contains(projectId),
            EventType = null,
            Outcome = ProjectOutcome.None,
            TotalEventCount = 0  // Will be updated by Fold
        };

        DemoProjects[projectId] = project;

        // Add first event to summary
        AddEventToSummary(project, "Project.Initiated", @event.InitiatedAt, $"Project '{@event.Name}' initiated");
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectRebranded @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            project.Name = @event.NewName;
            AddEventToSummary(project, "Project.Rebranded", @event.RebrandedAt, $"Project renamed to '{@event.NewName}'");
        }
    }

    // Legacy event handler - this is what gets used when old events are upcasted
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectCompleted @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.CompletedAt;
            project.Outcome = ProjectOutcome.None; // Legacy events don't have a specific outcome
            project.EventType = "Project.Completed";
            project.CompletionMessage = @event.Outcome;
            AddEventToSummary(project, "Project.Completed", @event.CompletedAt, $"Completed: {@event.Outcome}");
        }
    }

    // New specific completion events
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectCompletedSuccessfully @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.CompletedAt;
            project.Outcome = ProjectOutcome.Successful;
            project.EventType = "Project.CompletedSuccessfully";
            project.CompletionMessage = @event.Summary;
            AddEventToSummary(project, "Project.CompletedSuccessfully", @event.CompletedAt, $"Completed successfully: {@event.Summary}");
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectCancelled @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.CancelledAt;
            project.Outcome = ProjectOutcome.Cancelled;
            project.EventType = "Project.Cancelled";
            project.CompletionMessage = @event.Reason;
            AddEventToSummary(project, "Project.Cancelled", @event.CancelledAt, $"Cancelled: {@event.Reason}");
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectFailed @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.FailedAt;
            project.Outcome = ProjectOutcome.Failed;
            project.EventType = "Project.Failed";
            project.CompletionMessage = @event.Reason;
            AddEventToSummary(project, "Project.Failed", @event.FailedAt, $"Failed: {@event.Reason}");
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectDelivered @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.DeliveredAt;
            project.Outcome = ProjectOutcome.Delivered;
            project.EventType = "Project.Delivered";
            project.CompletionMessage = @event.DeliveryNotes;
            AddEventToSummary(project, "Project.Delivered", @event.DeliveredAt, $"Delivered: {@event.DeliveryNotes}");
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectSuspended @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.SuspendedAt;
            project.Outcome = ProjectOutcome.Suspended;
            project.EventType = "Project.Suspended";
            project.CompletionMessage = @event.Reason;
            AddEventToSummary(project, "Project.Suspended", @event.SuspendedAt, $"Suspended: {@event.Reason}");
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectReactivated @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = false;
            project.CompletedAt = null;
            project.Outcome = ProjectOutcome.None;
            project.EventType = null;
            project.CompletionMessage = null;
            AddEventToSummary(project, "Project.Reactivated", @event.ReactivatedAt, "Project reactivated");
        }
    }

    // Team member events - track for accurate event count
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MemberJoinedProject @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            AddEventToSummary(project, "Project.MemberJoinedProject", @event.JoinedAt, $"Team member added");
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MemberLeftProject @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            AddEventToSummary(project, "Project.MemberLeftProject", @event.LeftAt, "Team member removed");
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectMerged @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            AddEventToSummary(project, "Project.ProjectMerged", @event.MergedAt, "Project merged");
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectScopeRefined @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            AddEventToSummary(project, "Project.ProjectScopeRefined", @event.RefinedAt, "Project scope refined");
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemReordered @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            AddEventToSummary(project, "Project.WorkItemReordered", @event.ReorderedAt, "Work item reordered");
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemAddedToProject @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            AddEventToSummary(project, "Project.WorkItemAddedToProject", @event.AddedAt, "Work item added");
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemStatusChangedInProject @event, string projectId)
    {
        if (!TrackedProjectIds.Contains(projectId))
        {
            return;
        }

        if (DemoProjects.TryGetValue(projectId, out var project))
        {
            AddEventToSummary(project, "Project.WorkItemStatusChangedInProject", @event.ChangedAt, "Work item status changed");
        }
    }

    /// <summary>
    /// Get all legacy projects (those using old Project.Completed event)
    /// </summary>
    public IEnumerable<UpcastingDemoProject> GetLegacyProjects()
    {
        return DemoProjects.Values.Where(p => p.IsLegacyEvent);
    }

    /// <summary>
    /// Get all projects using new specific events
    /// </summary>
    public IEnumerable<UpcastingDemoProject> GetNewEventProjects()
    {
        return DemoProjects.Values.Where(p => !p.IsLegacyEvent);
    }

    /// <summary>
    /// Get project by outcome type
    /// </summary>
    public IEnumerable<UpcastingDemoProject> GetByOutcome(ProjectOutcome outcome)
    {
        return DemoProjects.Values.Where(p => p.Outcome == outcome);
    }
}

/// <summary>
/// Summary of a project used in the upcasting demonstration
/// </summary>
public class UpcastingDemoProject
{
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime InitiatedAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ProjectOutcome Outcome { get; set; }

    /// <summary>
    /// The event type that completed the project (e.g., "Project.Completed", "Project.CompletedSuccessfully")
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Message or reason for completion
    /// </summary>
    public string? CompletionMessage { get; set; }

    /// <summary>
    /// True if this project uses the legacy Project.Completed event
    /// </summary>
    public bool IsLegacyEvent { get; set; }

    /// <summary>
    /// First 2 events in the stream for visualization
    /// </summary>
    public List<EventSummary> EventStreamSummaryStart { get; set; } = new();

    /// <summary>
    /// Last event in the stream for visualization
    /// </summary>
    public EventSummary? EventStreamSummaryLast { get; set; }

    /// <summary>
    /// Total number of events in the stream (for calculating the gap)
    /// </summary>
    public int TotalEventCount { get; set; }
}

/// <summary>
/// Summary of an event in the stream for visualization
/// </summary>
public class EventSummary
{
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int EventStreamVersion { get; set; }
    public string? Summary { get; set; }
}
