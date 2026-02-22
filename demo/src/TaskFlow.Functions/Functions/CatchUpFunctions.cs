using System.Net;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Functions.Functions;

/// <summary>
/// Demonstrates projection catch-up using Durable Functions.
/// This example shows how to rebuild projections by processing all existing aggregates.
/// </summary>
/// <remarks>
/// The catch-up process:
/// 1. StartCatchUp - HTTP trigger that initiates the orchestration
/// 2. CatchUpOrchestrator - Coordinates discovery and building
/// 3. DiscoverWorkItemsActivity - Activity that finds all objects to process
/// 4. BuildProjectionActivity - Activity that processes work items with configurable batch saves
/// 5. GetCatchUpStatus - HTTP trigger to check progress
///
/// IMPORTANT: Projection updates are processed sequentially in a single activity to avoid
/// concurrency issues. If multiple activities update the same projection in parallel,
/// they would overwrite each other's changes.
///
/// The batch size controls how often intermediate saves occur:
/// - BatchSize = 0 or null: Save once at the end (default, most efficient)
/// - BatchSize > 0: Save after every N work items (allows resume on failure)
/// </remarks>
public class CatchUpFunctions
{
    private readonly ICatchUpDiscoveryService _discoveryService;
    private readonly IProjectionFactory<ProjectKanbanBoard> _kanbanFactory;
    private readonly IObjectDocumentFactory _documentFactory;
    private readonly IEventStreamFactory _eventStreamFactory;
    private readonly ILogger<CatchUpFunctions> _logger;

    public CatchUpFunctions(
        ICatchUpDiscoveryService discoveryService,
        IProjectionFactory<ProjectKanbanBoard> kanbanFactory,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        ILogger<CatchUpFunctions> logger)
    {
        _discoveryService = discoveryService;
        _kanbanFactory = kanbanFactory;
        _documentFactory = documentFactory;
        _eventStreamFactory = eventStreamFactory;
        _logger = logger;
    }

