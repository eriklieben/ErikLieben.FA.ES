using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events.Epic;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Projection that provides a summary list of all epics
/// Demonstrates projecting events from Table Storage aggregates
/// </summary>
[ProjectionWithExternalCheckpoint]
[BlobJsonProjection("projections", Connection = "BlobStorage")]
public partial class EpicSummary : Projection
{
    /// <summary>
    /// Dictionary of all epics indexed by their ID
    /// </summary>
    public Dictionary<string, EpicListItem> Epics { get; } = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(EpicCreated @event, string epicId)
    {
        Epics[epicId] = new EpicListItem
        {
            EpicId = epicId,
            Name = @event.Name,
            Description = @event.Description,
            OwnerId = @event.OwnerId,
            Priority = EpicPriority.Medium, // Default priority, can be changed later
            TargetCompletionDate = @event.TargetCompletionDate,
            CreatedAt = @event.CreatedAt,
            IsCompleted = false,
            ProjectCount = 0
        };
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(EpicRenamed @event, string epicId)
    {
        if (Epics.TryGetValue(epicId, out var epic))
        {
            epic.Name = @event.NewName;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(EpicDescriptionUpdated @event, string epicId)
    {
        if (Epics.TryGetValue(epicId, out var epic))
        {
            epic.Description = @event.NewDescription;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectAddedToEpic @event, string epicId)
    {
        if (Epics.TryGetValue(epicId, out var epic))
        {
            epic.ProjectCount++;
            if (!epic.ProjectIds.Contains(@event.ProjectId))
            {
                epic.ProjectIds.Add(@event.ProjectId);
            }
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectRemovedFromEpic @event, string epicId)
    {
        if (Epics.TryGetValue(epicId, out var epic))
        {
            epic.ProjectCount--;
            epic.ProjectIds.Remove(@event.ProjectId);
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(EpicTargetDateChanged @event, string epicId)
    {
        if (Epics.TryGetValue(epicId, out var epic))
        {
            epic.TargetCompletionDate = @event.NewTargetDate;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(EpicPriorityChanged @event, string epicId)
    {
        if (Epics.TryGetValue(epicId, out var epic))
        {
            if (Enum.TryParse<EpicPriority>(@event.NewPriority, out var priority))
            {
                epic.Priority = priority;
            }
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(EpicCompleted @event, string epicId)
    {
        if (Epics.TryGetValue(epicId, out var epic))
        {
            epic.IsCompleted = true;
            epic.CompletedAt = @event.CompletedAt;
            epic.CompletionSummary = @event.Summary;
        }
    }

    /// <summary>
    /// Get all epics as a list
    /// </summary>
    public IEnumerable<EpicListItem> GetAllEpics()
    {
        return Epics.Values.OrderByDescending(e => e.CreatedAt);
    }

    /// <summary>
    /// Get active (non-completed) epics
    /// </summary>
    public IEnumerable<EpicListItem> GetActiveEpics()
    {
        return Epics.Values.Where(e => !e.IsCompleted).OrderByDescending(e => e.CreatedAt);
    }

    /// <summary>
    /// Get epics by priority
    /// </summary>
    public IEnumerable<EpicListItem> GetEpicsByPriority(EpicPriority priority)
    {
        return Epics.Values.Where(e => e.Priority == priority).OrderByDescending(e => e.CreatedAt);
    }
}

/// <summary>
/// Summary information for an epic in the list view
/// </summary>
public class EpicListItem
{
    public string EpicId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public EpicPriority Priority { get; set; } = EpicPriority.Medium;
    public DateTime? TargetCompletionDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionSummary { get; set; }
    public int ProjectCount { get; set; }
    public List<string> ProjectIds { get; set; } = new();
}
