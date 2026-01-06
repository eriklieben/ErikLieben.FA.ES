using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.Api.DTOs;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Helpers;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;
using ErikLieben.FA.ES.Aggregates;

namespace TaskFlow.Api.Endpoints;

public static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/projects")
            .WithTags("Projects")
            .WithDescription("Project management endpoints for initiating, updating, and managing projects");

        group.MapPost("/", InitiateProject)
            .WithName("InitiateProject")
            .WithSummary("Initiate a new project");

        group.MapPut("/{id}/rebrand", RebrandProject)
            .WithName("RebrandProject")
            .WithSummary("Rebrand a project with a new name");

        group.MapPut("/{id}/scope", RefineScope)
            .WithName("RefineScope")
            .WithSummary("Refine project scope/description");

        group.MapPost("/{id}/complete-successfully", CompleteProjectSuccessfully)
            .WithName("CompleteProjectSuccessfully")
            .WithSummary("Complete project successfully with all objectives met");

        group.MapPost("/{id}/cancel", CancelProject)
            .WithName("CancelProject")
            .WithSummary("Cancel the project");

        group.MapPost("/{id}/fail", FailProject)
            .WithName("FailProject")
            .WithSummary("Mark project as failed");

        group.MapPost("/{id}/deliver", DeliverProject)
            .WithName("DeliverProject")
            .WithSummary("Deliver project to production/client");

        group.MapPost("/{id}/suspend", SuspendProject)
            .WithName("SuspendProject")
            .WithSummary("Suspend the project");

        group.MapPost("/{id}/merge", MergeProject)
            .WithName("MergeProject")
            .WithSummary("Merge project into another project");

        group.MapPost("/{id}/reactivate", ReactivateProject)
            .WithName("ReactivateProject")
            .WithSummary("Reactivate a completed project");

        group.MapPost("/{id}/team", AddTeamMember)
            .WithName("AddTeamMember")
            .WithSummary("Add a team member to the project");

        group.MapDelete("/{id}/team/{memberId}", RemoveTeamMember)
            .WithName("RemoveTeamMember")
            .WithSummary("Remove a team member from the project");

        group.MapPost("/{id}/reorder-workitem", ReorderWorkItem)
            .WithName("ReorderWorkItem")
            .WithSummary("Reorder a work item within its status column on the kanban board");

        group.MapGet("/{id}", GetProject)
            .WithName("GetProject")
            .WithSummary("Get project details");

        group.MapGet("/", ListProjects)
            .WithName("ListProjects")
            .WithSummary("List all projects");

        return group;
    }

    private static async Task<IResult> InitiateProject(
        [FromBody] InitiateProjectRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var projectId = ProjectId.New();
        var project = await factory.CreateAsync(projectId);

        // Get the initiating user's version token from the current request context
        var (initiatorId, initiatorToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        await project.InitiateProject(
            request.Name,
            request.Description,
            UserProfileId.From(request.OwnerId),
            initiatorToken);

        // Broadcast to all clients
        await hubContext.Clients.All.SendAsync("ProjectInitiated", new
        {
            projectId = projectId.Value.ToString(),
            name = request.Name
        });

        return Results.Created($"/api/projects/{projectId.Value}",
            new CommandResult(true, "Project initiated successfully", projectId.Value.ToString()));
    }

    private static async Task<IResult> RebrandProject(
        string id,
        [FromBody] RebrandProjectRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.RebrandProject(request.NewName, userId, userToken);

        await hubContext.Clients.Group($"project-{id}")
            .SendAsync("ProjectRebranded", new { projectId = id, newName = request.NewName });

        return Results.Ok(new CommandResult(true, "Project rebranded successfully"));
    }

    private static async Task<IResult> RefineScope(
        string id,
        [FromBody] RefineScopeRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.RefineScope(request.NewDescription, userId, userToken);

        return Results.Ok(new CommandResult(true, "Scope refined successfully"));
    }

    private static async Task<IResult> CompleteProjectSuccessfully(
        string id,
        [FromBody] CompleteProjectSuccessfullyRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.CompleteProjectSuccessfully(request.Summary, userId, userToken);

        await hubContext.Clients.Group($"project-{id}")
            .SendAsync("ProjectCompletedSuccessfully", new { projectId = id });

        return Results.Ok(new CommandResult(true, "Project completed successfully"));
    }

    private static async Task<IResult> CancelProject(
        string id,
        [FromBody] CancelProjectRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.CancelProject(request.Reason, userId, userToken);

        await hubContext.Clients.Group($"project-{id}")
            .SendAsync("ProjectCancelled", new { projectId = id });

        return Results.Ok(new CommandResult(true, "Project cancelled"));
    }

    private static async Task<IResult> FailProject(
        string id,
        [FromBody] FailProjectRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.FailProject(request.Reason, userId, userToken);

        await hubContext.Clients.Group($"project-{id}")
            .SendAsync("ProjectFailed", new { projectId = id });

        return Results.Ok(new CommandResult(true, "Project marked as failed"));
    }

    private static async Task<IResult> DeliverProject(
        string id,
        [FromBody] DeliverProjectRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.DeliverProject(request.DeliveryNotes, userId, userToken);

        await hubContext.Clients.Group($"project-{id}")
            .SendAsync("ProjectDelivered", new { projectId = id });

        return Results.Ok(new CommandResult(true, "Project delivered"));
    }

    private static async Task<IResult> SuspendProject(
        string id,
        [FromBody] SuspendProjectRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.SuspendProject(request.Reason, userId, userToken);

        await hubContext.Clients.Group($"project-{id}")
            .SendAsync("ProjectSuspended", new { projectId = id });

        return Results.Ok(new CommandResult(true, "Project suspended"));
    }

    private static async Task<IResult> MergeProject(
        string id,
        [FromBody] MergeProjectRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.MergeProject(request.TargetProjectId, request.Reason, userId, userToken);

        await hubContext.Clients.Group($"project-{id}")
            .SendAsync("ProjectMerged", new { projectId = id, targetProjectId = request.TargetProjectId });

        return Results.Ok(new CommandResult(true, "Project merged"));
    }

    private static async Task<IResult> ReactivateProject(
        string id,
        [FromBody] ReactivateProjectRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.ReactivateProject(request.Rationale, userId, userToken);

        return Results.Ok(new CommandResult(true, "Project reactivated successfully"));
    }

    private static async Task<IResult> AddTeamMember(
        string id,
        [FromBody] AddTeamMemberRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.AddTeamMember(UserProfileId.From(request.MemberId), request.Role, userId, userToken);

        await hubContext.Clients.Group($"project-{id}")
            .SendAsync("TeamMemberAdded", new
            {
                projectId = id,
                memberId = request.MemberId,
                role = request.Role
            });

        return Results.Ok(new CommandResult(true, "Team member added successfully"));
    }

    private static async Task<IResult> RemoveTeamMember(
        string id,
        string memberId,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.RemoveTeamMember(UserProfileId.From(memberId), userId, userToken);

        return Results.Ok(new CommandResult(true, "Team member removed successfully"));
    }

    private static async Task<IResult> ReorderWorkItem(
        string id,
        [FromBody] ReorderWorkItemRequest request,
        [FromServices] IProjectFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        await project.ReorderWorkItem(
            new WorkItemId(Guid.Parse(request.WorkItemId)),
            request.Status,
            request.NewPosition,
            userId,
            userToken);

        await hubContext.BroadcastWorkItemChanged(id, new
        {
            workItemId = request.WorkItemId,
            status = request.Status,
            position = request.NewPosition
        });

        return Results.Ok(new CommandResult(true, "Work item reordered successfully"));
    }

    private static async Task<IResult> GetProject(
        string id,
        [FromServices] IProjectFactory factory)
    {
        var project = await factory.GetAsync(ProjectId.From(id));

        var dto = new ProjectDto(
            id,
            project.Name ?? "",
            project.Description ?? "",
            project.OwnerId?.Value ?? "",
            project.IsCompleted,
            project.TeamMembers.ToDictionary(kvp => kvp.Key.Value, kvp => kvp.Value),
            0);

        return Results.Ok(dto);
    }

    private static async Task<IResult> ListProjects(
        [FromServices] IAggregateFactory factory)
    {
        // For now, return empty list - we'll implement projection-based listing later
        await System.Threading.Tasks.Task.CompletedTask;
        return Results.Ok(Array.Empty<ProjectListDto>());
    }
}
