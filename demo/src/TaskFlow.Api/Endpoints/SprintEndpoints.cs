using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.Api.DTOs;
using TaskFlow.Api.Helpers;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Projections;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Api.Endpoints;

public static class SprintEndpoints
{
    public static RouteGroupBuilder MapSprintEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/sprints")
            .WithTags("Sprints")
            .WithDescription("Sprint management endpoints for planning and tracking iterations");

        group.MapPost("/", CreateSprint)
            .WithName("CreateSprint")
            .WithSummary("Create a new sprint");

        group.MapGet("/", ListSprints)
            .WithName("ListSprints")
            .WithSummary("List all sprints");

        group.MapGet("/statistics", GetSprintStatistics)
            .WithName("GetSprintStatistics")
            .WithSummary("Get sprint statistics");

        group.MapGet("/active", GetActiveSprints)
            .WithName("GetActiveSprints")
            .WithSummary("Get all active sprints");

        group.MapGet("/planned", GetPlannedSprints)
            .WithName("GetPlannedSprints")
            .WithSummary("Get all planned sprints");

        group.MapGet("/project/{projectId}", GetSprintsByProject)
            .WithName("GetSprintsByProject")
            .WithSummary("Get sprints for a specific project");

        group.MapGet("/{id}", GetSprint)
            .WithName("GetSprint")
            .WithSummary("Get sprint details");

        group.MapPost("/{id}/start", StartSprint)
            .WithName("StartSprint")
            .WithSummary("Start a planned sprint");

        group.MapPost("/{id}/complete", CompleteSprint)
            .WithName("CompleteSprint")
            .WithSummary("Complete an active sprint");

        group.MapPost("/{id}/cancel", CancelSprint)
            .WithName("CancelSprint")
            .WithSummary("Cancel a sprint");

        group.MapPut("/{id}/goal", UpdateSprintGoal)
            .WithName("UpdateSprintGoal")
            .WithSummary("Update the sprint goal");

        group.MapPut("/{id}/dates", ChangeSprintDates)
            .WithName("ChangeSprintDates")
            .WithSummary("Change sprint start/end dates");

        group.MapPost("/{id}/work-items", AddWorkItemToSprint)
            .WithName("AddWorkItemToSprint")
            .WithSummary("Add a work item to the sprint");

        group.MapDelete("/{id}/work-items/{workItemId}", RemoveWorkItemFromSprint)
            .WithName("RemoveWorkItemFromSprint")
            .WithSummary("Remove a work item from the sprint");

