# Projections

Projections are read models that aggregate data from event streams into queryable state. They transform events into views optimized for reading.

## Overview

A projection in ErikLieben.FA.ES:

1. **Subscribes to events** - Listens for events from one or more event streams
2. **Builds read models** - Aggregates data into queryable structures
3. **Maintains checkpoints** - Tracks which events have been processed
4. **Provides query methods** - Exposes data through type-safe APIs

## Projection Types

| Type | Base Class | Use Case |
|------|------------|----------|
| Simple Projection | `Projection` | Single file, collects data from multiple streams |
| Routed Projection | `RoutedProjection` | Splits data across multiple destination files |

## Basic Structure

```csharp
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Projections;

namespace MyApp.Projections;

[BlobJsonProjection("projections", Connection = "BlobStorage")]
[ProjectionWithExternalCheckpoint]
public partial class OrderDashboard : Projection
{
    // State properties
    public Dictionary<string, OrderSummary> Orders { get; } = new();
    public int TotalOrders { get; private set; }

    // Event handlers (When methods)
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(OrderCreated @event, string orderId)
    {
        Orders[orderId] = new OrderSummary { OrderId = orderId };
        TotalOrders++;
    }

    // Query methods
    public IEnumerable<OrderSummary> GetActiveOrders()
    {
        return Orders.Values.Where(o => !o.IsCompleted);
    }
}
```

## Required Elements

### 1. Attributes

```csharp
[BlobJsonProjection("projections", Connection = "BlobStorage")]  // Storage config
[ProjectionWithExternalCheckpoint]                                // Checkpoint storage
public partial class OrderDashboard : Projection                  // Must be partial
```

### 2. Storage Attributes

| Attribute | Storage | Description |
|-----------|---------|-------------|
| `[BlobJsonProjection("path")]` | Azure Blob | Stores as JSON in blob storage |
| `[CosmosDbJsonProjection("container")]` | Cosmos DB | Stores in Cosmos DB container |
| `[TableProjection("table")]` | Azure Table | Stores in table storage |

### 3. Connection Parameter

Specify which connection string to use:

```csharp
[BlobJsonProjection("projections", Connection = "BlobStorage")]
[BlobJsonProjection("projections", Connection = "CustomConnection")]
```

### 4. When Methods

When methods process events and update projection state:

```csharp
// Basic When method with object ID parameter
[WhenParameterValueFactory<ObjectIdWhenValueFactory>]
private void When(OrderCreated @event, string orderId)
{
    // orderId is extracted from the event stream's object identifier
    Orders[orderId] = new OrderSummary
    {
        OrderId = orderId,
        CustomerId = @event.CustomerId,
        CreatedAt = @event.CreatedAt
    };
}

// When method without parameter (uses event data only)
private void When(SystemConfigured @event)
{
    ConfiguredAt = @event.ConfiguredAt;
}
```

**Key rules:**
- Always `private void When(EventType @event[, parameters])`
- Use `[WhenParameterValueFactory<T>]` to inject values like object IDs
- No async operations - state changes only
- No side effects - deterministic state rebuilding

## Simple Projections

Simple projections collect data into a single file:

```csharp
[BlobJsonProjection("projections/dashboard.json")]
[ProjectionWithExternalCheckpoint]
public partial class ProjectDashboard : Projection
{
    public Dictionary<string, ProjectMetrics> Projects { get; } = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectInitiated @event, string projectId)
    {
        Projects[projectId] = new ProjectMetrics
        {
            ProjectId = projectId,
            Name = @event.Name,
            CreatedAt = @event.InitiatedAt
        };
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectCompleted @event, string projectId)
    {
        if (Projects.TryGetValue(projectId, out var project))
        {
            project.IsCompleted = true;
            project.CompletedAt = @event.CompletedAt;
        }
    }

    // Query methods
    public IEnumerable<ProjectMetrics> GetActiveProjects()
    {
        return Projects.Values.Where(p => !p.IsCompleted);
    }

    public ProjectMetrics? GetProject(string projectId)
    {
        return Projects.TryGetValue(projectId, out var p) ? p : null;
    }
}
```

