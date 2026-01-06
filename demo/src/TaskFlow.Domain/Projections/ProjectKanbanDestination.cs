using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events.Project;
using TaskFlow.Domain.Events.WorkItem;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Destination projection representing a Kanban board for a single project.
/// Each destination is stored as a separate JSON file (e.g., kanban/project-123.json).
/// </summary>
[BlobJsonProjection("projections/kanban/{projectId}.json", Connection = "BlobStorage")]
public partial class ProjectKanbanDestination : Projection
{
    public Dictionary<string, WorkItemCard> WorkItems { get; } = new();

    /// <summary>
    /// The project ID this Kanban board belongs to.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// The available languages for this project's work item titles.
    /// Always includes "en-US" (the default). Additional languages are added
    /// when ProjectLanguagesConfigured event is received.
    /// </summary>
    public List<string> AvailableLanguages { get; } = ["en-US"];

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {
        // Set ProjectId from the first event if not already set
        if (string.IsNullOrEmpty(ProjectId))
        {
            ProjectId = @event.ProjectId;
        }

        WorkItems[workItemId] = new WorkItemCard
        {
            Id = workItemId,
            Title = @event.Title,
            Status = "Planned",
            ProjectId = @event.ProjectId
        };
    }


    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ResponsibilityAssigned @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var card))
        {
            card.AssignedTo = @event.MemberId;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    [When<WorkCommenced>]
    private void WhenCommenced(string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var card))
        {
            card.Status = "InProgress";
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    [When<WorkCompleted>]
    private void WhenCompleted(string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var card))
        {
            card.Status = "Completed";
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ResponsibilityRelinquished @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var card))
        {
            card.AssignedTo = null;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromCompletedToInProgress @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var card))
        {
            card.Status = "InProgress";
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromCompletedToPlanned @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var card))
        {
            card.Status = "Planned";
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromInProgressToPlanned @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var card))
        {
            card.Status = "Planned";
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemRelocated @event, string workItemId)
    {
        // If this is the new project, add the work item
        if (@event.NewProjectId == ProjectId)
        {
            // Work item is being moved TO this project
            // Note: We might not have all the details, so we create a basic card
            if (!WorkItems.ContainsKey(workItemId))
            {
                WorkItems[workItemId] = new WorkItemCard
                {
                    Id = workItemId,
                    Title = $"Relocated work item {workItemId}",
                    Status = "Planned", // Default status when relocated
                    ProjectId = ProjectId
                };
            }
        }
        // If this is the old project, remove the work item
        else if (@event.FormerProjectId == ProjectId)
        {
            WorkItems.Remove(workItemId);
        }
    }

    /// <summary>
    /// When project languages are configured, update the available languages list.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectLanguagesConfigured @event, string projectId)
    {
        // Set ProjectId if not already set
        if (string.IsNullOrEmpty(ProjectId))
        {
            ProjectId = projectId;
        }

        // Update available languages
        AvailableLanguages.Clear();
        AvailableLanguages.AddRange(@event.RequiredLanguages);
    }
}

public class WorkItemCard
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
}