        return group;
    }

    private static async Task<IResult> CreateSprint(
        [FromBody] CreateSprintRequest request,
        [FromServices] ISprintFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var (initiatorId, initiatorToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var (result, sprint) = await factory.CreateSprintAsync(
            request.Name,
            ProjectId.From(request.ProjectId),
            request.StartDate,
            request.EndDate,
            request.Goal,
            initiatorId,
            initiatorToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("SprintCreated", new
        {
            sprintId = sprint!.Metadata!.Id.Value.ToString(),
            name = request.Name,
            projectId = request.ProjectId
        });

        return Results.Created($"/api/sprints/{sprint.Metadata.Id.Value}",
            new CommandResult(true, "Sprint created successfully", sprint.Metadata.Id.Value.ToString()));
    }

    private static async Task<IResult> ListSprints(
        [FromServices] ISprintDashboardFactory dashboardFactory)
    {
        var dashboard = await dashboardFactory.GetAsync();
        var sprints = dashboard.GetAllSprints()
            .Select(s => new SprintListDto(
                s.SprintId,
                s.Name,
                s.ProjectId,
                s.StartDate,
                s.EndDate,
                s.Status,
                s.WorkItemCount))
            .ToList();

        return Results.Ok(sprints);
    }

    private static async Task<IResult> GetSprintStatistics(
        [FromServices] ISprintDashboardFactory dashboardFactory)
    {
        var dashboard = await dashboardFactory.GetAsync();
        var stats = dashboard.GetStatistics();

        return Results.Ok(new SprintStatisticsDto(
            stats.TotalSprints,
            stats.PlannedSprints,
            stats.ActiveSprints,
            stats.CompletedSprints,
            stats.CancelledSprints,
            stats.TotalWorkItems,
            stats.AverageWorkItemsPerSprint,
            stats.CompletionRate));
    }

    private static async Task<IResult> GetActiveSprints(
        [FromServices] ISprintDashboardFactory dashboardFactory)
    {
        var dashboard = await dashboardFactory.GetAsync();
        var sprints = dashboard.GetActiveSprints()
            .Select(s => new SprintListDto(
                s.SprintId,
                s.Name,
                s.ProjectId,
                s.StartDate,
                s.EndDate,
                s.Status,
                s.WorkItemCount))
            .ToList();

        return Results.Ok(sprints);
    }

    private static async Task<IResult> GetPlannedSprints(
        [FromServices] ISprintDashboardFactory dashboardFactory)
    {
        var dashboard = await dashboardFactory.GetAsync();
        var sprints = dashboard.GetPlannedSprints()
            .Select(s => new SprintListDto(
                s.SprintId,
                s.Name,
                s.ProjectId,
                s.StartDate,
                s.EndDate,
                s.Status,
                s.WorkItemCount))
            .ToList();

        return Results.Ok(sprints);
    }

    private static async Task<IResult> GetSprintsByProject(
        string projectId,
        [FromServices] ISprintDashboardFactory dashboardFactory)
    {
        var dashboard = await dashboardFactory.GetAsync();
        var sprints = dashboard.GetSprintsByProject(projectId)
            .Select(s => new SprintListDto(
                s.SprintId,
                s.Name,
                s.ProjectId,
                s.StartDate,
                s.EndDate,
                s.Status,
                s.WorkItemCount))
            .ToList();

        return Results.Ok(sprints);
    }

    private static async Task<IResult> GetSprint(
        string id,
        [FromServices] ISprintDashboardFactory dashboardFactory)
    {
        var dashboard = await dashboardFactory.GetAsync();
        var sprint = dashboard.GetSprintById(id);

        if (sprint == null)
        {
            return Results.NotFound(new CommandResult(false, "Sprint not found"));
        }

        return Results.Ok(new SprintDto(
            sprint.SprintId,
            sprint.Name,
            sprint.ProjectId,
            sprint.StartDate,
            sprint.EndDate,
            sprint.Goal,
            sprint.Status,
            sprint.CreatedBy,
            sprint.CreatedAt,
            sprint.WorkItemCount,
            sprint.WorkItemIds,
            sprint.DurationDays,
            sprint.DaysRemaining,
            sprint.IsOverdue));
    }

    private static async Task<IResult> StartSprint(
        string id,
        [FromServices] ISprintFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var sprint = await factory.GetAsync(SprintId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await sprint.StartSprint(userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("SprintStarted", new { sprintId = id });

        return Results.Ok(new CommandResult(true, "Sprint started successfully"));
    }

    private static async Task<IResult> CompleteSprint(
        string id,
        [FromBody] CompleteSprintRequest request,
        [FromServices] ISprintFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var sprint = await factory.GetAsync(SprintId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await sprint.CompleteSprint(userId, request.Summary, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("SprintCompleted", new { sprintId = id });

        return Results.Ok(new CommandResult(true, "Sprint completed successfully"));
    }

    private static async Task<IResult> CancelSprint(
        string id,
        [FromBody] CancelSprintRequest request,
        [FromServices] ISprintFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var sprint = await factory.GetAsync(SprintId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await sprint.CancelSprint(userId, request.Reason, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("SprintCancelled", new { sprintId = id });

        return Results.Ok(new CommandResult(true, "Sprint cancelled"));
    }

    private static async Task<IResult> UpdateSprintGoal(
        string id,
        [FromBody] UpdateSprintGoalRequest request,
        [FromServices] ISprintFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService)
    {
        var sprint = await factory.GetAsync(SprintId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await sprint.UpdateGoal(request.NewGoal, userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        return Results.Ok(new CommandResult(true, "Sprint goal updated"));
    }

    private static async Task<IResult> ChangeSprintDates(
        string id,
        [FromBody] ChangeSprintDatesRequest request,
        [FromServices] ISprintFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService)
    {
        var sprint = await factory.GetAsync(SprintId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await sprint.ChangeDates(request.NewStartDate, request.NewEndDate, userId, request.Reason, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        return Results.Ok(new CommandResult(true, "Sprint dates changed"));
    }

    private static async Task<IResult> AddWorkItemToSprint(
        string id,
        [FromBody] AddWorkItemToSprintRequest request,
        [FromServices] ISprintFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var sprint = await factory.GetAsync(SprintId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await sprint.AddWorkItem(WorkItemId.From(request.WorkItemId), userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("WorkItemAddedToSprint", new
        {
            sprintId = id,
            workItemId = request.WorkItemId
        });

        return Results.Ok(new CommandResult(true, "Work item added to sprint"));
    }

    private static async Task<IResult> RemoveWorkItemFromSprint(
        string id,
        string workItemId,
        [FromQuery] string? reason,
        [FromServices] ISprintFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var sprint = await factory.GetAsync(SprintId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await sprint.RemoveWorkItem(WorkItemId.From(workItemId), userId, reason, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("WorkItemRemovedFromSprint", new
        {
            sprintId = id,
            workItemId
        });

        return Results.Ok(new CommandResult(true, "Work item removed from sprint"));
    }
}
