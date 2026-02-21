# Projection Status and Versioning

This guide covers advanced patterns for managing projection status and schema versioning, including rebuild workflows and coordination strategies.

## Overview

When deploying new projections or updating existing ones, you need to:

1. **Track operational status** - Know if a projection is being rebuilt
2. **Detect schema changes** - Know when code differs from stored data
3. **Coordinate rebuilds** - Prevent conflicts between inline updates and rebuilds
4. **Handle skipped updates** - Queue events that couldn't be processed

## Projection Status

### Status Enum

```csharp
public enum ProjectionStatus
{
    Active = 0,      // Normal operation
    Rebuilding = 1,  // Rebuild in progress
    Disabled = 2     // Turned off
}
```

### Status Behavior

| Status | Inline Updates | Rebuild Updates | Use Case |
|--------|----------------|-----------------|----------|
| `Active` | Processed | Processed | Normal operation |
| `Rebuilding` | Skipped (with result) | Processed | During rebuild |
| `Disabled` | Skipped | Skipped | Maintenance mode |

### Factory Methods

```csharp
// Set status
await factory.SetStatusAsync(ProjectionStatus.Rebuilding);
await factory.SetStatusAsync(ProjectionStatus.Active);
await factory.SetStatusAsync(ProjectionStatus.Disabled);

// Get status (lightweight, doesn't load full projection)
var status = await factory.GetStatusAsync();
```

## Schema Versioning

### Declaring Schema Version

Use the `[ProjectionVersion]` attribute on your projection class:

```csharp
[BlobJsonProjection("projections")]
[ProjectionVersion(1)]  // Initial version
public partial class OrderDashboard : Projection
{
    public int TotalOrders { get; private set; }
}
```

When you make breaking changes, increment the version:

```csharp
[BlobJsonProjection("projections")]
[ProjectionVersion(2)]  // Breaking change: added TotalRevenue
public partial class OrderDashboard : Projection
{
    public int TotalOrders { get; private set; }
    public decimal TotalRevenue { get; private set; }  // New in v2
}
```

### Properties

The projection exposes version information:

```csharp
projection.SchemaVersion     // Version stored in projection data
projection.CodeSchemaVersion // Version from [ProjectionVersion] attribute
projection.NeedsSchemaUpgrade // True if versions differ
```

### Generated Code

The CLI generates a `CodeSchemaVersion` override:

```csharp
// Generated in OrderDashboard.Generated.cs
public override int CodeSchemaVersion => 2;
```

## Rebuild Patterns

### Basic Rebuild Workflow

```csharp
public class ProjectionRebuildService
{
    private readonly IProjectionFactory<OrderDashboard> _factory;
    private readonly IObjectDocumentFactory _docFactory;
    private readonly IEventStreamFactory _eventStreamFactory;
    private readonly ICatchUpDiscoveryService _discoveryService;

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        // 1. Set status to rebuilding
        await _factory.SetStatusAsync(ProjectionStatus.Rebuilding, cancellationToken: ct);

        try
        {
            // 2. Create fresh projection or clear existing
            var projection = new OrderDashboard(_docFactory, _eventStreamFactory);
            projection.SchemaVersion = projection.CodeSchemaVersion;

            // 3. Process all historical events
            await foreach (var item in _discoveryService.StreamWorkItemsAsync(["order"]))
            {
                var token = new VersionToken(item.ObjectName, item.ObjectId, TryUpdateToLatestVersion: true);
                await projection.UpdateToVersion(token);
            }

            // 4. Save rebuilt projection
            await _factory.SaveAsync(projection, cancellationToken: ct);

            // 5. Set status back to active
            await _factory.SetStatusAsync(ProjectionStatus.Active, cancellationToken: ct);
        }
        catch
        {
            // On failure, keep rebuilding status so retries can continue
            throw;
        }
    }
}
```

### Handling Skipped Updates

When inline updates are skipped during rebuild, use the result to queue retries:

```csharp
public class ProjectionUpdateHandler
{
    private readonly IProjectionFactory<OrderDashboard> _factory;
    private readonly IRetryQueue _retryQueue;
    private readonly ILogger<ProjectionUpdateHandler> _logger;

    public async Task HandleEventAsync(IEvent @event, VersionToken token)
    {
        var projection = await _factory.GetOrCreateAsync(_docFactory, _eventStreamFactory);

        var result = await projection.UpdateToVersion(token);

        if (result.Skipped)
        {
            _logger.LogWarning(
                "Update skipped for {ObjectId}: projection status is {Status}",
                token.ObjectId,
                result.Status);

            if (result.Status == ProjectionStatus.Rebuilding)
            {
                // Queue for retry after rebuild completes
                await _retryQueue.EnqueueAsync(new RetryItem
                {
                    Token = result.SkippedToken!,
                    ScheduledFor = DateTimeOffset.UtcNow.AddMinutes(5)
                });
            }
        }
        else
        {
            await _factory.SaveAsync(projection);
        }
    }
}
```

