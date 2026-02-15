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

public static class ReleaseEndpoints
{
    public static RouteGroupBuilder MapReleaseEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/releases")
            .WithTags("Releases")
            .WithDescription("Release management endpoints for versioned software releases (S3-backed)");

        group.MapPost("/", CreateRelease)
            .WithName("CreateRelease")
            .WithSummary("Create a new release");

        group.MapGet("/", ListReleases)
            .WithName("ListReleases")
            .WithSummary("List all releases");

        group.MapGet("/statistics", GetReleaseStatistics)
            .WithName("GetReleaseStatistics")
            .WithSummary("Get release statistics");

        group.MapGet("/project/{projectId}", GetReleasesByProject)
            .WithName("GetReleasesByProject")
            .WithSummary("Get releases for a specific project");

        group.MapGet("/{id}", GetRelease)
            .WithName("GetRelease")
            .WithSummary("Get release details");

        group.MapPost("/{id}/note", AddNote)
            .WithName("AddReleaseNote")
            .WithSummary("Add a note to a release");

        group.MapPost("/{id}/stage", StageRelease)
            .WithName("StageRelease")
            .WithSummary("Stage a release for deployment");

        group.MapPost("/{id}/deploy", DeployRelease)
            .WithName("DeployRelease")
            .WithSummary("Deploy a staged release");

        group.MapPost("/{id}/complete", CompleteRelease)
            .WithName("CompleteRelease")
            .WithSummary("Complete a deployed release");

        group.MapPost("/{id}/rollback", RollbackRelease)
            .WithName("RollbackRelease")
            .WithSummary("Roll back a release");

        return group;
    }

    private static async Task<IResult> CreateRelease(
        [FromBody] CreateReleaseRequest request,
        [FromServices] IReleaseFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var (initiatorId, initiatorToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var (result, release) = await factory.CreateReleaseAsync(
            request.Name,
            request.Version,
            ProjectId.From(request.ProjectId),
            initiatorId,
            initiatorToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("ReleaseCreated", new
        {
            releaseId = release!.Metadata!.Id.Value.ToString(),
            name = request.Name,
            version = request.Version,
            projectId = request.ProjectId
        });

        return Results.Created($"/api/releases/{release.Metadata.Id.Value}",
            new CommandResult(true, "Release created successfully", release.Metadata.Id.Value.ToString()));
    }

    private static Task<IResult> ListReleases(
        [FromServices] ReleaseDashboard dashboard)
    {
        var releases = dashboard.GetAllReleases()
            .Select(r => new ReleaseListDto(
                r.ReleaseId,
                r.Name,
                r.Version,
                r.ProjectId,
                r.Status,
                r.CreatedAt))
            .ToList();

        return Task.FromResult(Results.Ok(releases));
    }

    private static Task<IResult> GetReleaseStatistics(
        [FromServices] ReleaseDashboard dashboard)
    {
        var stats = dashboard.GetStatistics();

        return Task.FromResult(Results.Ok(new ReleaseStatisticsDto(
            stats.TotalReleases,
            stats.DraftCount,
            stats.StagedCount,
            stats.DeployedCount,
            stats.CompletedCount,
            stats.RolledBackCount,
            stats.CompletionRate,
            stats.RollbackRate)));
    }

    private static Task<IResult> GetReleasesByProject(
        string projectId,
        [FromServices] ReleaseDashboard dashboard)
    {
        var releases = dashboard.GetReleasesByProject(projectId)
            .Select(r => new ReleaseListDto(
                r.ReleaseId,
                r.Name,
                r.Version,
                r.ProjectId,
                r.Status,
                r.CreatedAt))
            .ToList();

        return Task.FromResult(Results.Ok(releases));
    }

    private static Task<IResult> GetRelease(
        string id,
        [FromServices] ReleaseDashboard dashboard)
    {
        var release = dashboard.GetReleaseById(id);

        if (release == null)
        {
            return Task.FromResult(Results.NotFound(new CommandResult(false, "Release not found")));
        }

        return Task.FromResult(Results.Ok(new ReleaseDto(
            release.ReleaseId,
            release.Name,
            release.Version,
            release.ProjectId,
            release.Status,
            release.CreatedBy,
            release.CreatedAt,
            release.StagedBy,
            release.StagedAt,
            release.DeployedBy,
            release.DeployedAt,
            release.CompletedBy,
            release.CompletedAt,
            release.RolledBackBy,
            release.RolledBackAt,
            release.RollbackReason)));
    }

    private static async Task<IResult> AddNote(
        string id,
        [FromBody] AddReleaseNoteRequest request,
        [FromServices] IReleaseFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var release = await factory.GetAsync(ReleaseId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await release.AddNote(request.Note, userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("ReleaseNoteAdded", new { releaseId = id });

        return Results.Ok(new CommandResult(true, "Note added to release"));
    }

    private static async Task<IResult> StageRelease(
        string id,
        [FromServices] IReleaseFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var release = await factory.GetAsync(ReleaseId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await release.Stage(userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("ReleaseStaged", new { releaseId = id });

        return Results.Ok(new CommandResult(true, "Release staged successfully"));
    }

    private static async Task<IResult> DeployRelease(
        string id,
        [FromServices] IReleaseFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var release = await factory.GetAsync(ReleaseId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await release.Deploy(userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("ReleaseDeployed", new { releaseId = id });

        return Results.Ok(new CommandResult(true, "Release deployed successfully"));
    }

    private static async Task<IResult> CompleteRelease(
        string id,
        [FromServices] IReleaseFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var release = await factory.GetAsync(ReleaseId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await release.Complete(userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("ReleaseCompleted", new { releaseId = id });

        return Results.Ok(new CommandResult(true, "Release completed successfully"));
    }

    private static async Task<IResult> RollbackRelease(
        string id,
        [FromBody] RollbackReleaseRequest request,
        [FromServices] IReleaseFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var release = await factory.GetAsync(ReleaseId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await release.Rollback(request.Reason, userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("ReleaseRolledBack", new { releaseId = id });

        return Results.Ok(new CommandResult(true, "Release rolled back"));
    }
}