    /// <summary>
    /// HTTP trigger to start a projection catch-up operation.
    /// </summary>
    /// <remarks>
    /// Usage: POST /api/catchup/start
    /// Body: { "objectNames": ["project", "workitem"], "batchSize": 100 }
    ///
    /// Parameters:
    /// - objectNames: Array of object type names to process (default: ["project", "workitem"])
    /// - batchSize: Number of items to process before saving (default: 0 = save once at end)
    ///
    /// Returns the orchestration instance ID for tracking progress.
    /// </remarks>
    [Function("StartCatchUp")]
    public async Task<HttpResponseData> StartCatchUp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "catchup/start")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        _logger.LogInformation("Starting catch-up orchestration");

        // Parse request body for configuration
        CatchUpRequest? request = null;
        try
        {
            request = await req.ReadFromJsonAsync<CatchUpRequest>();
        }
        catch
        {
            // Use defaults if body parsing fails
        }

        var objectNames = request?.ObjectNames ?? ["project", "workitem"];
        var batchSize = request?.BatchSize ?? 0;

        // Start the orchestration
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            "CatchUpOrchestrator",
            new CatchUpOrchestratorInput(objectNames, batchSize));

        _logger.LogInformation(
            "Started catch-up orchestration with ID: {InstanceId}, BatchSize: {BatchSize}",
            instanceId, batchSize);

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new
        {
            instanceId,
            statusUrl = $"/api/catchup/status/{instanceId}",
            batchSize,
            message = "Catch-up orchestration started"
        });

        return response;
    }

    /// <summary>
    /// HTTP trigger to check the status of a catch-up operation.
    /// </summary>
    /// <remarks>
    /// Usage: GET /api/catchup/status/{instanceId}
    ///
    /// Returns the current status, progress, and results of the orchestration.
    /// </remarks>
    [Function("GetCatchUpStatus")]
    public async Task<HttpResponseData> GetCatchUpStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "catchup/status/{instanceId}")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId)
    {
        var metadata = await client.GetInstanceAsync(instanceId);

        if (metadata == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Orchestration not found" });
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            instanceId = metadata.InstanceId,
            status = metadata.RuntimeStatus.ToString(),
            createdAt = metadata.CreatedAt,
            lastUpdatedAt = metadata.LastUpdatedAt,
            output = metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed
                ? metadata.ReadOutputAs<CatchUpSummary>()
                : null
        });

        return response;
    }

    /// <summary>
    /// Durable orchestrator that coordinates the catch-up process.
    /// Uses sequential processing to avoid concurrency conflicts on the projection.
    /// </summary>
    [Function("CatchUpOrchestrator")]
    public async Task<CatchUpSummary> CatchUpOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<CatchUpFunctions>();
        var input = context.GetInput<CatchUpOrchestratorInput>()!;

        logger.LogInformation("Starting catch-up for object types: {ObjectNames}, BatchSize: {BatchSize}",
            string.Join(", ", input.ObjectNames), input.BatchSize);

        // Step 1: Discover all work items
        var workItems = await context.CallActivityAsync<List<CatchUpWorkItem>>(
            "DiscoverWorkItemsActivity",
            input.ObjectNames);

        logger.LogInformation("Discovered {Count} work items to process", workItems.Count);

        if (workItems.Count == 0)
        {
            return new CatchUpSummary(0, 0, 0, 0);
        }

        // Step 2: Build projection - process ALL work items in a single activity
        // BatchSize controls how often intermediate saves occur
        var buildInput = new BuildProjectionInput(workItems, input.BatchSize);
        var buildResult = await context.CallActivityAsync<BuildResult>(
            "BuildProjectionActivity",
            buildInput);

        // Step 3: Return summary
        var summary = new CatchUpSummary(
            TotalItems: workItems.Count,
            SuccessCount: buildResult.Processed,
            FailureCount: buildResult.Failed,
            SaveCount: buildResult.SaveCount);

        logger.LogInformation(
            "Catch-up completed: {Total} total, {Success} succeeded, {Failed} failed, {Saves} saves",
            summary.TotalItems, summary.SuccessCount, summary.FailureCount, summary.SaveCount);

        return summary;
    }

    /// <summary>
    /// Activity that discovers all work items using ICatchUpDiscoveryService.
    /// </summary>
    [Function("DiscoverWorkItemsActivity")]
    public async Task<List<CatchUpWorkItem>> DiscoverWorkItemsActivity(
        [ActivityTrigger] string[] objectNames)
    {
        _logger.LogInformation("Discovering work items for: {ObjectNames}",
            string.Join(", ", objectNames));

        var workItems = new List<CatchUpWorkItem>();

        await foreach (var item in _discoveryService.StreamWorkItemsAsync(objectNames))
        {
            workItems.Add(item);
        }

        _logger.LogInformation("Discovered {Count} work items", workItems.Count);
        return workItems;
    }

    /// <summary>
    /// Activity that builds the projection by processing work items sequentially with configurable batch saves.
    /// </summary>
    /// <remarks>
    /// This activity processes all work items on a single projection instance to avoid
    /// concurrency issues. Each work item's document metadata is loaded (lightweight),
    /// then UpdateToVersion reads events and folds them into the projection sequentially.
    ///
    /// The BatchSize parameter controls intermediate saves:
    /// - BatchSize = 0: Save once at the end (most efficient, but no resume on failure)
    /// - BatchSize > 0: Save after every N items (allows resume from checkpoint on failure)
    /// </remarks>
    [Function("BuildProjectionActivity")]
    public async Task<BuildResult> BuildProjectionActivity(
        [ActivityTrigger] BuildProjectionInput input)
    {
        var workItems = input.WorkItems;
        var batchSize = input.BatchSize;

        _logger.LogInformation(
            "Building projection with {Count} work items, BatchSize: {BatchSize}",
            workItems.Count, batchSize);

        // Load projection ONCE at the start
        var projection = await _kanbanFactory.GetOrCreateAsync(
            _documentFactory,
            _eventStreamFactory);

        int processed = 0;
        int failed = 0;
        int saveCount = 0;
        int itemsSinceLastSave = 0;

        // Process ALL work items sequentially on the same projection instance
        foreach (var workItem in workItems)
        {
            try
            {
                // GetAsync loads only document METADATA (not events) - lightweight!
                // Just need StreamIdentifier to construct the VersionToken
                var document = await _documentFactory.GetAsync(
                    workItem.ObjectName,
                    workItem.ObjectId);

                if (document?.Active != null)
                {
                    // Create token from version 0 to latest
                    var token = new VersionToken(
                        workItem.ObjectName,
                        workItem.ObjectId,
                        document.Active.StreamIdentifier,
                        version: 0
                    ).ToLatestVersion();

                    // UpdateToVersion reads events from stream and folds sequentially
                    // Events are NOT read by GetAsync - only read here
                    await projection.UpdateToVersion(token);
                    processed++;
                    itemsSinceLastSave++;

                    _logger.LogDebug("Processed {ObjectName}/{ObjectId}",
                        workItem.ObjectName, workItem.ObjectId);

                    // Intermediate save if batch size is configured and reached
                    if (batchSize > 0 && itemsSinceLastSave >= batchSize)
                    {
                        await _kanbanFactory.SaveAsync(projection);
                        saveCount++;
                        itemsSinceLastSave = 0;

                        _logger.LogInformation(
                            "Intermediate save after {Processed} items (save #{SaveCount})",
                            processed, saveCount);
                    }
                }
                else
                {
                    _logger.LogWarning("Document not found for {ObjectName}/{ObjectId}",
                        workItem.ObjectName, workItem.ObjectId);
                    failed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process {ObjectName}/{ObjectId}",
                    workItem.ObjectName, workItem.ObjectId);
                failed++;
            }
        }

        // Final save (always needed unless we just saved and there are no new items)
        if (itemsSinceLastSave > 0 || saveCount == 0)
        {
            await _kanbanFactory.SaveAsync(projection);
            saveCount++;
        }

        _logger.LogInformation(
            "Projection build completed: {Processed} processed, {Failed} failed, {SaveCount} saves",
            processed, failed, saveCount);

        return new BuildResult(processed, failed, saveCount);
    }
}

