# Routed Projections

Routed projections split data across multiple destination files based on event content. Use them when data needs to be partitioned for efficient querying or when data volume requires splitting.

## Overview

A routed projection consists of:

- **Router** - The main projection that receives all events and decides routing
- **Destinations** - Child projections that store partitioned data
- **Registry** - Tracks all destinations and their metadata

## When to Use Routed Projections

| Scenario | Simple Projection | Routed Projection |
|----------|-------------------|-------------------|
| Single dashboard | Yes | No |
| Per-user data | No | Yes |
| Per-project data | No | Yes |
| Large datasets | No | Yes |
| Sharded queries | No | Yes |

## Basic Structure

### Router Definition

```csharp
[BlobJsonProjection("projections/kanban.json")]
[ProjectionWithExternalCheckpoint]
public partial class ProjectKanbanBoard : RoutedProjection
{
    // Global state for routing decisions
    public Dictionary<string, ProjectInfo> Projects { get; } = new();
    private readonly Dictionary<string, string> workItemToProject = new();

    // Create destination when project is created
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectInitiated @event, string projectId)
    {
        Projects[projectId] = new ProjectInfo { Id = projectId, Name = @event.Name };

        // Create destination with metadata for path resolution
        AddDestination<ProjectKanbanDestination>(
            projectId,
            new Dictionary<string, string> { ["projectId"] = projectId });
    }

    // Route events to appropriate destination
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {
        workItemToProject[workItemId] = @event.ProjectId;
        RouteToDestination(@event.ProjectId);
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemCompleted @event, string workItemId)
    {
        if (workItemToProject.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }
}
```

### Destination Definition

```csharp
[BlobJsonProjection("projections/kanban/project-{projectId}.json")]
public partial class ProjectKanbanDestination : Projection
{
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
    private void When(WorkItemCompleted @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.Status = WorkItemStatus.Completed;
        }
    }
}
```

## Core Methods

### AddDestination

Create a new destination:

```csharp
// Simple destination
AddDestination<MyDestination>(destinationKey);

// With metadata for path resolution
AddDestination<MyDestination>(destinationKey, new Dictionary<string, string>
{
    ["projectId"] = projectId,
    ["region"] = region
});
```

The metadata is used in path templates like `projections/{projectId}/{region}.json`.

### RouteToDestination

Route events to destinations:

```csharp
// Route current event
RouteToDestination(destinationKey);

// Route a custom event
RouteToDestination(destinationKey, customEvent);

// Route with execution context
RouteToDestination(destinationKey, executionContext);

// Route to multiple destinations
RouteToDestinations("dest1", "dest2", "dest3");
```

## Querying Destinations

### Get All Keys

```csharp
var keys = projection.GetDestinationKeys();

foreach (var key in keys)
{
    Console.WriteLine($"Destination: {key}");
}
```

### Get Specific Destination

```csharp
if (projection.TryGetDestination<ProjectKanbanDestination>(projectId, out var destination))
{
    var workItems = destination.WorkItems.Values;
    // Use destination data...
}
```

### Clear Destinations

```csharp
projection.ClearDestinations();
```

## Destination Registry

The registry tracks all destinations:

```csharp
// Access registry
var registry = projection.Registry;

// Check destination metadata
if (registry.Destinations.TryGetValue(destinationKey, out var metadata))
{
    Console.WriteLine($"Created: {metadata.CreatedAt}");
    Console.WriteLine($"Modified: {metadata.LastModified}");
    Console.WriteLine($"Type: {metadata.DestinationTypeName}");
}
```

### DestinationMetadata

| Property | Type | Description |
|----------|------|-------------|
| `DestinationTypeName` | string | Type name of destination |
| `CreatedAt` | DateTimeOffset | When destination was created |
| `LastModified` | DateTimeOffset | Last modification time |
| `UserMetadata` | Dictionary | Custom metadata |
| `CheckpointFingerprint` | string | Checkpoint hash |

## Path Templates

Use placeholders in projection paths:

```csharp
[BlobJsonProjection("projections/{projectId}/kanban.json")]
public partial class ProjectKanbanDestination : Projection { }

[BlobJsonProjection("projections/{region}/{year}/{month}.json")]
public partial class MonthlyReport : Projection { }
```

Placeholders are resolved from the metadata dictionary passed to `AddDestination`.

## Patterns

### Paging Pattern

Split users across pages:

```csharp
public partial class UserProfiles : RoutedProjection
{
    private const int UsersPerPage = 100;

    public int TotalUsers { get; set; }
    public int TotalPages { get; set; }

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

### Multi-Tenant Pattern

Separate data by tenant:

```csharp
public partial class TenantData : RoutedProjection
{
    private readonly Dictionary<string, string> entityToTenant = new();

    private void When(TenantCreated @event, string tenantId)
    {
        AddDestination<TenantDataDestination>(
            tenantId,
            new Dictionary<string, string> { ["tenantId"] = tenantId });
    }

    private void When(EntityCreated @event, string entityId)
    {
        entityToTenant[entityId] = @event.TenantId;
        RouteToDestination(@event.TenantId);
    }
}
```

### Region-Based Pattern

Partition by geographic region:

```csharp
public partial class RegionalData : RoutedProjection
{
    private void When(OrderCreated @event, string orderId)
    {
        var region = DetermineRegion(@event.ShippingAddress);

        // Ensure destination exists
        if (!Registry.Destinations.ContainsKey(region))
        {
            AddDestination<RegionalOrdersDestination>(
                region,
                new Dictionary<string, string> { ["region"] = region });
        }

        RouteToDestination(region);
    }

    private static string DetermineRegion(Address address)
    {
        // Logic to determine region from address
        return address.Country switch
        {
            "US" or "CA" or "MX" => "north-america",
            "GB" or "DE" or "FR" => "europe",
            _ => "other"
        };
    }
}
```

## ASP.NET Core Integration

Use with Minimal APIs:

```csharp
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

app.MapGet("/kanban/all-projects", async (
    [Projection] ProjectKanbanBoard kanban) =>
{
    var allProjects = kanban.GetDestinationKeys()
        .Select(key =>
        {
            kanban.TryGetDestination<ProjectKanbanDestination>(key, out var dest);
            return new { ProjectId = key, WorkItemCount = dest?.WorkItems.Count ?? 0 };
        });

    return Results.Ok(allProjects);
});
```

## Best Practices

### Do

- Create destinations lazily when first event for that partition arrives
- Keep router state minimal (just enough for routing decisions)
- Use meaningful destination keys
- Include path template metadata when creating destinations
- Run `dotnet faes` after changes

### Don't

- Don't create all possible destinations upfront
- Don't store large data in the router
- Don't use sequential numeric keys without purpose
- Don't route events to non-existent destinations

## Error Handling

```csharp
private void When(WorkItemUpdated @event, string workItemId)
{
    // Safe routing with existence check
    if (workItemToProject.TryGetValue(workItemId, out var projectId))
    {
        // Destination might not exist if work item was created before routing
        if (Registry.Destinations.ContainsKey(projectId))
        {
            RouteToDestination(projectId);
        }
    }
}
```

## See Also

- [Projections](Projections.md) - Basic projection patterns
- [Configuration](Configuration.md) - Storage configuration
- [Minimal APIs](MinimalApis.md) - API integration
- [Testing](Testing.md) - Testing projections
