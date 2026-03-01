# Migration Guide: v1.x to v2.0

This guide covers migrating from ErikLieben.FA.ES v1.x to v2.0. The v2.0 release introduces significant new features while maintaining backward compatibility with deprecation warnings for APIs that will be removed in future versions.

## Breaking Changes

### Package Changes

v2.0 introduces new packages:

| New Package | Purpose |
|-------------|---------|
| `ErikLieben.FA.ES.CosmosDb` | Cosmos DB storage provider |
| `ErikLieben.FA.ES.EventStreamManagement` | Migration, backup, and stream repair |
| `ErikLieben.FA.ES.AspNetCore.MinimalApis` | ASP.NET Core Minimal APIs integration |
| `ErikLieben.FA.ES.CodeAnalysis` | Shared Roslyn analysis utilities |
| `ErikLieben.FA.ES.Benchmarks` | Performance benchmarks |

### .NET Version Requirements

v2.0 targets:
- .NET 9.0
- .NET 10.0 (preview support)

v1.x targets are no longer supported.

## Deprecations

### Projection Fold Methods

The `IObjectDocument`-based Fold methods are deprecated in favor of `VersionToken`-based methods:

```csharp
// v1.x - Deprecated
public Task Fold<T>(IEvent @event, IObjectDocument document, T? data = null,
    IExecutionContext? context = null);

// v2.0 - New preferred method
public Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null,
    IExecutionContext? context = null);
```

**Action Required**: Update your projection implementations to use VersionToken:

```csharp
// Before (v1.x)
await projection.Fold(@event, document);

// After (v2.0)
var versionToken = new VersionToken(@event, document);
await projection.Fold(@event, versionToken);
```

### GetWhenParameterValue Method

```csharp
// v1.x - Deprecated
protected T? GetWhenParameterValue<T, Te>(string forType, IObjectDocument document, IEvent @event);

// v2.0 - New preferred method
protected T? GetWhenParameterValue<T, Te>(string forType, VersionToken versionToken, IEvent @event);
```

### Factory Retrieval Methods → Repository

The retrieval and query methods on the aggregate factory are deprecated. Use the generated repository instead.

The **factory** is now focused on **creating and instantiating** single aggregate instances, while the **repository** handles **retrieval and queries**.

#### Deprecated Factory Methods

| Deprecated Factory Method | Repository Replacement |
|---------------------------|----------------------|
| `factory.GetAsync(id)` | `repository.GetByIdAsync(id)` |
| `factory.GetAsync(id, upToVersion)` | `repository.GetByIdAsync(id, upToVersion)` |
| `factory.GetWithDocumentAsync(id)` | `repository.GetByIdWithDocumentAsync(id)` |
| `factory.GetFirstByDocumentTag(tag)` | `repository.GetFirstByDocumentTagAsync(tag)` |
| `factory.GetAllByDocumentTag(tag)` | `repository.GetAllByDocumentTagAsync(tag)` |

**Action Required**: Replace factory retrieval calls with repository equivalents:

```csharp
// Before (deprecated)
public class OrderService
{
    private readonly IOrderFactory _factory;

    public async Task<Order> GetOrder(string id)
    {
        return await _factory.GetAsync(id);  // Deprecated
    }

    public async Task<Order?> FindByEmail(string email)
    {
        return await _factory.GetFirstByDocumentTag(email);  // Deprecated
    }
}

// After (recommended)
public class OrderService
{
    private readonly IOrderFactory _factory;
    private readonly IOrderRepository _repository;

    public async Task<Order?> GetOrder(string id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<Order?> FindByEmail(string email)
    {
        return await _repository.GetFirstByDocumentTagAsync(email);
    }

    public async Task<Order> CreateOrder(string customerId)
    {
        // Factory is still used for creation
        var order = await _factory.CreateAsync(Guid.NewGuid().ToString());
        await order.Create(customerId);
        return order;
    }
}
```

#### Behavior Differences

| | Factory | Repository |
|-|---------|-----------|
| **Not found** | Throws exception | Returns `null` |
| **Snapshot support** | No (reads from version 0) | Yes (uses snapshots when available) |
| **Registration** | Singleton | Singleton |

#### Repository-Only Methods

The repository also provides query methods with no factory equivalent:

| Method | Description |
|--------|-------------|
| `GetObjectIdsAsync(token?, pageSize)` | Paginated listing of all aggregate IDs |
| `ExistsAsync(id)` | Efficient existence check without loading the aggregate |
| `CountAsync()` | Total count of aggregates |

#### What Stays on the Factory

These factory methods are **not deprecated** and remain the correct way to create aggregates:

| Method | Description |
|--------|-------------|
| `CreateAsync(id)` | Create a new aggregate |
| `Create(IEventStream)` | Instantiate from an event stream (used by `[EventStream]` binding) |
| `Create(IObjectDocument)` | Instantiate from a document |

