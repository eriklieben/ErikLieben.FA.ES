using ErikLieben.FA.ES;
using ErikLieben.FA.Results;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// Extension methods for WorkItemFactory
/// </summary>
public partial interface IWorkItemFactory
{
    /// <summary>
    /// Creates a work item and automatically links it to the specified project.
    /// This is a convenience method that combines CreateAsync, PlanTask, and Project.AddWorkItem in one operation.
    /// </summary>
    Task<(Result result, WorkItem? workItem)> CreateAndLinkToProjectAsync(
        ProjectId projectId,
        string title,
        string description,
        WorkItemPriority priority,
        UserProfileId plannedBy,
        IProjectFactory projectFactory,
        VersionToken? plannedByUser = null,
        DateTime? occurredAt = null);
}

/// <summary>
/// Extension implementation for WorkItemFactory
/// </summary>
public partial class WorkItemFactory
{
    /// <summary>
    /// Creates a work item and automatically links it to the specified project.
    /// This is a convenience method that combines CreateAsync, PlanTask, and Project.AddWorkItem in one operation.
    /// </summary>
    public async Task<(Result result, WorkItem? workItem)> CreateAndLinkToProjectAsync(
        ProjectId projectId,
        string title,
        string description,
        WorkItemPriority priority,
        UserProfileId plannedBy,
        IProjectFactory projectFactory,
        VersionToken? plannedByUser = null,
        DateTime? occurredAt = null)
    {
        // Generate new WorkItem ID
        var workItemId = WorkItemId.New();

        // Create the WorkItem aggregate
        var workItem = await CreateAsync(workItemId);

        // Plan the task on the WorkItem
        var planResult = await workItem.PlanTask(
            projectId.Value.ToString(),
            title,
            description,
            priority,
            plannedBy,
            plannedByUser,
            occurredAt);

        if (!planResult.IsSuccess)
        {
            return (planResult, null);
        }

        // Get the Project aggregate
        var project = await projectFactory.GetAsync(projectId);

        // Add the WorkItem to the Project
        var addResult = await project.AddWorkItem(workItemId, plannedBy, plannedByUser, occurredAt);

        if (!addResult.IsSuccess)
        {
            return (addResult, null);
        }

        return (Result.Success(), workItem);
    }
}
