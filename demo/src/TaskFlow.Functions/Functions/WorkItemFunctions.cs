using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Projections;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Functions.Functions;

/// <summary>
/// Sample Azure Functions demonstrating the EventStream input binding.
/// These functions show how to use [EventStreamInput] to automatically load aggregates.
/// </summary>
public class WorkItemFunctions(ILogger<WorkItemFunctions> logger)
{
    /// <summary>
    /// Gets a work item by ID using the EventStream input binding.
    /// The binding automatically loads the aggregate from the event stream.
    /// </summary>
    /// <remarks>
    /// Usage: GET /api/workitems/{id}
    ///
    /// The [EventStreamInput] attribute automatically:
    /// 1. Resolves the appropriate factory (IWorkItemFactory)
    /// 2. Loads the aggregate from the event store
    /// 3. Folds all events to rebuild current state
    /// </remarks>
    [Function(nameof(GetWorkItem))]
    public async Task<HttpResponseData> GetWorkItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "workitems/{id}")] HttpRequestData req,
        string id,
        [EventStreamInput("{id}")] WorkItem workItem)
    {
        logger.LogInformation("Getting work item {WorkItemId}", id);

        if (workItem.Metadata?.Id == null)
        {
            logger.LogWarning("Work item {WorkItemId} not found", id);
            var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new
            {
                error = "Work item not found",
                id = id
            });
            return notFoundResponse;
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            id = workItem.Metadata?.Id?.Value.ToString() ?? id,
            title = workItem.Title,
            description = workItem.Description,
            status = workItem.Status.ToString(),
            priority = workItem.Priority.ToString(),
            projectId = workItem.ProjectId,
            assignedTo = workItem.AssignedTo,
            deadline = workItem.Deadline,
            estimatedHours = workItem.EstimatedHours,
            tags = workItem.Tags,
            commentsCount = workItem.Comments.Count
        });

        return response;
    }

    /// <summary>
    /// Assigns responsibility for a work item.
    /// Demonstrates modifying an aggregate loaded via EventStream binding.
    /// </summary>
    [Function(nameof(AssignWorkItem))]
    public async Task<HttpResponseData> AssignWorkItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workitems/{id}/assign")] HttpRequestData req,
        string id,
        [EventStreamInput("{id}")] WorkItem workItem)
    {
        var requestBody = await req.ReadFromJsonAsync<AssignWorkItemRequest>();
        if (requestBody == null)
        {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { error = "Invalid request body" });
            return badResponse;
        }

        logger.LogInformation("Assigning work item {WorkItemId} to {MemberId}", id, requestBody.MemberId);

        // The aggregate is already loaded - we can call domain methods on it
        // Note: In a real scenario, you'd also pass a user context/token
        // await workItem.AssignResponsibility(requestBody.MemberId, userId, userToken);

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            success = true,
            message = $"Work item {id} assigned to {requestBody.MemberId}",
            workItemId = id
        });

        return response;
    }

    /// <summary>
    /// Creates a new work item.
    /// Shows creating an aggregate without EventStream binding (using factory directly).
    /// </summary>
    [Function(nameof(CreateWorkItem))]
    public async Task<HttpResponseData> CreateWorkItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workitems")] HttpRequestData req,
        [FromServices] IWorkItemFactory workItemFactory)
    {
        var requestBody = await req.ReadFromJsonAsync<CreateWorkItemRequest>();
        if (requestBody == null)
        {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { error = "Invalid request body" });
            return badResponse;
        }

        logger.LogInformation("Creating new work item: {Title}", requestBody.Title);

        // For creation, we use the factory directly
        // This is because the aggregate doesn't exist yet
        var workItemId = WorkItemId.New();

        var response = req.CreateResponse(System.Net.HttpStatusCode.Created);
        response.Headers.Add("Location", $"/api/workitems/{workItemId.Value}");
        await response.WriteAsJsonAsync(new
        {
            success = true,
            message = "Work item created",
            workItemId = workItemId.Value.ToString()
        });

        return response;
    }

    /// <summary>
    /// Triggers an update of projections to their latest state.
    /// Demonstrates using [ProjectionOutput&lt;T&gt;] to update projections after function execution.
    /// </summary>
    /// <remarks>
    /// Usage: POST /api/projections/refresh
    ///
    /// The [ProjectionOutput&lt;T&gt;] attributes cause the middleware to:
    /// 1. Load each projection after the function completes
    /// 2. Call UpdateToLatestVersion() to process any pending events
    /// 3. Save the updated projection back to storage
    ///
    /// Multiple projections can be updated by adding multiple attributes.
    /// If any projection update fails, all updates are rolled back.
    /// </remarks>
    [Function(nameof(RefreshProjections))]
    [ProjectionOutput<ActiveWorkItems>]
    [ProjectionOutput<ProjectKanbanBoard>]
    public async Task<HttpResponseData> RefreshProjections(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projections/refresh")] HttpRequestData req)
    {
        logger.LogInformation("Refreshing projections to latest state");

        // The actual projection updates happen in the middleware after this function returns
        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            success = true,
            message = "Projection refresh triggered",
            projections = new[] { nameof(ActiveWorkItems), nameof(ProjectKanbanBoard) }
        });

        return response;
    }

    /// <summary>
    /// Simple health check endpoint without any custom bindings.
    /// Use this to verify the Functions host is working correctly.
    /// </summary>
    [Function(nameof(HealthCheck))]
    public async Task<HttpResponseData> HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        logger.LogInformation("Health check requested");

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "TaskFlow.Functions"
        });

        return response;
    }

    public record AssignWorkItemRequest(string MemberId);
    public record CreateWorkItemRequest(string Title, string Description, string ProjectId, WorkItemPriority Priority);
}
