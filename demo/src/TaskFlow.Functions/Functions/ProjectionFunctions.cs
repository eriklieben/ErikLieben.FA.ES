using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Functions.Functions;

/// <summary>
/// Sample Azure Functions demonstrating the Projection input binding.
/// These functions show how to use [ProjectionInput] to automatically load projections.
/// </summary>
public class ProjectionFunctions
{
    private readonly ILogger<ProjectionFunctions> _logger;

    public ProjectionFunctions(ILogger<ProjectionFunctions> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the Project Kanban Board projection.
    /// The binding automatically loads the projection from blob storage.
    /// </summary>
    /// <remarks>
    /// Usage: GET /api/projections/kanban
    ///
    /// The [ProjectionInput] attribute automatically:
    /// 1. Resolves the appropriate factory (IProjectionFactory&lt;ProjectKanbanBoard&gt;)
    /// 2. Loads the projection from blob storage
    /// 3. Returns the fully hydrated projection
    /// </remarks>
    [Function("GetKanbanBoard")]
    public async Task<HttpResponseData> GetKanbanBoard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projections/kanban")] HttpRequestData req,
        [ProjectionInput] ProjectKanbanBoard kanbanBoard)
    {
        _logger.LogInformation("Getting Kanban board projection");

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            projectCount = kanbanBoard.Projects?.Count ?? 0,
            projects = kanbanBoard.Projects?.Select(p => new
            {
                projectId = p.Key,
                projectName = p.Value.Name
            }),
            checkpointFingerprint = kanbanBoard.CheckpointFingerprint
        });

        return response;
    }

    /// <summary>
    /// Gets the Active Work Items projection.
    /// Shows loading a different projection type.
    /// </summary>
    [Function("GetActiveWorkItems")]
    public async Task<HttpResponseData> GetActiveWorkItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projections/active-workitems")] HttpRequestData req,
        [ProjectionInput] ActiveWorkItems activeWorkItems)
    {
        _logger.LogInformation("Getting active work items projection");

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            activeCount = activeWorkItems.WorkItems?.Count ?? 0,
            items = activeWorkItems.WorkItems?.Select(i => new
            {
                id = i.Key,
                title = i.Value.Title,
                status = i.Value.Status.ToString(),
                assignedTo = i.Value.AssignedTo
            }),
            checkpointFingerprint = activeWorkItems.CheckpointFingerprint
        });

        return response;
    }

    /// <summary>
    /// Gets the User Profiles projection (routed projection).
    /// Demonstrates loading a routed projection with destinations.
    /// </summary>
    [Function("GetUserProfiles")]
    public async Task<HttpResponseData> GetUserProfiles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projections/userprofiles")] HttpRequestData req,
        [ProjectionInput] UserProfiles userProfiles)
    {
        _logger.LogInformation("Getting user profiles projection");

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            totalUsers = userProfiles.TotalUsers,
            destinationCount = userProfiles.Destinations?.Count ?? 0,
            checkpointFingerprint = userProfiles.CheckpointFingerprint
        });

        return response;
    }

    /// <summary>
    /// Example of a queue-triggered function that processes projection updates.
    /// Demonstrates using projection binding with non-HTTP triggers.
    /// </summary>
    [Function("ProcessProjectionUpdate")]
    public async Task ProcessProjectionUpdate(
        [QueueTrigger("projection-updates", Connection = "AzureWebJobsStorage")] ProjectionUpdateMessage message,
        [ProjectionInput] ProjectKanbanBoard kanbanBoard)
    {
        _logger.LogInformation(
            "Processing projection update for project {ProjectId} with event {EventType}",
            message.ProjectId,
            message.EventType);

        // The projection is loaded - you can fold events into it
        // In a real scenario you would:
        // 1. Get the event from the message or event store
        // 2. Call projection.Fold(event, versionToken)
        // 3. Save the projection back to storage

        await Task.CompletedTask;
    }

    public record ProjectionUpdateMessage(string ProjectId, string EventType, string EventId);
}
