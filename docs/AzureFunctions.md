# Azure Functions Integration

The `ErikLieben.FA.ES.Azure.Functions.Worker.Extensions` package provides bindings for using event sourcing in Azure Functions (isolated worker model).

## Overview

The package provides:

- **[EventStreamInput]** - Load aggregates from event streams
- **[ProjectionInput]** - Load projections from storage
- **[ProjectionOutput<T>]** - Update projections after function execution

## Installation

```bash
dotnet add package ErikLieben.FA.ES.Azure.Functions.Worker.Extensions
```

## Setup

### 1. Configure Program.cs

```csharp
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.AzureStorage;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure Azure Blob Storage
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(connectionString)
        .WithName("Store");
});

// Configure Event Store services
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store", autoCreateContainer: true));
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// Configure Azure Functions bindings
builder.ConfigureEventStoreBindings();

// Register your domain factories
builder.Services.ConfigureMyDomainFactory();

// Register projection factories for [ProjectionInput] binding
builder.Services.AddSingleton<ProjectDashboardFactory>();
builder.Services.AddSingleton<IProjectionFactory<ProjectDashboard>>(
    sp => sp.GetRequiredService<ProjectDashboardFactory>());
builder.Services.AddSingleton<IProjectionFactory>(
    sp => sp.GetRequiredService<ProjectDashboardFactory>());

await builder.Build().RunAsync();
```

### 2. Required Registrations

For each projection type you want to use with `[ProjectionInput]`, register:

```csharp
// The generated factory
builder.Services.AddSingleton<YourProjectionFactory>();

// As typed factory
builder.Services.AddSingleton<IProjectionFactory<YourProjection>>(
    sp => sp.GetRequiredService<YourProjectionFactory>());

// As generic factory (required for binding resolution)
builder.Services.AddSingleton<IProjectionFactory>(
    sp => sp.GetRequiredService<YourProjectionFactory>());
```

## EventStreamInput Binding

Load aggregates automatically from event streams:

```csharp
[Function("GetWorkItem")]
public async Task<HttpResponseData> GetWorkItem(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "workitems/{id}")] HttpRequestData req,
    string id,
    [EventStreamInput("{id}")] WorkItem workItem)
{
    if (workItem.Metadata?.Id == null)
    {
        var notFound = req.CreateResponse(HttpStatusCode.NotFound);
        await notFound.WriteAsJsonAsync(new { error = "Work item not found" });
        return notFound;
    }

    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new
    {
        id = workItem.Metadata.Id.Value,
        title = workItem.Title,
        status = workItem.Status.ToString()
    });

    return response;
}
```

### Attribute Properties

| Property | Description |
|----------|-------------|
| `ObjectId` | The object identifier (supports route parameter binding like `{id}`) |
| `ObjectType` | Optional object type name for document resolution |
| `Connection` | Connection configuration name |
| `DocumentType` | Document type for factory selection |
| `DefaultStreamType` | Default stream type |
| `DefaultStreamConnection` | Default connection for streams |
| `CreateEmptyObjectWhenNonExistent` | Create new if doesn't exist (default: false) |

### Modifying Aggregates

```csharp
[Function("AssignWorkItem")]
public async Task<HttpResponseData> AssignWorkItem(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workitems/{id}/assign")] HttpRequestData req,
    string id,
    [EventStreamInput("{id}")] WorkItem workItem)
{
    var request = await req.ReadFromJsonAsync<AssignRequest>();

    // The aggregate is loaded - call domain methods
    await workItem.AssignResponsibility(request.MemberId);

    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new { success = true });
    return response;
}
```

### Creating New Aggregates

For creation, use the factory directly:

```csharp
[Function("CreateWorkItem")]
public async Task<HttpResponseData> CreateWorkItem(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workitems")] HttpRequestData req,
    [FromServices] IWorkItemFactory workItemFactory)
{
    var request = await req.ReadFromJsonAsync<CreateRequest>();
    var id = WorkItemId.New();

    var workItem = await workItemFactory.CreateAsync(id.Value.ToString());
    await workItem.Plan(request.Title, request.Description, request.ProjectId);

    var response = req.CreateResponse(HttpStatusCode.Created);
    response.Headers.Add("Location", $"/api/workitems/{id.Value}");
    await response.WriteAsJsonAsync(new { id = id.Value });
    return response;
}
```

## ProjectionInput Binding

Load projections automatically:

```csharp
[Function("GetKanbanBoard")]
public async Task<HttpResponseData> GetKanbanBoard(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projections/kanban")] HttpRequestData req,
    [ProjectionInput] ProjectKanbanBoard kanbanBoard)
{
    var response = req.CreateResponse(HttpStatusCode.OK);
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
```

### Attribute Properties

| Property | Description |
|----------|-------------|
| `BlobName` | Optional specific blob name to load from |
| `CreateIfNotExists` | Create new projection if not found (default: true) |

### Multiple Projection Types

```csharp
[Function("GetActiveWorkItems")]
public async Task<HttpResponseData> GetActiveWorkItems(
    [HttpTrigger(...)] HttpRequestData req,
    [ProjectionInput] ActiveWorkItems activeWorkItems)
{
    // ...
}

[Function("GetUserProfiles")]
public async Task<HttpResponseData> GetUserProfiles(
    [HttpTrigger(...)] HttpRequestData req,
    [ProjectionInput] UserProfiles userProfiles)
{
    // Routed projections work too
    var allProfiles = userProfiles.GetAllProfiles();
    // ...
}
```

