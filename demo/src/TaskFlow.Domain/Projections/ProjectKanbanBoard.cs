using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events.Project;
using TaskFlow.Domain.Events.WorkItem;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Routed projection that manages Kanban boards across multiple projects.
/// Each project gets its own JSON file (kanban/project-{id}.json).
/// The projection itself acts as the router - When methods route events to destinations.
/// </summary>
[BlobJsonProjection("projections/kanban.json", Connection = "BlobStorage")]
[ProjectionWithExternalCheckpoint]
public partial class ProjectKanbanBoard : RoutedProjection
{
    /// <summary>
    /// List of all projects in the Kanban board.
    /// This is stored in the main kanban.json file.
    /// </summary>
    public Dictionary<string, ProjectInfo> Projects { get; } = new();

    /// <summary>
    /// Tracks which work items belong to which projects for routing purposes.
    /// </summary>
    private readonly Dictionary<string, string> workItemToProjectMap = new();

    /// <summary>
    /// Tracks which additional languages (besides en-US) are configured for each project.
    /// </summary>
    private readonly Dictionary<string, List<string>> projectLanguages = new();

    /// <summary>
    /// Routes an event to the project destination and all its language destinations.
    /// Language destinations receive a LanguageContext with the appropriate language code.
    /// </summary>
    private void RouteToProjectAndLanguages(string projectId)
    {
        // Route to main project destination (no language context needed)
        RouteToDestination(projectId);

        // Route to all language destinations for this project with LanguageContext
        // First check the transient cache, then fall back to checking existing destinations
        if (projectLanguages.TryGetValue(projectId, out var languages))
        {
            foreach (var languageCode in languages)
            {
                var languageDestinationKey = $"{projectId}_{languageCode}";
                var context = new LanguageContext(languageCode, projectId);
                RouteToDestination(languageDestinationKey, context);
            }
        }
        else
        {
            // Check existing destinations for language destinations matching this project
            // This handles the case where projection was loaded from storage
            var prefix = $"{projectId}_";
            foreach (var destinationKey in GetDestinationKeys())
            {
                if (destinationKey.StartsWith(prefix))
                {
                    // Extract language code from destination key (format: "{projectId}_{languageCode}")
                    var languageCode = destinationKey.Substring(prefix.Length);
                    var context = new LanguageContext(languageCode, projectId);
                    RouteToDestination(destinationKey, context);
                }
            }
        }
    }

    // When methods - process events at the projection level to track metadata and route to partitions

    /// <summary>
    /// When a project is initiated, create a partition for it and track project info.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectInitiated @event, string projectId)
    {
        // Track project info in the main projection
        Projects[projectId] = new ProjectInfo
        {
            Id = projectId,
            Name = @event.Name,
            Description = @event.Description,
            OwnerId = @event.OwnerId,
            InitiatedAt = @event.InitiatedAt
        };

        // Create destination for the project's work items with projectId metadata for path resolution
        AddDestination<ProjectKanbanDestination>(projectId, new Dictionary<string, string> { ["projectId"] = projectId });
    }

    /// <summary>
    /// When a work item is planned, track the work item -> project mapping and route to all partitions.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {
        // Track which project this work item belongs to
        workItemToProjectMap[workItemId] = @event.ProjectId;

        // Ensure partition exists for this project with projectId metadata
        AddDestination<ProjectKanbanDestination>(@event.ProjectId, new Dictionary<string, string> { ["projectId"] = @event.ProjectId });

        // Route the event to the project's partition and all language partitions
        RouteToProjectAndLanguages(@event.ProjectId);
    }

    /// <summary>
    /// When responsibility is assigned to a work item, route to all partitions.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ResponsibilityAssigned @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToProjectAndLanguages(projectId);
        }
    }

    /// <summary>
    /// When work commences on a work item, route to all partitions.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCommenced @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToProjectAndLanguages(projectId);
        }
    }

    /// <summary>
    /// When work is completed on a work item, route to all partitions.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCompleted @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToProjectAndLanguages(projectId);
        }
    }

    /// <summary>
    /// When responsibility is relinquished, route to all partitions.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ResponsibilityRelinquished @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToProjectAndLanguages(projectId);
        }
    }

    /// <summary>
    /// When a deadline is established, route to the project's partition.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(DeadlineEstablished @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }

    /// <summary>
    /// When a deadline is removed, route to the project's partition.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(DeadlineRemoved @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }

    /// <summary>
    /// When effort is reestimated, route to the project's partition.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(EffortReestimated @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }

    /// <summary>
    /// When feedback is provided, route to the project's partition.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(FeedbackProvided @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }

    /// <summary>
    /// When requirements are refined, route to the project's partition.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(RequirementsRefined @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }

    /// <summary>
    /// When work item is moved back from completed to in progress, route to all partitions.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromCompletedToInProgress @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToProjectAndLanguages(projectId);
        }
    }

    /// <summary>
    /// When work item is moved back from completed to planned, route to all partitions.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromCompletedToPlanned @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToProjectAndLanguages(projectId);
        }
    }

    /// <summary>
    /// When work item is moved back from in progress to planned, route to all partitions.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(MovedBackFromInProgressToPlanned @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToProjectAndLanguages(projectId);
        }
    }

    /// <summary>
    /// When work item is relocated to another project, update mapping and route to both projects and their languages.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemRelocated @event, string workItemId)
    {
        // Route to old project first (to remove from there)
        if (workItemToProjectMap.TryGetValue(workItemId, out var oldProjectId))
        {
            RouteToProjectAndLanguages(oldProjectId);
        }

        // Update mapping to new project
        workItemToProjectMap[workItemId] = @event.NewProjectId;

        // Ensure partition exists for new project with projectId metadata
        AddDestination<ProjectKanbanDestination>(@event.NewProjectId, new Dictionary<string, string> { ["projectId"] = @event.NewProjectId });

        // Route to new project and its languages
        RouteToProjectAndLanguages(@event.NewProjectId);
    }

    /// <summary>
    /// When work item is reprioritized, route to the project's partition.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemReprioritized @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }

    /// <summary>
    /// When work item is retagged, route to the project's partition.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemRetagged @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }

    /// <summary>
    /// When work item is revived, route to the project's partition.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemRevived @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }

    /// <summary>
    /// When drag is marked as accidental, route to the project's partition.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(DragMarkedAsAccidental @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }

    /// <summary>
    /// When project languages are configured, route to the project's partition
    /// and create language-specific destinations for each non-English language.
    /// </summary>
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectLanguagesConfigured @event, string projectId)
    {
        // Ensure the project destination exists
        AddDestination<ProjectKanbanDestination>(projectId, new Dictionary<string, string> { ["projectId"] = projectId });

        // Route the event to update the project's AvailableLanguages
        RouteToDestination(projectId);

        // Track which languages this project supports (excluding en-US which is the default)
        projectLanguages[projectId] = @event.RequiredLanguages.Where(l => l != "en-US").ToList();

        // Create language-specific destinations for each non-English language
        foreach (var languageCode in @event.RequiredLanguages)
        {
            if (languageCode == "en-US") continue; // Skip English - titles are in the main projection

            var languageDestinationKey = $"{projectId}_{languageCode}";
            AddDestination<ProjectKanbanLanguageDestination>(
                languageDestinationKey,
                new Dictionary<string, string>
                {
                    ["projectId"] = projectId,
                    ["languageCode"] = languageCode
                });
        }
    }

}

/// <summary>
/// Information about a project in the Kanban board.
/// </summary>
public class ProjectInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public DateTime InitiatedAt { get; set; }
}