## Routed Projections

Routed projections split data across multiple destination files. Use when:

- Data needs to be partitioned (e.g., per user, per project)
- You want to load only relevant subsets
- Data volume requires splitting

### Structure

```csharp
[BlobJsonProjection("projections/kanban.json")]
[ProjectionWithExternalCheckpoint]
public partial class ProjectKanbanBoard : RoutedProjection
{
    // Main projection tracks metadata
    public Dictionary<string, ProjectInfo> Projects { get; } = new();

    // Track mappings for routing decisions
    private readonly Dictionary<string, string> workItemToProjectMap = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectInitiated @event, string projectId)
    {
        // Track in main projection
        Projects[projectId] = new ProjectInfo { Id = projectId, Name = @event.Name };

        // Create destination for this project's data
        AddDestination<ProjectKanbanDestination>(
            projectId,
            new Dictionary<string, string> { ["projectId"] = projectId });
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {
        // Track mapping
        workItemToProjectMap[workItemId] = @event.ProjectId;

        // Route event to the project's destination
        RouteToDestination(@event.ProjectId);
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCommenced @event, string workItemId)
    {
        if (workItemToProjectMap.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }
}
```

### Destination Types

Destinations are regular projections that receive routed events:

```csharp
[BlobJsonProjection("projections/kanban/project-{projectId}.json")]
public partial class ProjectKanbanDestination : Projection
{
    public List<string> AvailableLanguages { get; set; } = ["en-US"];
    public Dictionary<string, KanbanWorkItem> WorkItems { get; } = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {
        WorkItems[workItemId] = new KanbanWorkItem
        {
            Id = workItemId,
            Title = @event.Title,
            Status = WorkItemStatus.Planned
        };
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCommenced @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.Status = WorkItemStatus.InProgress;
        }
    }
}
```

### Routing Methods

| Method | Description |
|--------|-------------|
| `AddDestination<T>(key)` | Create a new destination |
| `AddDestination<T>(key, metadata)` | Create with metadata for path resolution |
| `RouteToDestination(key)` | Route current event to destination |
| `RouteToDestination(key, customEvent)` | Route a different event |
| `RouteToDestination(key, context)` | Route with execution context |
| `RouteToDestinations(keys)` | Route to multiple destinations |

### Querying Destinations

```csharp
// Get all destination keys
var keys = projection.GetDestinationKeys();

// Try to get a specific destination
if (projection.TryGetDestination<ProjectKanbanDestination>("project-1", out var dest))
{
    var workItems = dest.WorkItems.Values;
}
```

## Cosmos DB Projections

For Cosmos DB storage:

```csharp
[CosmosDbJsonProjection("projections", Connection = "cosmosdb")]
[ProjectionWithExternalCheckpoint]
public partial class SprintDashboard : Projection
{
    public Dictionary<string, SprintSummary> Sprints { get; } = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(SprintCreated @event, string sprintId)
    {
        Sprints[sprintId] = new SprintSummary
        {
            SprintId = sprintId,
            Name = @event.Name,
            Status = SprintStatus.Planned
        };
    }
}
```

## Checkpoints

Projections track which events have been processed using checkpoints.

### External Checkpoints

Use `[ProjectionWithExternalCheckpoint]` to store checkpoints separately:

```csharp
[BlobJsonProjection("projections")]
[ProjectionWithExternalCheckpoint]  // Checkpoint stored in separate file
public partial class Dashboard : Projection { }
```

### Checkpoint Structure

```csharp
// Checkpoint maps object identifiers to version identifiers
// e.g., "project__order-123" -> "order__order-123__5"
public abstract Checkpoint Checkpoint { get; set; }

// Fingerprint for change detection
public string? CheckpointFingerprint { get; set; }
```

## Projection Status

Projections support operational status for coordinating rebuilds with inline updates.

### Status Values

| Status | Value | Description |
|--------|-------|-------------|
| `Active` | 0 | Normal operation, inline updates processed |
| `Rebuilding` | 1 | Being rebuilt, inline updates skipped |
| `Disabled` | 2 | Turned off, all updates skipped |

### Setting Status

