using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events;
using TaskFlow.Domain.Events.Project;
using TaskFlow.Domain.Events.WorkItem;
using TaskFlow.Domain.Projections.Model;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.Project;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Projection that provides dashboard metrics and KPIs for each project
/// Demonstrates aggregating statistics from multiple event streams
/// </summary>
[ProjectionWithExternalCheckpoint]
[BlobJsonProjection("projections", Connection = "BlobStorage")]
public partial class ProjectDashboard : Projection
{
    public Dictionary<string, ProjectMetrics> Projects { get; } = new();

    /// <summary>
    /// Team member summaries across all projects
    /// Demonstrates deeply nested complex types for JSON serialization
    /// </summary>
    public List<TeamMemberSummary> AllTeamMembers { get; } = [];

    // Project events
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectInitiated @event, string projectId)
    {


        Projects[projectId] = new ProjectMetrics
        {
            ProjectId = projectId,
            Name = @event.Name,
            OwnerId = @event.OwnerId,
            InitiatedAt = @event.InitiatedAt,
            IsCompleted = false
        };
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectRebranded @event, string projectId)
    {


        if (Projects.TryGetValue(projectId, out var project))
        {
            project.Name = @event.NewName;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectCompleted @event, string projectId)
    {
        // Legacy event handler - for backwards compatibility
        // Note: Outcome stays as None because we don't know the specific outcome from legacy data
        if (Projects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.CompletedAt;
            project.Outcome = ProjectOutcome.None;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectCompletedSuccessfully @event, string projectId)
    {
        if (Projects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.CompletedAt;
            project.Outcome = ProjectOutcome.Successful;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectCancelled @event, string projectId)
    {
        if (Projects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.CancelledAt;
            project.Outcome = ProjectOutcome.Cancelled;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectFailed @event, string projectId)
    {
        if (Projects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.FailedAt;
            project.Outcome = ProjectOutcome.Failed;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectDelivered @event, string projectId)
    {
        if (Projects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.DeliveredAt;
            project.Outcome = ProjectOutcome.Delivered;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectSuspended @event, string projectId)
    {
        if (Projects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.SuspendedAt;
            project.Outcome = ProjectOutcome.Suspended;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectMerged @event, string projectId)
    {
        if (Projects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.MergedAt;
            project.Outcome = ProjectOutcome.Merged;
            project.MergedIntoProjectId = @event.TargetProjectId;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectReactivated @event, string projectId)
    {
        if (Projects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = false;
            project.CompletedAt = null;
            project.Outcome = ProjectOutcome.None;
            project.MergedIntoProjectId = null;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MemberJoinedProject @event, string projectId)
    {


        if (Projects.TryGetValue(projectId, out var project))
        {
            project.TeamMemberCount++;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MemberLeftProject @event, string projectId)
    {


        if (Projects.TryGetValue(projectId, out var project))
        {
            project.TeamMemberCount--;
        }
    }

    // Work item events
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {


        if (Projects.TryGetValue(@event.ProjectId, out var project))
        {
            project.TotalWorkItems++;
            project.PlannedWorkItems++;

            // Track by priority
            switch (@event.Priority)
            {
                case WorkItemPriority.Low:
                    project.LowPriorityCount++;
                    break;
                case WorkItemPriority.Medium:
                    project.MediumPriorityCount++;
                    break;
                case WorkItemPriority.High:
                    project.HighPriorityCount++;
                    break;
                case WorkItemPriority.Critical:
                    project.CriticalPriorityCount++;
                    break;
            }

            // Track which project this work item belongs to
            WorkItemProjects[workItemId] = @event.ProjectId;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCommenced @event, string workItemId)
    {


        // Find which project this work item belongs to
        if (WorkItemProjects.TryGetValue(workItemId, out var projectId))
        {
            if (Projects.TryGetValue(projectId, out var project))
            {
                project.PlannedWorkItems--;
                project.InProgressWorkItems++;
            }
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCompleted @event, string workItemId)
    {


        if (WorkItemProjects.TryGetValue(workItemId, out var projectId))
        {
            if (Projects.TryGetValue(projectId, out var project))
            {
                project.InProgressWorkItems--;
                project.CompletedWorkItems++;
            }
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemRevived @event, string workItemId)
    {


        if (WorkItemProjects.TryGetValue(workItemId, out var projectId))
        {
            if (Projects.TryGetValue(projectId, out var project))
            {
                project.CompletedWorkItems--;
                project.PlannedWorkItems++;
            }
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemReordered @event, string projectId)
    {
        if (Projects.TryGetValue(projectId, out var project))
        {
            var targetList = @event.Status switch
            {
                WorkItemStatus.Planned => project.PlannedItemsOrder,
                WorkItemStatus.InProgress => project.InProgressItemsOrder,
                WorkItemStatus.Completed => project.CompletedItemsOrder,
                _ => null
            };

            if (targetList != null)
            {
                // Remove the item from its current position if it exists
                targetList.Remove(@event.WorkItemId);

                // Insert at the new position
                var insertIndex = Math.Min(@event.NewPosition, targetList.Count);
                targetList.Insert(insertIndex, @event.WorkItemId);
            }
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemRelocated @event, string workItemId)
    {


        // Update from old project
        if (Projects.TryGetValue(@event.FormerProjectId, out var oldProject))
        {
            oldProject.TotalWorkItems--;
        }

        // Update to new project
        if (Projects.TryGetValue(@event.NewProjectId, out var newProject))
        {
            newProject.TotalWorkItems++;
        }

        // Update tracking dictionary
        WorkItemProjects[workItemId] = @event.NewProjectId;
    }

    // Helper dictionary to track which project each work item belongs to
    private Dictionary<string, string> WorkItemProjects { get; } = new();

    /// <summary>
    /// Get metrics for a specific project
    /// </summary>
    public ProjectMetrics? GetProjectMetrics(string projectId)
    {
        return Projects.TryGetValue(projectId, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// Get all active (non-completed) projects
    /// </summary>
    public IEnumerable<ProjectMetrics> GetActiveProjects()
    {
        return Projects.Values.Where(p => !p.IsCompleted);
    }

    /// <summary>
    /// Get projects with overdue work items
    /// </summary>
    public IEnumerable<ProjectMetrics> GetProjectsWithIssues()
    {
        return Projects.Values.Where(p =>
            !p.IsCompleted &&
            (p.CriticalPriorityCount > 0 || p.InProgressWorkItems == 0 && p.PlannedWorkItems > 0));
    }
}

/// <summary>
/// Metrics and KPIs for a project
/// </summary>
public class ProjectMetrics
{
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TeamMemberCount { get; set; }

    // Outcome tracking
    public ProjectOutcome Outcome { get; set; } = ProjectOutcome.None;
    public string? MergedIntoProjectId { get; set; }  // Only set when Outcome is Merged

    // Work item metrics
    public int TotalWorkItems { get; set; }
    public int PlannedWorkItems { get; set; }
    public int InProgressWorkItems { get; set; }
    public int CompletedWorkItems { get; set; }

    // Priority breakdown
    public int LowPriorityCount { get; set; }
    public int MediumPriorityCount { get; set; }
    public int HighPriorityCount { get; set; }
    public int CriticalPriorityCount { get; set; }

    // Calculated properties
    public double CompletionPercentage =>
        TotalWorkItems > 0 ? (double)CompletedWorkItems / TotalWorkItems * 100 : 0;

    public double InProgressPercentage =>
        TotalWorkItems > 0 ? (double)InProgressWorkItems / TotalWorkItems * 100 : 0;

    // Kanban board item ordering (per status column)
    public List<string> PlannedItemsOrder { get; set; } = new();
    public List<string> InProgressItemsOrder { get; set; } = new();
    public List<string> CompletedItemsOrder { get; set; } = new();
}
