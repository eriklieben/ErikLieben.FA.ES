# Projection Catch-Up

Projection catch-up enables you to discover and process all existing objects for a projection that needs to be rebuilt or initialized. The library provides work item discovery; you implement the orchestration using your preferred mechanism (Durable Functions, queues, batch processing).

## Overview

Catch-up processing is needed when:
- **New projection deployment** - A new projection type needs to process all existing aggregates
- **Schema changes** - A projection's `When` methods changed and need to reprocess events
- **Bug fixes** - A projection bug was fixed and historical data needs reprocessing
- **Recovery** - A projection's stored state was corrupted or lost

The catch-up mechanism:
1. Uses `IObjectIdProvider` to enumerate all object IDs for specified object types
2. Returns `CatchUpWorkItem` records representing work to be done
3. Supports pagination with continuation tokens for large datasets
4. Provides streaming via `IAsyncEnumerable` for memory-efficient processing

## Key Types

### CatchUpWorkItem

Represents a single unit of work for catch-up processing:

```csharp
public record CatchUpWorkItem(
    string ObjectName,      // e.g., "project", "workitem"
    string ObjectId,        // The unique object identifier
    string? ProjectionTypeName = null);  // Optional projection filter
```

### CatchUpDiscoveryResult

Paginated result from discovery operations:

```csharp
public record CatchUpDiscoveryResult(
    IReadOnlyList<CatchUpWorkItem> WorkItems,  // Work items in this page
    string? ContinuationToken,                  // Token for next page (null when done)
    long? TotalEstimate);                       // Optional total count estimate
```

### ICatchUpDiscoveryService

The main interface for discovering work items:

| Method | Description |
|--------|-------------|
| `StreamWorkItemsAsync` | Returns `IAsyncEnumerable<CatchUpWorkItem>` for streaming all items |
| `DiscoverWorkItemsAsync` | Returns paginated `CatchUpDiscoveryResult` with continuation token |
| `EstimateTotalWorkItemsAsync` | Returns approximate count (may be expensive) |

The service is registered automatically when you call `ConfigureEventStore()`.

## Basic Usage

### Streaming All Work Items

The simplest approach streams all work items using `IAsyncEnumerable`:

```csharp
public class CatchUpProcessor
{
    private readonly ICatchUpDiscoveryService _discoveryService;

    public CatchUpProcessor(ICatchUpDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    public async Task ProcessAllAsync(CancellationToken ct)
    {
        // Stream work items for project and workitem aggregates
        await foreach (var item in _discoveryService.StreamWorkItemsAsync(
            ["project", "workitem"],
            pageSize: 100,
            ct))
        {
            Console.WriteLine($"Processing {item.ObjectName}/{item.ObjectId}");
            // Process the work item...
        }
    }
}
```

### Paginated Discovery

For scenarios requiring explicit pagination control:

```csharp
public async Task ProcessWithPaginationAsync()
{
    string? continuationToken = null;

    do
    {
        var result = await _discoveryService.DiscoverWorkItemsAsync(
            objectNames: ["project", "workitem"],
            pageSize: 50,
            continuationToken: continuationToken);

        foreach (var item in result.WorkItems)
        {
            Console.WriteLine($"Processing {item.ObjectName}/{item.ObjectId}");
            // Process the work item...
        }

        continuationToken = result.ContinuationToken;

    } while (continuationToken != null);
}
```

### Estimating Total Work

To show progress or plan batch sizes:

```csharp
var totalItems = await _discoveryService.EstimateTotalWorkItemsAsync(
    ["project", "workitem"]);

Console.WriteLine($"Approximately {totalItems} items to process");
```

> **Note:** `EstimateTotalWorkItemsAsync` may be expensive for large datasets as it requires counting objects in storage.

## Concurrency Considerations

**Important**: When rebuilding projections, you must avoid updating the same projection from multiple parallel processes. If multiple activities load, update, and save the same projection concurrently, they will overwrite each other's changes.

**Problem with parallel processing:**
```
Activity A: Load projection (state S0) → Update → Save (S1)
Activity B: Load projection (state S0) → Update → Save (S2)  ← Overwrites A's changes!
```

**Solution**: Process all work items sequentially in a single activity:
1. Load the projection once
2. For each work item, load its document metadata (lightweight) and call `UpdateToVersion`
3. Save the projection once at the end

## Orchestration Patterns

The library provides discovery only. You implement orchestration using your preferred mechanism.

### Durable Functions (Sequential Processing with Configurable Batch Saves)

The recommended pattern for Azure Functions uses Durable Functions with sequential projection building and configurable intermediate saves:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;