```csharp
// Before starting a rebuild
await factory.SetStatusAsync(ProjectionStatus.Rebuilding);

// After rebuild completes
await factory.SetStatusAsync(ProjectionStatus.Active);

// Temporarily disable a projection
await factory.SetStatusAsync(ProjectionStatus.Disabled);
```

### Checking Status

```csharp
var status = await factory.GetStatusAsync();
if (status == ProjectionStatus.Rebuilding)
{
    // Handle rebuilding state
}
```

### Update Results

When calling `UpdateToVersion`, the result indicates if the update was processed:

```csharp
var result = await projection.UpdateToVersion(token);
if (result.Skipped)
{
    // Update was skipped due to status
    logger.LogWarning("Update skipped: {Status}, Token: {Token}",
        result.Status, result.SkippedToken);

    // Queue for retry if needed
    await retryQueue.EnqueueAsync(result.SkippedToken);
}
```

## Schema Versioning

Track projection schema versions to detect when rebuilds are needed due to code changes.

### Using ProjectionVersion Attribute

```csharp
[BlobJsonProjection("projections")]
[ProjectionVersion(2)]  // Increment when schema changes
public partial class OrderDashboard : Projection
{
    // New field added in v2
    public decimal TotalRevenue { get; private set; }
}
```

### Detecting Schema Upgrades

```csharp
var projection = await factory.GetOrCreateAsync(docFactory, eventFactory);

if (projection.NeedsSchemaUpgrade)
{
    logger.LogWarning(
        "Projection {Name} needs rebuild: stored v{Stored}, code v{Code}",
        typeof(OrderDashboard).Name,
        projection.SchemaVersion,
        projection.CodeSchemaVersion);

    // Trigger rebuild workflow
    await rebuildService.QueueRebuildAsync<OrderDashboard>();
}
```

### Properties

| Property | Description |
|----------|-------------|
| `SchemaVersion` | Version stored in the projection |
| `CodeSchemaVersion` | Version from `[ProjectionVersion]` attribute |
| `NeedsSchemaUpgrade` | True if versions don't match |

### When to Increment Version

Increment the projection version when:
- Adding new properties that need historical data
- Changing how events are processed
- Removing or renaming properties
- Changing the structure of stored data

See [Projection Status and Versioning](ProjectionStatusAndVersioning.md) for detailed rebuild patterns.

## Parameter Value Factories

Inject values into When methods using factories:

### ObjectIdWhenValueFactory

Extracts the object ID from the event stream:

```csharp
[WhenParameterValueFactory<ObjectIdWhenValueFactory>]
private void When(OrderCreated @event, string orderId)
{
    // orderId = "order-123" (from the event stream)
}
```

### Custom Factories

Create custom factories for complex scenarios:

```csharp
public class CustomWhenValueFactory : IProjectionWhenParameterValueFactoryWithVersionToken<CustomContext>
{
    public CustomContext? Create(VersionToken versionToken, IEvent @event)
    {
        return new CustomContext
        {
            ObjectId = versionToken.ObjectId,
            Version = versionToken.Version
        };
    }
}

// Usage
[WhenParameterValueFactory<CustomWhenValueFactory>]
private void When(OrderCreated @event, CustomContext context) { }
```

## Projection Factory

The CLI generates a factory for loading projections:

```csharp
public class DashboardService
{
    private readonly IProjectionFactory<OrderDashboard> _factory;

    public DashboardService(IProjectionFactory<OrderDashboard> factory)
    {
        _factory = factory;
    }

    public async Task<OrderDashboard> GetDashboard()
    {
        return await _factory.GetAsync();
    }
}
```

## ASP.NET Core Integration

Use the `[Projection]` attribute for automatic binding:

```csharp
// In your endpoint
app.MapGet("/dashboard", async (
    [Projection] OrderDashboard dashboard) =>
{
    return dashboard.GetActiveOrders();
});

app.MapGet("/projects/{projectId}/kanban", async (
    string projectId,
    [Projection] ProjectKanbanBoard kanban) =>
{
    if (kanban.TryGetDestination<ProjectKanbanDestination>(projectId, out var dest))
    {
        return Results.Ok(dest.WorkItems.Values);
    }
    return Results.NotFound();
});
```