/// <summary>
/// Request model for starting a catch-up operation.
/// </summary>
/// <param name="ObjectNames">Object type names to process (e.g., ["project", "workitem"])</param>
/// <param name="BatchSize">Number of items to process before intermediate save. 0 = save once at end.</param>
public record CatchUpRequest(string[] ObjectNames, int BatchSize = 0);

/// <summary>
/// Input for the catch-up orchestrator.
/// </summary>
/// <param name="ObjectNames">Object type names to process</param>
/// <param name="BatchSize">Number of items to process before intermediate save</param>
public record CatchUpOrchestratorInput(string[] ObjectNames, int BatchSize);

/// <summary>
/// Input for the build projection activity.
/// </summary>
/// <param name="WorkItems">List of work items to process</param>
/// <param name="BatchSize">Number of items to process before intermediate save</param>
public record BuildProjectionInput(List<CatchUpWorkItem> WorkItems, int BatchSize);

/// <summary>
/// Result of the build projection activity.
/// </summary>
/// <param name="Processed">Number of successfully processed items</param>
/// <param name="Failed">Number of failed items</param>
/// <param name="SaveCount">Number of save operations performed</param>
public record BuildResult(int Processed, int Failed, int SaveCount);

/// <summary>
/// Summary of a completed catch-up operation.
/// </summary>
/// <param name="TotalItems">Total number of discovered work items</param>
/// <param name="SuccessCount">Number of successfully processed items</param>
/// <param name="FailureCount">Number of failed items</param>
/// <param name="SaveCount">Number of save operations performed</param>
public record CatchUpSummary(int TotalItems, int SuccessCount, int FailureCount, int SaveCount);
