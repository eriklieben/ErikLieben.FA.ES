using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.CosmosDb;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events.Sprint;
using TaskFlow.Domain.ValueObjects.Sprint;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Projection that provides dashboard metrics and KPIs for all sprints.
/// Demonstrates projecting events from CosmosDB-backed aggregates and storing the projection in CosmosDB.
/// </summary>
[ProjectionWithExternalCheckpoint]
[CosmosDbJsonProjection("projections", Connection = "cosmosdb")]
public partial class SprintDashboard : Projection
{
    /// <summary>
    /// Dictionary of all sprints indexed by their ID
    /// </summary>
    public Dictionary<string, SprintSummary> Sprints { get; } = new();

    /// <summary>
    /// Mapping of work items to their sprint
    /// </summary>
    private Dictionary<string, string> WorkItemToSprintMapping { get; } = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(SprintCreated @event, string sprintId)
    {
        Sprints[sprintId] = new SprintSummary
        {
            SprintId = sprintId,
            Name = @event.Name,
            ProjectId = @event.ProjectId,
            StartDate = @event.StartDate,
            EndDate = @event.EndDate,
            Goal = @event.Goal,
            Status = SprintStatus.Planned,
            CreatedBy = @event.CreatedBy,
            CreatedAt = @event.CreatedAt,
            WorkItemCount = 0
        };
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(SprintStarted @event, string sprintId)
    {
        if (Sprints.TryGetValue(sprintId, out var sprint))
        {
            sprint.Status = SprintStatus.Active;
            sprint.StartedAt = @event.StartedAt;
            sprint.StartedBy = @event.StartedBy;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(SprintCompleted @event, string sprintId)
    {
        if (Sprints.TryGetValue(sprintId, out var sprint))
        {
            sprint.Status = SprintStatus.Completed;
            sprint.CompletedAt = @event.CompletedAt;
            sprint.CompletedBy = @event.CompletedBy;
            sprint.CompletionSummary = @event.Summary;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(SprintCancelled @event, string sprintId)
    {
        if (Sprints.TryGetValue(sprintId, out var sprint))
        {
            sprint.Status = SprintStatus.Cancelled;
            sprint.CancelledAt = @event.CancelledAt;
            sprint.CancelledBy = @event.CancelledBy;
            sprint.CancellationReason = @event.Reason;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemAddedToSprint @event, string sprintId)
    {
        if (Sprints.TryGetValue(sprintId, out var sprint))
        {
            sprint.WorkItemCount++;
            if (!sprint.WorkItemIds.Contains(@event.WorkItemId))
            {
                sprint.WorkItemIds.Add(@event.WorkItemId);
            }
            WorkItemToSprintMapping[@event.WorkItemId] = sprintId;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemRemovedFromSprint @event, string sprintId)
    {
        if (Sprints.TryGetValue(sprintId, out var sprint))
        {
            sprint.WorkItemCount--;
            sprint.WorkItemIds.Remove(@event.WorkItemId);
            WorkItemToSprintMapping.Remove(@event.WorkItemId);
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(SprintGoalUpdated @event, string sprintId)
    {
        if (Sprints.TryGetValue(sprintId, out var sprint))
        {
            sprint.Goal = @event.NewGoal;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(SprintDatesChanged @event, string sprintId)
    {
        if (Sprints.TryGetValue(sprintId, out var sprint))
        {
            sprint.StartDate = @event.NewStartDate;
            sprint.EndDate = @event.NewEndDate;
        }
    }

    /// <summary>
    /// Get all sprints as a list
    /// </summary>
    public IEnumerable<SprintSummary> GetAllSprints()
    {
        return Sprints.Values.OrderByDescending(s => s.CreatedAt);
    }

    /// <summary>
    /// Get sprints by project
    /// </summary>
    public IEnumerable<SprintSummary> GetSprintsByProject(string projectId)
    {
        return Sprints.Values
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.StartDate);
    }

    /// <summary>
    /// Get active (in-progress) sprints
    /// </summary>
    public IEnumerable<SprintSummary> GetActiveSprints()
    {
        return Sprints.Values
            .Where(s => s.Status == SprintStatus.Active)
            .OrderByDescending(s => s.StartDate);
    }

    /// <summary>
    /// Get planned (not yet started) sprints
    /// </summary>
    public IEnumerable<SprintSummary> GetPlannedSprints()
    {
        return Sprints.Values
            .Where(s => s.Status == SprintStatus.Planned)
            .OrderBy(s => s.StartDate);
    }

    /// <summary>
    /// Get completed sprints
    /// </summary>
    public IEnumerable<SprintSummary> GetCompletedSprints()
    {
        return Sprints.Values
            .Where(s => s.Status == SprintStatus.Completed)
            .OrderByDescending(s => s.CompletedAt);
    }

    /// <summary>
    /// Get sprint by ID
    /// </summary>
    public SprintSummary? GetSprintById(string sprintId)
    {
        return Sprints.TryGetValue(sprintId, out var sprint) ? sprint : null;
    }

    /// <summary>
    /// Get the sprint containing a specific work item
    /// </summary>
    public SprintSummary? GetSprintForWorkItem(string workItemId)
    {
        if (WorkItemToSprintMapping.TryGetValue(workItemId, out var sprintId))
        {
            return Sprints.TryGetValue(sprintId, out var sprint) ? sprint : null;
        }
        return null;
    }

    /// <summary>
    /// Get sprint statistics
    /// </summary>
    public SprintStatistics GetStatistics()
    {
        var sprints = Sprints.Values.ToList();
        return new SprintStatistics
        {
            TotalSprints = sprints.Count,
            PlannedSprints = sprints.Count(s => s.Status == SprintStatus.Planned),
            ActiveSprints = sprints.Count(s => s.Status == SprintStatus.Active),
            CompletedSprints = sprints.Count(s => s.Status == SprintStatus.Completed),
            CancelledSprints = sprints.Count(s => s.Status == SprintStatus.Cancelled),
            TotalWorkItems = sprints.Sum(s => s.WorkItemCount),
            AverageWorkItemsPerSprint = sprints.Count > 0
                ? (double)sprints.Sum(s => s.WorkItemCount) / sprints.Count
                : 0
        };
    }
}

/// <summary>
/// Summary information for a sprint
/// </summary>
public class SprintSummary
{
    public string SprintId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Goal { get; set; }
    public SprintStatus Status { get; set; } = SprintStatus.Planned;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public string? StartedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public string? CompletionSummary { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
    public int WorkItemCount { get; set; }
    public List<string> WorkItemIds { get; set; } = new();

    /// <summary>
    /// Duration of the sprint in days
    /// </summary>
    public int DurationDays => (EndDate - StartDate).Days;

    /// <summary>
    /// Days remaining in the sprint (if active)
    /// </summary>
    public int? DaysRemaining => Status == SprintStatus.Active
        ? Math.Max(0, (EndDate - DateTime.UtcNow).Days)
        : null;

    /// <summary>
    /// Whether the sprint is overdue (past end date but not completed)
    /// </summary>
    public bool IsOverdue => Status == SprintStatus.Active && DateTime.UtcNow > EndDate;
}

/// <summary>
/// Sprint statistics across all sprints
/// </summary>
public class SprintStatistics
{
    public int TotalSprints { get; set; }
    public int PlannedSprints { get; set; }
    public int ActiveSprints { get; set; }
    public int CompletedSprints { get; set; }
    public int CancelledSprints { get; set; }
    public int TotalWorkItems { get; set; }
    public double AverageWorkItemsPerSprint { get; set; }

    /// <summary>
    /// Completion rate as a percentage
    /// </summary>
    public double CompletionRate => TotalSprints > 0
        ? (double)CompletedSprints / TotalSprints * 100
        : 0;
}