Custom factory extensions using `protected CreateAsync<T>(id, firstEvent, metadata)` continue to work as before.

### StreamInformation Properties

Several properties have been renamed for clarity:

| Deprecated | Replacement |
|------------|-------------|
| `Connection` | `DataStore` |
| `DocumentTagConnection` | `DocumentTagStore` |
| `EventStreamTagConnection` | `StreamTagStore` |
| `SnapShotConnection` | `SnapShotStore` |
| `ObjectDocumentConnection` | `DocumentStore` |

**Action Required**: Update property references:

```csharp
// Before (v1.x)
var connection = streamInfo.Connection;

// After (v2.0)
var dataStore = streamInfo.DataStore;
```

## New Features in v2.0

### 1. Cosmos DB Support

Add Cosmos DB as a storage provider:

```csharp
// Install package
// dotnet add package ErikLieben.FA.ES.CosmosDb

// Configure
services.AddAzureClients(clients =>
{
    clients.AddCosmosClient(connectionString).WithName("Cosmos");
});

services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings
{
    DatabaseName = "eventstore",
    AutoCreateContainers = true
});
```

### 2. Event Stream Management

New migration and backup capabilities:

```csharp
// Install package
// dotnet add package ErikLieben.FA.ES.EventStreamManagement

// Migration service
var result = await migrationService
    .MigrateStream(sourceStream, destinationStream)
    .WithTransformation(events => events.Select(Transform))
    .ExecuteAsync();

// Backup service
await backupService.BackupAsync(stream, new BackupOptions
{
    IncludeSnapshots = true
});
```

### 3. Live Migration

Migrate streams while the application is running:

```csharp
var options = new LiveMigrationOptions
{
    SourceObjectName = "order",
    SourceObjectId = "order-123",
    DestinationDataStore = "Cosmos",
    OnEventMigrated = (e, i, t) => Console.WriteLine($"Migrated event {i}/{t}")
};

var result = await executor.ExecuteAsync(options);
```

### 4. Stream Tags

Categorize and query streams by tags:

```csharp
// Set a tag
await tagStore.SetAsync(document, "priority");

// Query by tag
var orderIds = await tagStore.GetAsync("order", "priority");

// Remove a tag
await tagStore.RemoveAsync(document, "priority");
```

### 5. Version Tokens

Improved optimistic concurrency with version tokens:

```csharp
var token = new VersionToken(
    objectName: "order",
    objectId: "order-123",
    streamIdentifier: "order",
    version: 42);

// Check for updates
if (projection.IsNewer(token))
{
    await projection.UpdateToVersion(token);
}
```

### 6. Testing Framework Enhancements

New test builders and assertions:

```csharp
// AggregateTestBuilder
var builder = new AggregateTestBuilder<Order>(context)
    .GivenEvents(new OrderCreated("customer-1"));

await builder
    .When(order => order.Ship("TRACK-123"))
    .ThenExpectEvent<OrderShipped>(e => e.TrackingNumber == "TRACK-123")
    .Build();

// ProjectionTestBuilder
var projBuilder = new ProjectionTestBuilder<Dashboard>(context)
    .GivenEvents(events);

await projBuilder
    .WhenProjected()
    .ThenAssert(p => p.TotalOrders == 5)
    .Build();
```

### 7. Stream Actions

New action types for stream processing:

```csharp
// Pre-append action (validation)
stream.RegisterPreAppendAction(new ValidationAction());

// Post-append action (notifications)
stream.RegisterPostAppendAction(new NotificationAction());

// Post-read action (enrichment)
stream.RegisterPostReadAction(new EnrichmentAction());
```

### 8. ASP.NET Core Minimal APIs

Simplified API integration:

```csharp
// Configure
builder.Services.AddEventStoreMinimalApis();
app.UseEventStoreMinimalApis();

// Use in endpoints
app.MapPost("/orders", async (
    [EventStream("order")] IEventStream stream,
    [Projection] Dashboard dashboard) =>
{
    // Use stream and projection
});
```

### 9. Azure Functions Worker Extensions

Improved bindings for isolated worker:

```csharp
[Function("ProcessOrder")]
public async Task Run(
    [BlobTrigger("orders/{id}")] string data,
    [EventStreamInput("order", "{id}")] IEventStream stream,
    [ProjectionOutput<Dashboard>] IAsyncCollector<Dashboard> output)
{
    // Process and update projection
}
```

## Migration Steps

### Step 1: Update Package References