### Azure Durable Functions Pattern

For long-running rebuilds, use Durable Functions:

```csharp
[Function("RebuildProjection")]
public async Task RebuildOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var projectionName = context.GetInput<string>();

    // Set rebuilding status
    await context.CallActivityAsync("SetProjectionStatus",
        (projectionName, ProjectionStatus.Rebuilding));

    // Discover all work items
    var workItems = await context.CallActivityAsync<List<CatchUpWorkItem>>(
        "DiscoverWorkItems", projectionName);

    // Process in batches
    var batches = workItems.Chunk(100);
    foreach (var batch in batches)
    {
        var tasks = batch.Select(item =>
            context.CallActivityAsync("ProcessWorkItem", item));
        await Task.WhenAll(tasks);
    }

    // Set active status
    await context.CallActivityAsync("SetProjectionStatus",
        (projectionName, ProjectionStatus.Active));
}

[Function("SetProjectionStatus")]
public async Task SetStatus(
    [ActivityTrigger] (string ProjectionName, ProjectionStatus Status) input,
    [FromServices] IServiceProvider services)
{
    var factory = services.GetRequiredService<IProjectionFactory<OrderDashboard>>();
    await factory.SetStatusAsync(input.Status);
}
```

## Schema Migration Strategies

### Strategy 1: Full Rebuild

Best for small projections or simple changes:

```csharp
if (projection.NeedsSchemaUpgrade)
{
    // Delete existing and rebuild from scratch
    await factory.DeleteAsync();
    await rebuildService.RebuildAsync();
}
```

### Strategy 2: In-Place Migration

For large projections where you can migrate data:

```csharp
if (projection.NeedsSchemaUpgrade)
{
    // Migrate existing data
    foreach (var order in projection.Orders.Values)
    {
        // Calculate new fields from existing data
        order.TotalRevenue = CalculateRevenue(order);
    }

    // Update schema version
    projection.SchemaVersion = projection.CodeSchemaVersion;
    await factory.SaveAsync(projection);
}
```

### Strategy 3: Dual-Write During Transition

For zero-downtime migrations:

```csharp
// During transition period, write to both versions
if (projection.NeedsSchemaUpgrade)
{
    // Write to old projection (v1)
    await oldFactory.SaveAsync(projectionV1);

    // Write to new projection (v2)
    await newFactory.SaveAsync(projectionV2);
}
```

## Monitoring and Alerting

### Health Check

```csharp
public class ProjectionHealthCheck : IHealthCheck
{
    private readonly IProjectionFactory<OrderDashboard> _factory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        var status = await _factory.GetStatusAsync(cancellationToken: ct);

        return status switch
        {
            ProjectionStatus.Active => HealthCheckResult.Healthy(),
            ProjectionStatus.Rebuilding => HealthCheckResult.Degraded(
                "Projection is being rebuilt"),
            ProjectionStatus.Disabled => HealthCheckResult.Unhealthy(
                "Projection is disabled"),
            _ => HealthCheckResult.Unhealthy("Unknown status")
        };
    }
}
```

### Startup Check

```csharp
public class ProjectionStartupCheck : IHostedService
{
    private readonly IEnumerable<IProjectionFactory> _factories;
    private readonly ILogger<ProjectionStartupCheck> _logger;

    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var factory in _factories)
        {
            var projection = await factory.GetOrCreateProjectionAsync(
                _docFactory, _eventStreamFactory, cancellationToken: ct);

            if (projection.NeedsSchemaUpgrade)
            {
                _logger.LogWarning(
                    "Projection {Type} needs schema upgrade: v{Stored} -> v{Code}",
                    factory.ProjectionType.Name,
                    projection.SchemaVersion,
                    projection.CodeSchemaVersion);

                // Optionally trigger rebuild automatically
                // await rebuildService.QueueRebuildAsync(factory.ProjectionType);
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

## Best Practices

### Do

- Set `Rebuilding` status before starting rebuild
- Handle `ProjectionUpdateResult.Skipped` appropriately
- Queue skipped events for retry when status is `Rebuilding`
- Increment version for any breaking schema change
- Test rebuild workflow in staging environment
- Monitor rebuild progress and duration

### Don't

- Don't leave projections in `Rebuilding` status indefinitely
- Don't ignore `NeedsSchemaUpgrade` in production
- Don't increment version for additive-only changes (when safe)
- Don't perform rebuilds during peak traffic
- Don't skip validation after rebuild completes

## See Also

- [Projections](Projections.md) - Basic projection guide
- [Projection Catch-Up](ProjectionCatchUp.md) - Catch-up discovery service
- [Storage Providers](StorageProviders.md) - Factory implementations
- [Azure Functions](AzureFunctions.md) - Durable Functions patterns
