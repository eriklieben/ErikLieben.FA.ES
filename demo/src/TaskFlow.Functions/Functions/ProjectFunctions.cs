using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Projections;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.Project;
using TaskFlow.Domain.Events.Project;

namespace TaskFlow.Functions.Functions;

/// <summary>
/// Sample Azure Functions demonstrating the EventStream input binding with Project aggregate.
/// These functions show various patterns for working with aggregates in Azure Functions.
/// </summary>
public class ProjectFunctions(ILogger<ProjectFunctions> logger)
{
    /// <summary>
    /// Gets a project by ID using the EventStream input binding.
    /// </summary>
    /// <remarks>
    /// Usage: GET /api/projects/{id}
    /// </remarks>
    [Function(nameof(GetProject))]
    public async Task<HttpResponseData> GetProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{id}")] HttpRequestData req,
        string id,
        [EventStreamInput("{id}")] Project project)
    {
        logger.LogInformation("Getting project {ProjectId}", id);

        if (project.Metadata?.Id == null)
        {
            logger.LogWarning("Project {ProjectId} not found", id);
            var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new
            {
                error = "Project not found",
                id
            });
            return notFoundResponse;
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            id = project.Metadata?.Id?.Value.ToString() ?? id,
            name = project.Name,
            description = project.Description,
            ownerId = project.OwnerId?.Value,
            isCompleted = project.IsCompleted,
            outcome = project.Outcome.ToString(),
            teamMembersCount = project.TeamMembers.Count,
            plannedItemsCount = project.PlannedItemsOrder.Count,
            inProgressItemsCount = project.InProgressItemsOrder.Count,
            completedItemsCount = project.CompletedItemsOrder.Count
        });

        return response;
    }

    /// <summary>
    /// Creates a new project using the factory directly.
    /// Demonstrates creating an aggregate without EventStream binding.
    /// </summary>
    [Function(nameof(CreateProject))]
    [ProjectionOutput<ProjectKanbanBoard>]
    public async Task<HttpResponseData> CreateProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects")] HttpRequestData req,
        [FromServices] IProjectFactory projectFactory)
    {
        var requestBody = await req.ReadFromJsonAsync<CreateProjectRequest>();
        if (requestBody == null || string.IsNullOrWhiteSpace(requestBody.Name))
        {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { error = "Invalid request body. Name is required." });
            return badResponse;
        }

        logger.LogInformation("Creating new project: {Name}", requestBody.Name);

        var projectId = ProjectId.New();
        var project = await projectFactory.CreateAsync(projectId);

        var result = await project.InitiateProject(
            requestBody.Name,
            requestBody.Description ?? string.Empty,
            UserProfileId.From(requestBody.OwnerId ?? "system"));

        if (result.IsFailure)
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = "Failed to create project",
                details = result.Errors.ToArray().Select(e => e.Message)
            });
            return errorResponse;
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.Created);
        response.Headers.Add("Location", $"/api/projects/{projectId.Value}");
        await response.WriteAsJsonAsync(new
        {
            success = true,
            message = "Project created",
            projectId = projectId.Value.ToString()
        });

        return response;
    }

    /// <summary>
    /// Renames a project using the EventStream binding.
    /// Demonstrates modifying an aggregate loaded via binding.
    /// </summary>
    [Function(nameof(RenameProject))]
    [ProjectionOutput<ProjectKanbanBoard>]
    public async Task<HttpResponseData> RenameProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{id}/rename")] HttpRequestData req,
        string id,
        [EventStreamInput("{id}")] Project project)
    {
        var requestBody = await req.ReadFromJsonAsync<RenameProjectRequest>();
        if (requestBody == null || string.IsNullOrWhiteSpace(requestBody.NewName))
        {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { error = "NewName is required" });
            return badResponse;
        }

        if (project.Metadata?.Id == null)
        {
            var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { error = "Project not found", id });
            return notFoundResponse;
        }

        logger.LogInformation("Renaming project {ProjectId} to {NewName}", id, requestBody.NewName);

        var result = await project.RebrandProject(
            requestBody.NewName,
            UserProfileId.From(requestBody.RenamedBy ?? "system"));

        if (result.IsFailure)
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = "Failed to rename project",
                details = result.Errors.ToArray().Select(e => e.Message)
            });
            return errorResponse;
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            success = true,
            message = $"Project renamed to {requestBody.NewName}",
            projectId = id
        });

        return response;
    }

    /// <summary>
    /// Adds a team member to a project.
    /// Demonstrates aggregate modification with validation.
    /// </summary>
    [Function(nameof(AddTeamMember))]
    [ProjectionOutput<ProjectKanbanBoard>]
    public async Task<HttpResponseData> AddTeamMember(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{id}/members")] HttpRequestData req,
        string id,
        [EventStreamInput("{id}")] Project project)
    {
        var requestBody = await req.ReadFromJsonAsync<AddMemberRequest>();
        if (requestBody == null || string.IsNullOrWhiteSpace(requestBody.MemberId))
        {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { error = "MemberId is required" });
            return badResponse;
        }

        if (project.Metadata?.Id == null)
        {
            var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { error = "Project not found", id });
            return notFoundResponse;
        }

        logger.LogInformation("Adding member {MemberId} to project {ProjectId}", requestBody.MemberId, id);

        var result = await project.AddTeamMemberWithPermissions(
            UserProfileId.From(requestBody.MemberId),
            requestBody.Role ?? "Member",
            requestBody.Permissions ?? new MemberPermissions(CanEdit: false, CanDelete: false, CanInvite: false, CanManageWorkItems: false),
            UserProfileId.From(requestBody.AddedBy ?? "system"));

        if (result.IsFailure)
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = "Failed to add team member",
                details = result.Errors.ToArray().Select(e => e.Message)
            });
            return errorResponse;
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            success = true,
            message = $"Member {requestBody.MemberId} added to project",
            projectId = id,
            role = requestBody.Role ?? "Member"
        });

        return response;
    }

    /// <summary>
    /// Completes a project.
    /// Demonstrates aggregate state transitions.
    /// </summary>
    [Function(nameof(CompleteProject))]
    [ProjectionOutput<ProjectKanbanBoard>]
    public async Task<HttpResponseData> CompleteProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{id}/complete")] HttpRequestData req,
        string id,
        [EventStreamInput("{id}")] Project project)
    {
        var requestBody = await req.ReadFromJsonAsync<CompleteProjectRequest>();

        if (project.Metadata?.Id == null)
        {
            var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { error = "Project not found", id });
            return notFoundResponse;
        }

        logger.LogInformation("Completing project {ProjectId}", id);

        var result = await project.CompleteProjectSuccessfully(
            requestBody?.Summary ?? "Project completed successfully",
            UserProfileId.From(requestBody?.CompletedBy ?? "system"));

        if (result.IsFailure)
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = "Failed to complete project",
                details = result.Errors.ToArray().Select(e => e.Message)
            });
            return errorResponse;
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            success = true,
            message = "Project completed successfully",
            projectId = id
        });

        return response;
    }

    /// <summary>
    /// Gets the project kanban board for a specific project.
    /// Demonstrates combining projection input with project lookup.
    /// </summary>
    [Function(nameof(GetProjectKanban))]
    public async Task<HttpResponseData> GetProjectKanban(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{id}/kanban")] HttpRequestData req,
        string id,
        [ProjectionInput] ProjectKanbanBoard kanbanBoard)
    {
        logger.LogInformation("Getting kanban board for project {ProjectId}", id);

        // Check if project exists in the kanban board
        if (kanbanBoard.Projects == null || !kanbanBoard.Projects.TryGetValue(id, out var projectInfo))
        {
            var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { error = "Project not found in kanban board", id });
            return notFoundResponse;
        }

        // Try to get the destination for this project's work items
        var workItems = new List<object>();
        if (kanbanBoard.TryGetDestination<ProjectKanbanDestination>(id, out var destination) && destination.WorkItems != null)
        {
            workItems = destination.WorkItems.Values.Select(w => new
            {
                id = w.Id,
                title = w.Title,
                status = w.Status,
                assignedTo = w.AssignedTo
            }).ToList<object>();
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            projectId = id,
            projectName = projectInfo.Name,
            workItems,
            workItemCount = workItems.Count,
            checkpointFingerprint = kanbanBoard.CheckpointFingerprint
        });

        return response;
    }

    // Request records
    public record CreateProjectRequest(string Name, string? Description, string? OwnerId);
    public record RenameProjectRequest(string NewName, string? RenamedBy);
    public record AddMemberRequest(string MemberId, string? Role, MemberPermissions? Permissions, string? AddedBy);
    public record CompleteProjectRequest(string? Summary, string? CompletedBy);
}
