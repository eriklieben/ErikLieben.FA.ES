using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.Api.DTOs;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Helpers;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Projections;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Api.Endpoints;

public static class EpicEndpoints
{
    public static RouteGroupBuilder MapEpicEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/epics")
            .WithTags("Epics")
            .WithDescription("Epic management endpoints - demonstrates Azure Table Storage backend");

        group.MapPost("/", CreateEpic)
            .WithName("CreateEpic")
            .WithSummary("Create a new epic");

        group.MapPut("/{id}/rename", RenameEpic)
            .WithName("RenameEpic")
            .WithSummary("Rename an epic");

        group.MapPut("/{id}/description", UpdateDescription)
            .WithName("UpdateEpicDescription")
            .WithSummary("Update epic description");

        group.MapPost("/{id}/projects", AddProjectToEpic)
            .WithName("AddProjectToEpic")
            .WithSummary("Add a project to the epic");

        group.MapDelete("/{id}/projects/{projectId}", RemoveProjectFromEpic)
            .WithName("RemoveProjectFromEpic")
            .WithSummary("Remove a project from the epic");

        group.MapPut("/{id}/target-date", ChangeTargetDate)
            .WithName("ChangeEpicTargetDate")
            .WithSummary("Change the epic target completion date");

        group.MapPut("/{id}/priority", ChangePriority)
            .WithName("ChangeEpicPriority")
            .WithSummary("Change the epic priority");

        group.MapPost("/{id}/complete", CompleteEpic)
            .WithName("CompleteEpic")
            .WithSummary("Complete the epic");

        group.MapGet("/{id}", GetEpic)
            .WithName("GetEpic")
            .WithSummary("Get epic details");

        group.MapGet("/", ListEpics)
            .WithName("ListEpics")
            .WithSummary("List all epics");

        return group;
    }

    private static async Task<IResult> CreateEpic(
        [FromBody] CreateEpicRequest request,
        [FromServices] IEpicFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var (initiatorId, initiatorToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var (result, epic) = await factory.CreateEpicAsync(
            request.Name,
            request.Description,
            UserProfileId.From(request.OwnerId),
            request.TargetCompletionDate,
            initiatorToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("EpicCreated", new
        {
            epicId = epic!.Metadata!.Id.Value.ToString(),
            name = request.Name
        });

        return Results.Created($"/api/epics/{epic!.Metadata!.Id.Value}",
            new CommandResult(true, "Epic created successfully", epic.Metadata.Id.Value.ToString()));
    }

    private static async Task<IResult> RenameEpic(
        string id,
        [FromBody] RenameEpicRequest request,
        [FromServices] IEpicFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var epic = await factory.GetAsync(EpicId.From(id));

        var userId = UserProfileId.From("api-user");
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var result = await epic.RenameEpic(request.NewName, userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.Group($"epic-{id}")
            .SendAsync("EpicRenamed", new { epicId = id, newName = request.NewName });

        return Results.Ok(new CommandResult(true, "Epic renamed successfully"));
    }

    private static async Task<IResult> UpdateDescription(
        string id,
        [FromBody] UpdateEpicDescriptionRequest request,
        [FromServices] IEpicFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory)
    {
        var epic = await factory.GetAsync(EpicId.From(id));

        var userId = UserProfileId.From("api-user");
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var result = await epic.UpdateDescription(request.NewDescription, userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        return Results.Ok(new CommandResult(true, "Epic description updated successfully"));
    }

    private static async Task<IResult> AddProjectToEpic(
        string id,
        [FromBody] AddProjectToEpicRequest request,
        [FromServices] IEpicFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var epic = await factory.GetAsync(EpicId.From(id));

        var userId = UserProfileId.From("api-user");
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var result = await epic.AddProject(ProjectId.From(request.ProjectId), userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.Group($"epic-{id}")
            .SendAsync("ProjectAddedToEpic", new { epicId = id, projectId = request.ProjectId });

        return Results.Ok(new CommandResult(true, "Project added to epic successfully"));
    }

    private static async Task<IResult> RemoveProjectFromEpic(
        string id,
        string projectId,
        [FromServices] IEpicFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var epic = await factory.GetAsync(EpicId.From(id));

        var userId = UserProfileId.From("api-user");
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var result = await epic.RemoveProject(ProjectId.From(projectId), userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.Group($"epic-{id}")
            .SendAsync("ProjectRemovedFromEpic", new { epicId = id, projectId });

        return Results.Ok(new CommandResult(true, "Project removed from epic successfully"));
    }

    private static async Task<IResult> ChangeTargetDate(
        string id,
        [FromBody] ChangeEpicTargetDateRequest request,
        [FromServices] IEpicFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory)
    {
        var epic = await factory.GetAsync(EpicId.From(id));

        var userId = UserProfileId.From("api-user");
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var result = await epic.ChangeTargetDate(request.NewTargetDate, userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        return Results.Ok(new CommandResult(true, "Epic target date changed successfully"));
    }

    private static async Task<IResult> ChangePriority(
        string id,
        [FromBody] ChangeEpicPriorityRequest request,
        [FromServices] IEpicFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory)
    {
        var epic = await factory.GetAsync(EpicId.From(id));

        var userId = UserProfileId.From("api-user");
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var result = await epic.ChangePriority(request.NewPriority, userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        return Results.Ok(new CommandResult(true, "Epic priority changed successfully"));
    }

    private static async Task<IResult> CompleteEpic(
        string id,
        [FromBody] CompleteEpicRequest request,
        [FromServices] IEpicFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var epic = await factory.GetAsync(EpicId.From(id));

        var userId = UserProfileId.From("api-user");
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var result = await epic.CompleteEpic(request.Summary, userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("EpicCompleted", new { epicId = id });

        return Results.Ok(new CommandResult(true, "Epic completed successfully"));
    }

    private static async Task<IResult> GetEpic(
        string id,
        [FromServices] IEpicFactory factory)
    {
        try
        {
            var epic = await factory.GetAsync(EpicId.From(id));

            return Results.Ok(new EpicDto(
                epic.Metadata!.Id.Value.ToString(),
                epic.Name ?? string.Empty,
                epic.Description ?? string.Empty,
                epic.OwnerId?.Value ?? string.Empty,
                epic.TargetCompletionDate,
                epic.CreatedAt,
                epic.IsCompleted,
                epic.Priority,
                epic.Projects.Select(p => p.Value.ToString()).ToList(),
                epic.Metadata.VersionInStream));
        }
        catch (Exception)
        {
            return Results.NotFound(new CommandResult(false, $"Epic {id} not found"));
        }
    }

    private static Task<IResult> ListEpics(
        [FromServices] IProjectionService projectionService)
    {
        var projection = projectionService.GetEpicSummary();
        var epics = projection.GetAllEpics()
            .Select(e => new EpicListDto(
                e.EpicId,
                e.Name,
                e.OwnerId,
                e.IsCompleted,
                e.Priority,
                e.ProjectCount,
                e.TargetCompletionDate))
            .ToList();

        return Task.FromResult(Results.Ok(epics));
    }
}