## Best Practices

### Do

- Keep projections focused on a single read model
- Use meaningful query method names
- Initialize collections in property declarations
- Use private setters for state properties
- Run `dotnet faes` after any changes
- Use routed projections for large datasets

### Don't

- Don't call external services in When methods
- Don't make projections non-partial
- Don't throw exceptions in When methods
- Don't put async code in When methods
- Don't modify state outside of When methods
- Don't store sensitive data without encryption

## Common Patterns

### Cross-Stream Aggregation

Track data from multiple event streams:

```csharp
public partial class Dashboard : Projection
{
    public Dictionary<string, ProjectMetrics> Projects { get; } = new();
    private Dictionary<string, string> WorkItemProjects { get; } = new();

    // Project events
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectInitiated @event, string projectId)
    {
        Projects[projectId] = new ProjectMetrics { ProjectId = projectId };
    }

    // Work item events (from different stream type)
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {
        // Link work item to project
        WorkItemProjects[workItemId] = @event.ProjectId;

        if (Projects.TryGetValue(@event.ProjectId, out var project))
        {
            project.TotalWorkItems++;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkCompleted @event, string workItemId)
    {
        if (WorkItemProjects.TryGetValue(workItemId, out var projectId) &&
            Projects.TryGetValue(projectId, out var project))
        {
            project.CompletedWorkItems++;
        }
    }
}
```

### Paging with Routed Projections

Split users across pages:

```csharp
[BlobJsonProjection("projections/users.json")]
public partial class UserProfiles : RoutedProjection
{
    private const int UsersPerPage = 10;

    public int TotalPages { get; set; }
    public int TotalUsers { get; set; }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(UserCreated @event, string userId)
    {
        int pageNumber = (TotalUsers / UsersPerPage) + 1;
        var pageKey = $"page-{pageNumber}";

        AddDestination<UserProfilePage>(
            pageKey,
            new Dictionary<string, string> { ["pageNumber"] = pageNumber.ToString() });

        TotalUsers++;
        TotalPages = Math.Max(TotalPages, pageNumber);

        RouteToDestination(pageKey);
    }
}
```

### Statistics Tracking

```csharp
public partial class SprintDashboard : Projection
{
    public Dictionary<string, SprintSummary> Sprints { get; } = new();

    public SprintStatistics GetStatistics()
    {
        var sprints = Sprints.Values.ToList();
        return new SprintStatistics
        {
            TotalSprints = sprints.Count,
            ActiveSprints = sprints.Count(s => s.Status == SprintStatus.Active),
            CompletedSprints = sprints.Count(s => s.Status == SprintStatus.Completed),
            CompletionRate = sprints.Count > 0
                ? (double)sprints.Count(s => s.Status == SprintStatus.Completed) / sprints.Count * 100
                : 0
        };
    }
}
```

## Testing

Use the `ProjectionTestBuilder` for testing:

```csharp
[Fact]
public async Task Dashboard_ShouldTrackOrders()
{
    var context = TestSetup.GetContext();

    await ProjectionTestBuilder.For<OrderDashboard>(context)
        .Given(
            ("order-1", new OrderCreated("customer-1", DateTime.UtcNow)),
            ("order-2", new OrderCreated("customer-2", DateTime.UtcNow)))
        .Then(projection =>
        {
            Assert.Equal(2, projection.TotalOrders);
            Assert.Equal(2, projection.Orders.Count);
        });
}
```

See [Testing](Testing.md) for complete testing guide.

## See Also

- [Getting Started](GettingStarted.md) - Quick setup guide
- [Aggregates](Aggregates.md) - Event emitting aggregates
- [Storage Providers](StorageProviders.md) - Configure storage
- [Testing](Testing.md) - Unit testing projections
- [Minimal APIs](MinimalApis.md) - ASP.NET Core integration
- [Projection Status and Versioning](ProjectionStatusAndVersioning.md) - Rebuild workflows
- [Projection Catch-Up](ProjectionCatchUp.md) - Catch-up discovery service
