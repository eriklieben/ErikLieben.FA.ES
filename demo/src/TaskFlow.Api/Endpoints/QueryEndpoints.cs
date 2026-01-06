using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Services;
using TaskFlow.Api.DTOs;

namespace TaskFlow.Api.Endpoints;

/// <summary>
/// Query endpoints that use projections for fast read access
/// Demonstrates CQRS pattern with separate read models
/// </summary>
public static class QueryEndpoints
{
    public static RouteGroupBuilder MapQueryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/queries")
            .WithTags("Queries (CQRS Read Models)")
            .WithDescription("Query endpoints using projections for fast read access - demonstrates CQRS pattern");

        group.MapGet("/projects", GetAllProjects)
            .WithName("QueryAllProjects")
            .WithSummary("Get all projects with metrics from ProjectDashboard projection");

        group.MapGet("/projects/{id}/metrics", GetProjectMetrics)
            .WithName("QueryProjectMetrics")
            .WithSummary("Get detailed metrics for a specific project");

        group.MapGet("/projects/{id}/kanban-order", GetProjectKanbanOrder)
            .WithName("QueryProjectKanbanOrder")
            .WithSummary("Get the kanban board item order for a specific project");

        group.MapGet("/projects/{id}/available-languages", GetProjectAvailableLanguages)
            .WithName("QueryProjectAvailableLanguages")
            .WithSummary("Get available languages for a project's kanban board");

        group.MapGet("/projects/{id}/kanban/{languageCode}", GetProjectKanbanByLanguage)
            .WithName("QueryProjectKanbanByLanguage")
            .WithSummary("Get kanban board work items in a specific language");

        group.MapGet("/projects/active", GetActiveProjects)
            .WithName("QueryActiveProjects")
            .WithSummary("Get all active (non-completed) projects");

        group.MapGet("/workitems/active", GetActiveWorkItems)
            .WithName("QueryActiveWorkItems")
            .WithSummary("Get all active work items from ActiveWorkItems projection");

        group.MapGet("/workitems/active/by-project/{projectId}", GetActiveWorkItemsByProject)
            .WithName("QueryActiveWorkItemsByProject")
            .WithSummary("Get active work items for a specific project");

        group.MapGet("/workitems/active/by-assignee/{memberId}", GetActiveWorkItemsByAssignee)
            .WithName("QueryActiveWorkItemsByAssignee")
            .WithSummary("Get work items assigned to a specific member");

        group.MapGet("/workitems/overdue", GetOverdueWorkItems)
            .WithName("QueryOverdueWorkItems")
            .WithSummary("Get all overdue work items");