public class CatchUpFunctions
{
    private readonly ICatchUpDiscoveryService _discoveryService;
    private readonly IProjectionFactory<ProjectKanbanBoard> _kanbanFactory;
    private readonly IObjectDocumentFactory _documentFactory;
    private readonly IEventStreamFactory _eventStreamFactory;

    // HTTP trigger to start catch-up
    // POST /api/catchup/start
    // Body: { "objectNames": ["project", "workitem"], "batchSize": 100 }
    [Function("StartCatchUp")]
    public async Task<HttpResponseData> StartCatchUp(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "catchup/start")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        var request = await req.ReadFromJsonAsync<CatchUpRequest>();
        var objectNames = request?.ObjectNames ?? ["project", "workitem"];
        var batchSize = request?.BatchSize ?? 0;  // 0 = save once at end

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            "CatchUpOrchestrator",
            new CatchUpOrchestratorInput(objectNames, batchSize));

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { instanceId, batchSize });
        return response;
    }

    // Orchestrator - coordinates discovery and building
    [Function("CatchUpOrchestrator")]
    public async Task<CatchUpSummary> CatchUpOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<CatchUpOrchestratorInput>()!;

        // Step 1: Discover all work items
        var workItems = await context.CallActivityAsync<List<CatchUpWorkItem>>(
            "DiscoverWorkItemsActivity", input.ObjectNames);

        if (workItems.Count == 0)
            return new CatchUpSummary(0, 0, 0, 0);

        // Step 2: Build projection with configurable batch saves
        var buildInput = new BuildProjectionInput(workItems, input.BatchSize);
        var result = await context.CallActivityAsync<BuildResult>(
            "BuildProjectionActivity", buildInput);

        return new CatchUpSummary(workItems.Count, result.Processed, result.Failed, result.SaveCount);
    }

    // Activity: Build projection with intermediate saves
    [Function("BuildProjectionActivity")]
    public async Task<BuildResult> BuildProjectionActivity(
        [ActivityTrigger] BuildProjectionInput input)
    {
        var projection = await _kanbanFactory.GetOrCreateAsync(
            _documentFactory, _eventStreamFactory);

        int processed = 0, failed = 0, saveCount = 0, itemsSinceLastSave = 0;

        foreach (var workItem in input.WorkItems)
        {
            try
            {
                var document = await _documentFactory.GetAsync(
                    workItem.ObjectName, workItem.ObjectId);

                if (document?.Active != null)
                {
                    var token = new VersionToken(
                        workItem.ObjectName,
                        workItem.ObjectId,
                        document.Active.StreamIdentifier,
                        version: 0
                    ).ToLatestVersion();

                    await projection.UpdateToVersion(token);
                    processed++;
                    itemsSinceLastSave++;

                    // Intermediate save if batch size reached
                    if (input.BatchSize > 0 && itemsSinceLastSave >= input.BatchSize)
                    {
                        await _kanbanFactory.SaveAsync(projection);
                        saveCount++;
                        itemsSinceLastSave = 0;
                    }
                }
            }
            catch { failed++; }
        }

        // Final save
        if (itemsSinceLastSave > 0 || saveCount == 0)
        {
            await _kanbanFactory.SaveAsync(projection);
            saveCount++;
        }

        return new BuildResult(processed, failed, saveCount);
    }
}

// Request: { "objectNames": [...], "batchSize": 100 }
public record CatchUpRequest(string[] ObjectNames, int BatchSize = 0);
public record CatchUpOrchestratorInput(string[] ObjectNames, int BatchSize);
public record BuildProjectionInput(List<CatchUpWorkItem> WorkItems, int BatchSize);
public record BuildResult(int Processed, int Failed, int SaveCount);
public record CatchUpSummary(int TotalItems, int SuccessCount, int FailureCount, int SaveCount);
```

**Batch Size Configuration:**
- `batchSize = 0` (default): Save once at the end - most efficient, but no resume on failure
- `batchSize > 0`: Save after every N items - allows resume from checkpoint on failure

### Queue-Based Distribution

For Container Apps or other queue-based workers:

```csharp
// Producer: Enqueue work items
public async Task EnqueueCatchUpWorkAsync(
    ICatchUpDiscoveryService discoveryService,
    QueueClient queueClient)
{
    await foreach (var item in discoveryService.StreamWorkItemsAsync(["project", "workitem"]))
    {
        var message = JsonSerializer.Serialize(item);
        await queueClient.SendMessageAsync(message);
    }
}