```xml
<!-- Remove -->
<PackageReference Include="ErikLieben.FA.ES" Version="1.x.x" />

<!-- Add -->
<PackageReference Include="ErikLieben.FA.ES" Version="2.0.0" />
<!-- Add optional new packages as needed -->
<PackageReference Include="ErikLieben.FA.ES.CosmosDb" Version="2.0.0" />
<PackageReference Include="ErikLieben.FA.ES.EventStreamManagement" Version="2.0.0" />
<PackageReference Include="ErikLieben.FA.ES.AspNetCore.MinimalApis" Version="2.0.0" />
```

### Step 2: Update Target Framework

```xml
<PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
</PropertyGroup>
```

### Step 3: Run Code Generation

```bash
dotnet faes
```

### Step 4: Fix Deprecation Warnings

Address compiler warnings for deprecated APIs:

1. Update `Fold(IEvent, IObjectDocument)` calls to use `VersionToken`
2. Update `StreamInformation` property references
3. Update `GetWhenParameterValue` calls
4. Replace `factory.GetAsync()` / `factory.GetFirstByDocumentTag()` / `factory.GetAllByDocumentTag()` calls with the repository equivalents (inject `IOrderRepository` instead of or alongside `IOrderFactory`)

### Step 5: Test Thoroughly

```bash
dotnet test
```

## Configuration Changes

### EventStreamDefaultTypeSettings

New storage type options available:

```csharp
// v1.x
services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// v2.0 - Additional options
services.ConfigureEventStore(new EventStreamDefaultTypeSettings(
    streamType: "blob",
    documentType: "blob",
    documentTagType: "table",     // New
    eventStreamTagType: "table",  // New
    documentRefType: "blob"
));
```

### Per-Aggregate Storage

New attribute for Cosmos DB:

```csharp
// v1.x - Blob or Table only
[EventStreamType("blob", "blob")]
public partial class Order : Aggregate { }

// v2.0 - Cosmos DB option
[EventStreamType("cosmosdb", "cosmosdb")]
public partial class Order : Aggregate { }
```

## CLI Changes

### New Commands

```bash
# Watch mode for continuous code generation
dotnet faes watch

# Update command for code migrations
dotnet faes update
```

### Updated Code Generation

The generated code now includes:
- AOT-compatible JSON serialization contexts
- EventUpcasterRegistry registration
- Improved checkpoint handling for projections

## Data Migration

### Storage Migration

To migrate existing data to a new storage provider:

```csharp
var migrationService = serviceProvider.GetRequiredService<IEventStreamMigrationService>();

// Migrate a single stream
await migrationService
    .MigrateStream(sourceStream, destinationStream)
    .WithVerification()
    .ExecuteAsync();

// Bulk migration
await migrationService
    .BulkMigrate()
    .FromStorage("blob")
    .ToStorage("cosmosdb")
    .WithBatchSize(100)
    .ExecuteAsync();
```

### Projection Rebuild

To rebuild projections after migration:

```csharp
// Clear and rebuild
var projection = await factory.GetAsync();
projection.Checkpoint.Clear();
projection.CheckpointFingerprint = null;
await projection.UpdateToLatestVersion();
await factory.SaveAsync(projection);
```

## Troubleshooting

### Common Issues

#### 1. Missing VersionToken Constructor

**Error**: `No constructor found for VersionToken`

**Solution**: Ensure you're creating VersionToken correctly:

```csharp
// From event and document
var token = new VersionToken(@event, document);

// Or from parts
var token = new VersionToken("order", "order-123", "order", 42);
```

#### 2. JSON Serialization Errors

**Error**: `JsonTypeInfo not registered for type X`

**Solution**: Run `dotnet faes` to regenerate serialization contexts.

#### 3. Projection Checkpoint Mismatch

**Error**: `Checkpoint fingerprint does not match`

**Solution**: Projections may need rebuilding after significant changes. Clear the checkpoint and rebuild.

#### 4. Storage Provider Not Found

**Error**: `No storage provider registered for type 'cosmosdb'`

**Solution**: Install and configure the CosmosDb package:

```csharp
services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings());
```

## Rollback Plan

If you need to rollback to v1.x:

1. Revert package references to v1.x versions
2. Revert target framework if changed
3. Regenerate code with v1.x CLI: `dotnet faes`
4. Address any API incompatibilities

Note: Data stored in new storage providers (Cosmos DB) won't be accessible after rollback. Ensure backups before migration.

## Getting Help

- [GitHub Issues](https://github.com/eriklieben/ErikLieben.FA.ES/issues)
- [Documentation](./index.md)
- [CHANGELOG](./CHANGELOG.md)

## See Also

- [Getting Started](GettingStarted.md) - Initial setup
- [Configuration](Configuration.md) - All settings
- [Architecture](Architecture.md) - System design
- [Event Stream Management](EventStreamManagement.md) - Migration details
- [Live Migration](LiveMigration.md) - Live migration guide