### Non-HTTP Triggers

```csharp
[Function("ProcessProjectionUpdate")]
public async Task ProcessProjectionUpdate(
    [QueueTrigger("projection-updates")] ProjectionUpdateMessage message,
    [ProjectionInput] ProjectKanbanBoard kanbanBoard)
{
    // Process queue message with access to projection
    logger.LogInformation(
        "Processing update for {ProjectId}",
        message.ProjectId);
}
```

## ProjectionOutput Binding

Update projections after function execution:

```csharp
[Function("RefreshProjections")]
[ProjectionOutput<ActiveWorkItems>]
[ProjectionOutput<ProjectKanbanBoard>]
public async Task<HttpResponseData> RefreshProjections(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projections/refresh")] HttpRequestData req)
{
    // The actual projection updates happen in middleware after function returns

    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new
    {
        success = true,
        message = "Projection refresh triggered",
        projections = new[] { nameof(ActiveWorkItems), nameof(ProjectKanbanBoard) }
    });

    return response;
}
```

### Attribute Properties

| Property | Description |
|----------|-------------|
| `BlobName` | Optional blob name for routed projections |
| `SaveAfterUpdate` | Save projection after updating (default: true) |

### Multiple Projections

Apply multiple attributes to update multiple projections:

```csharp
[Function("CreateWorkItem")]
[ProjectionOutput<ProjectKanbanBoard>]
[ProjectionOutput<ActiveWorkItems>]
[ProjectionOutput<ProjectDashboard>]
public async Task<HttpResponseData> CreateWorkItem(...)
{
    // All three projections update after successful execution
}
```

### How It Works

1. Function executes normally
2. If successful, middleware loads each marked projection
3. Calls `UpdateToLatestVersion()` on each
4. Saves updated projections to storage
5. If any update fails, exception is thrown

## Complete Example

```csharp
public class WorkItemFunctions
{
    private readonly ILogger<WorkItemFunctions> _logger;

    public WorkItemFunctions(ILogger<WorkItemFunctions> logger)
    {
        _logger = logger;
    }

    [Function("GetWorkItem")]
    public async Task<HttpResponseData> GetWorkItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "workitems/{id}")] HttpRequestData req,
        string id,
        [EventStreamInput("{id}")] WorkItem workItem)
    {
        _logger.LogInformation("Getting work item {WorkItemId}", id);

        if (workItem.Metadata?.Id == null)
        {
            return await NotFound(req, "Work item not found");
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            id = workItem.Metadata.Id.Value,
            title = workItem.Title,
            description = workItem.Description,
            status = workItem.Status.ToString(),
            priority = workItem.Priority.ToString(),
            assignedTo = workItem.AssignedTo
        });

        return response;
    }

    [Function("CompleteWorkItem")]
    [ProjectionOutput<ActiveWorkItems>]
    [ProjectionOutput<ProjectKanbanBoard>]
    public async Task<HttpResponseData> CompleteWorkItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workitems/{id}/complete")] HttpRequestData req,
        string id,
        [EventStreamInput("{id}")] WorkItem workItem)
    {
        _logger.LogInformation("Completing work item {WorkItemId}", id);

        if (workItem.Metadata?.Id == null)
        {
            return await NotFound(req, "Work item not found");
        }

        await workItem.Complete("user-123");

        // Projections will be updated after this returns

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { success = true, message = "Work item completed" });
        return response;
    }

    private static async Task<HttpResponseData> NotFound(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.NotFound);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }
}
```

## Aspire Integration

When using .NET Aspire:

```csharp
var builder = FunctionsApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Configure from Aspire connection strings
builder.Services.AddAzureClients(clientBuilder =>
{
    var connectionString = builder.Configuration.GetConnectionString("events");
    clientBuilder.AddBlobServiceClient(connectionString).WithName("Store");
});

// Rest of configuration...
```

## Best Practices

### Do

- Register projection factories for all projection types you'll use
- Use `[ProjectionOutput<T>]` for immediate consistency
- Keep functions focused on single operations
- Use `[FromServices]` for factories when creating new aggregates
- Handle missing aggregate/projection cases gracefully

### Don't

- Don't use `[EventStreamInput]` for creating new aggregates
- Don't mix multiple aggregate modifications in one function
- Don't forget to register projection factories
- Don't rely on eventual consistency if immediate updates needed

## Troubleshooting

### "No factory found for projection type"

Register the projection factory:

```csharp
builder.Services.AddSingleton<YourProjectionFactory>();
builder.Services.AddSingleton<IProjectionFactory<YourProjection>>(
    sp => sp.GetRequiredService<YourProjectionFactory>());
builder.Services.AddSingleton<IProjectionFactory>(
    sp => sp.GetRequiredService<YourProjectionFactory>());
```

### "Cannot bind parameter"

Ensure:
1. `builder.ConfigureEventStoreBindings()` is called
2. The aggregate/projection type is registered
3. Route parameter names match binding expressions

### "Aggregate not found"

For `[EventStreamInput]`, set `CreateEmptyObjectWhenNonExistent = true` or handle null case in function.

## See Also

- [Getting Started](GettingStarted.md) - Basic setup
- [Aggregates](Aggregates.md) - Aggregate patterns
- [Projections](Projections.md) - Projection types
- [Minimal APIs](MinimalApis.md) - ASP.NET Core integration