        return group;
    }

    private static async Task<IResult> GetAllProjects(
        [FromServices] IProjectionService projectionService,
        [FromServices] IWebHostEnvironment environment)
    {
        var dashboard = await GetProjectDashboardFromJson(environment, projectionService);
        var projects = dashboard.Projects.Values.Select(p => new
        {
            projectId = p.ProjectId,
            name = p.Name,
            ownerId = p.OwnerId,
            isCompleted = p.IsCompleted,
            initiatedAt = p.InitiatedAt,
            completedAt = p.CompletedAt,
            teamMemberCount = p.TeamMemberCount,
            metrics = new
            {
                totalWorkItems = p.TotalWorkItems,
                plannedWorkItems = p.PlannedWorkItems,
                inProgressWorkItems = p.InProgressWorkItems,
                completedWorkItems = p.CompletedWorkItems,
                completionPercentage = p.CompletionPercentage,
                inProgressPercentage = p.InProgressPercentage,
                priorityBreakdown = new
                {
                    low = p.LowPriorityCount,
                    medium = p.MediumPriorityCount,
                    high = p.HighPriorityCount,
                    critical = p.CriticalPriorityCount
                }
            }
        }).ToList();

        return Results.Ok(projects);
    }

    private static async Task<IResult> GetProjectKanbanOrder(
        string id,
        [FromServices] IProjectionService projectionService,
        [FromServices] IWebHostEnvironment environment)
    {
        var dashboard = await GetProjectDashboardFromJson(environment, projectionService);
        var metrics = dashboard.GetProjectMetrics(id);

        if (metrics == null)
        {
            return Results.NotFound(new { message = "Project not found" });
        }

        return Results.Ok(new
        {
            projectId = metrics.ProjectId,
            plannedItemsOrder = metrics.PlannedItemsOrder,
            inProgressItemsOrder = metrics.InProgressItemsOrder,
            completedItemsOrder = metrics.CompletedItemsOrder
        });
    }

    private static Task<IResult> GetProjectAvailableLanguages(
        string id,
        [FromServices] IProjectionService projectionService)
    {
        var kanbanBoard = projectionService.GetProjectKanbanBoard();

        // Try to get the project destination
        if (!kanbanBoard.TryGetDestination<TaskFlow.Domain.Projections.ProjectKanbanDestination>(id, out var destination))
        {
            return Task.FromResult(Results.NotFound(new { message = "Project not found" }));
        }

        return Task.FromResult(Results.Ok(new
        {
            projectId = id,
            availableLanguages = destination.AvailableLanguages,
            defaultLanguage = "en-US"
        }));
    }

    private static Task<IResult> GetProjectKanbanByLanguage(
        string id,
        string languageCode,
        [FromServices] IProjectionService projectionService)
    {
        var kanbanBoard = projectionService.GetProjectKanbanBoard();

        // For en-US (default), use the main destination
        if (languageCode == "en-US")
        {
            if (!kanbanBoard.TryGetDestination<TaskFlow.Domain.Projections.ProjectKanbanDestination>(id, out var mainDestination))
            {
                return Task.FromResult(Results.NotFound(new { message = "Project not found" }));
            }

            return Task.FromResult(Results.Ok(new
            {
                projectId = id,
                languageCode = languageCode,
                workItems = mainDestination.WorkItems.Values.Select(w => new
                {
                    workItemId = w.Id,
                    title = w.Title,
                    status = w.Status,
                    assignedTo = w.AssignedTo
                }).ToList()
            }));
        }

        // For other languages, use the language-specific destination
        var languageDestinationKey = $"{id}_{languageCode}";
        if (!kanbanBoard.TryGetDestination<TaskFlow.Domain.Projections.ProjectKanbanLanguageDestination>(languageDestinationKey, out var langDestination))
        {
            return Task.FromResult(Results.NotFound(new { message = $"Language '{languageCode}' not available for this project" }));
        }

        return Task.FromResult(Results.Ok(new
        {
            projectId = id,
            languageCode = languageCode,
            workItems = langDestination.WorkItems.Values.Select(w => new
            {
                workItemId = w.Id,
                title = w.Title,
                status = w.Status,
                assignedTo = w.AssignedTo
            }).ToList()
        }));
    }

    private static async Task<IResult> GetProjectMetrics(
        string id,
        [FromServices] IProjectionService projectionService,
        [FromServices] IWebHostEnvironment environment)
    {
        var dashboard = await GetProjectDashboardFromJson(environment, projectionService);
        var metrics = dashboard.GetProjectMetrics(id);

        if (metrics == null)
        {
            return Results.NotFound(new { message = "Project not found" });
        }

        return Results.Ok(new
        {
            projectId = metrics.ProjectId,
            name = metrics.Name,
            ownerId = metrics.OwnerId,
            isCompleted = metrics.IsCompleted,
            initiatedAt = metrics.InitiatedAt,
            completedAt = metrics.CompletedAt,
            teamMemberCount = metrics.TeamMemberCount,
            workItemMetrics = new
            {
                total = metrics.TotalWorkItems,
                planned = metrics.PlannedWorkItems,
                inProgress = metrics.InProgressWorkItems,
                completed = metrics.CompletedWorkItems,
                completionPercentage = metrics.CompletionPercentage,
                inProgressPercentage = metrics.InProgressPercentage
            },
            priorityBreakdown = new
            {
                low = metrics.LowPriorityCount,
                medium = metrics.MediumPriorityCount,
                high = metrics.HighPriorityCount,
                critical = metrics.CriticalPriorityCount
            }
        });
    }

    private static async Task<IResult> GetActiveProjects(
        [FromServices] IProjectionService projectionService,
        [FromServices] IWebHostEnvironment environment)
    {
        var dashboard = await GetProjectDashboardFromJson(environment, projectionService);
        var projects = dashboard.GetActiveProjects().Select(p => new
        {
            projectId = p.ProjectId,
            name = p.Name,
            ownerId = p.OwnerId,
            initiatedAt = p.InitiatedAt,
            totalWorkItems = p.TotalWorkItems,
            completionPercentage = p.CompletionPercentage
        }).ToList();

        return Results.Ok(projects);
    }

    private static async Task<IResult> GetActiveWorkItems(
        [FromServices] IProjectionService projectionService,
        [FromServices] IWebHostEnvironment environment)
    {
        var activeWorkItems = await GetActiveWorkItemsFromJson(environment, projectionService);
        var items = activeWorkItems.WorkItems.Values.Select(w => new WorkItemListDto(
            w.WorkItemId,
            w.ProjectId,
            w.Title,
            w.Priority,
            w.Status,
            w.AssignedTo,
            w.Deadline
        )).ToList();

        return Results.Ok(items);
    }

    private static async Task<IResult> GetActiveWorkItemsByProject(
        string projectId,
        [FromServices] IProjectionService projectionService,
        [FromServices] IWebHostEnvironment environment)
    {
        var activeWorkItems = await GetActiveWorkItemsFromJson(environment, projectionService);
        var items = activeWorkItems.GetByProject(projectId).Select(w => new WorkItemListDto(
            w.WorkItemId,
            w.ProjectId,
            w.Title,
            w.Priority,
            w.Status,
            w.AssignedTo,
            w.Deadline
        )).ToList();

        return Results.Ok(items);
    }

    private static async Task<IResult> GetActiveWorkItemsByAssignee(
        string memberId,
        [FromServices] IProjectionService projectionService,
        [FromServices] IWebHostEnvironment environment)
    {
        var activeWorkItems = await GetActiveWorkItemsFromJson(environment, projectionService);
        var items = activeWorkItems.GetByAssignee(memberId).Select(w => new WorkItemListDto(
            w.WorkItemId,
            w.ProjectId,
            w.Title,
            w.Priority,
            w.Status,
            w.AssignedTo,
            w.Deadline
        )).ToList();

        return Results.Ok(items);
    }

    private static async Task<IResult> GetOverdueWorkItems(
        [FromServices] IProjectionService projectionService,
        [FromServices] IWebHostEnvironment environment)
    {
        var activeWorkItems = await GetActiveWorkItemsFromJson(environment, projectionService);
        var items = activeWorkItems.GetOverdue().Select(w => new
        {
            workItemId = w.WorkItemId,
            projectId = w.ProjectId,
            title = w.Title,
            priority = w.Priority,
            status = w.Status,
            assignedTo = w.AssignedTo,
            deadline = w.Deadline,
            daysOverdue = w.Deadline.HasValue
                ? (int)(DateTime.UtcNow - w.Deadline.Value).TotalDays
                : 0
        }).ToList();

        return Results.Ok(items);
    }

    // Helper methods to get projections (now using in-memory projections that are persisted to blob storage)
    private static Task<TaskFlow.Domain.Projections.ProjectDashboard> GetProjectDashboardFromJson(
        IWebHostEnvironment environment,
        IProjectionService projectionService)
    {
        // Use in-memory projection that's updated in real-time and persisted to blob storage
        return Task.FromResult(projectionService.GetProjectDashboard());
    }

    private static Task<TaskFlow.Domain.Projections.ActiveWorkItems> GetActiveWorkItemsFromJson(
        IWebHostEnvironment environment,
        IProjectionService projectionService)
    {
        // Use in-memory projection that's updated in real-time and persisted to blob storage
        return Task.FromResult(projectionService.GetActiveWorkItems());
    }
}