// Consumer: Process from queue (in worker service)
public class CatchUpWorker : BackgroundService
{
    private readonly QueueClient _queueClient;
    private readonly IServiceProvider _services;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var messages = await _queueClient.ReceiveMessagesAsync(maxMessages: 10);

            foreach (var message in messages.Value)
            {
                var workItem = JsonSerializer.Deserialize<CatchUpWorkItem>(
                    message.MessageText);

                await ProcessWorkItemAsync(workItem!, ct);
                await _queueClient.DeleteMessageAsync(
                    message.MessageId,
                    message.PopReceipt);
            }

            await Task.Delay(1000, ct);
        }
    }
}
```

### Simple Sequential Processing

For small datasets or development/testing, use sequential processing:

```csharp
public async Task RunSequentialCatchUpAsync(
    ICatchUpDiscoveryService discoveryService,
    IProjectionFactory<ProjectKanbanBoard> projectionFactory,
    IObjectDocumentFactory documentFactory,
    IEventStreamFactory eventStreamFactory)
{
    // Load projection ONCE
    var projection = await projectionFactory.GetOrCreateAsync(
        documentFactory, eventStreamFactory);

    int processed = 0;

    // Process all work items sequentially
    await foreach (var item in discoveryService.StreamWorkItemsAsync(["project", "workitem"]))
    {
        var document = await documentFactory.GetAsync(item.ObjectName, item.ObjectId);

        if (document?.Active != null)
        {
            var token = new VersionToken(
                item.ObjectName,
                item.ObjectId,
                document.Active.StreamIdentifier,
                version: 0
            ).ToLatestVersion();

            await projection.UpdateToVersion(token);
            processed++;
        }
    }

    // Save ONCE at the end
    await projectionFactory.SaveAsync(projection);

    Console.WriteLine($"Processed {processed} work items");
}
```

## Best Practices

### Do

- **Process projections sequentially** - Load once, process all work items, save once to avoid concurrency conflicts
- **Use streaming for discovery** - `StreamWorkItemsAsync` processes items as they're discovered without loading all into memory
- **Implement proper error handling** - Track failures separately for retry/investigation
- **Use continuation tokens for resumption** - Store the token to resume after failures
- **Log progress** - Track processed count for monitoring and debugging
- **Load document metadata only** - `GetAsync` returns lightweight metadata, not events; events are read by `UpdateToVersion`

### Don't

- **Don't update projections in parallel** - Multiple processes saving the same projection will overwrite each other
- **Don't load all work items into memory** - For large datasets, this causes memory pressure; use streaming instead
- **Don't ignore failures** - Track and retry failed items separately
- **Don't estimate count for progress bars in production** - It's expensive; use processed count instead
- **Don't use fan-out for projection updates** - The orchestration should coordinate, but projection building must be sequential

## Testing

### Unit Testing with Mock Provider

```csharp
public class CatchUpDiscoveryServiceTests
{
    [Fact]
    public async Task StreamWorkItemsAsync_ReturnsAllItems()
    {
        // Arrange
        var mockProvider = new Mock<IObjectIdProvider>();
        mockProvider
            .Setup(p => p.GetObjectIdsAsync("project", null, 100, default))
            .ReturnsAsync(new PagedResult<string>
            {
                Items = ["proj-1", "proj-2"],
                ContinuationToken = null
            });

        var service = new CatchUpDiscoveryService(mockProvider.Object);

        // Act
        var items = new List<CatchUpWorkItem>();
        await foreach (var item in service.StreamWorkItemsAsync(["project"]))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal("project", i.ObjectName));
    }
}
```

### Integration Testing

```csharp
[Fact]
public async Task CatchUp_ProcessesAllProjects()
{
    // Arrange - use test context with in-memory storage
    var context = TestSetup.GetContext();

    // Create some test aggregates
    for (int i = 0; i < 5; i++)
    {
        var project = await context.GetAggregate<Project>($"proj-{i}");
        await project.Create($"Project {i}");
    }

    var discoveryService = context.Services.GetRequiredService<ICatchUpDiscoveryService>();

    // Act
    var items = new List<CatchUpWorkItem>();
    await foreach (var item in discoveryService.StreamWorkItemsAsync(["project"]))
    {
        items.Add(item);
    }

    // Assert
    Assert.Equal(5, items.Count);
}
```

## See Also

- [Projections](Projections.md) - Core projection concepts and patterns
- [Azure Functions](AzureFunctions.md) - Azure Functions integration including bindings
- [Version Tokens](VersionTokens.md) - Understanding version management for projections
- [Testing](Testing.md) - Testing patterns for aggregates and projections
