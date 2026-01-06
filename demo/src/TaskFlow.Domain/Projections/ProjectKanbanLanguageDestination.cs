using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events.WorkItem;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Destination projection representing a Kanban board for a single project in a specific language.
/// Each language destination is stored as a separate JSON file (e.g., kanban/{projectId}/nl-NL.json).
/// This is a full copy of ProjectKanbanDestination with translated titles.
/// The language code is passed via LanguageContext when routing events.
/// </summary>
[BlobJsonProjection("projections/kanban/{projectId}/{languageCode}.json", Connection = "BlobStorage")]
public partial class ProjectKanbanLanguageDestination : Projection
{
    public Dictionary<string, WorkItemCard> WorkItems { get; } = new();

    /// <summary>
    /// The project ID this Kanban board belongs to.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId, LanguageContext? context)
    {
        // Set ProjectId from context or event
        if (string.IsNullOrEmpty(ProjectId))
        {
            ProjectId = context?.ProjectId ?? @event.ProjectId;
        }

        // Get the translated title for this language, or fall back to English
        var title = @event.Title; // Default to English
        if (context?.LanguageCode != null && @event.TitleTranslations?.TryGetValue(context.LanguageCode, out var translatedTitle) == true)
        {
            title = translatedTitle;
        }

        WorkItems[workItemId] = new WorkItemCard
        {
            Id = workItemId,
            Title = title,
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
}
