# Configuration Reference

This document covers all configuration options for the ErikLieben.FA.ES event sourcing framework.

## Service Registration

### Fluent Builder Setup (Recommended)

The fluent builder API provides a clean, discoverable way to configure event sourcing:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 1. Register Azure clients
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(connectionString).WithName("Store");
});

// 2. Configure event sourcing with fluent API
builder.Services.AddFaes(faes => faes
    .UseDefaultStorage("blob")
    .UseBlobStorage(new EventStreamBlobSettings("Store", autoCreateContainer: true))
);

// 3. Register your domain (generated)
builder.Services.ConfigureMyDomainFactory();
```

#### Multiple Storage Providers

```csharp
builder.Services.AddFaes(faes => faes
    .UseDefaultStorage("blob")
    .UseBlobStorage(new EventStreamBlobSettings("Store"))
    .UseTableStorage(new EventStreamTableSettings("Tables"))
    .UseCosmosDb(new EventStreamCosmosDbSettings { DatabaseName = "eventstore" })
);
```

#### With Health Checks

```csharp
builder.Services.AddFaes(faes => faes
    .UseDefaultStorage("blob")
    .UseBlobStorage(new EventStreamBlobSettings("Store"))
    .AddBlobHealthCheck("Store")
    .UseCosmosDb(cosmosSettings)
    .AddCosmosDbHealthCheck()
);
```

### Classic Setup

The traditional configuration approach is still supported:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 1. Register Azure clients
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(connectionString).WithName("Store");
});

// 2. Configure storage provider (choose one)
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store"));
// OR
builder.Services.ConfigureTableEventStore(new EventStreamTableSettings("Store"));
// OR
builder.Services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings());

// 3. Configure event store defaults
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// 4. Register your domain (generated)
builder.Services.ConfigureMyDomainFactory();
```

## Event Stream Default Settings

### EventStreamDefaultTypeSettings

Configure default storage types for all components:

```csharp
// Simple: same type for everything
services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// Advanced: different types per component
services.ConfigureEventStore(new EventStreamDefaultTypeSettings(
    streamType: "blob",           // Event streams
    documentType: "blob",         // Object documents
    documentTagType: "table",     // Document tags
    eventStreamTagType: "table",  // Stream tags
    documentRefType: "blob"       // Document references
));
```

| Parameter | Description |
|-----------|-------------|
| `StreamType` | Storage type for event streams |
| `DocumentType` | Storage type for object documents |
| `DocumentTagType` | Storage type for document tags |
| `EventStreamTagType` | Storage type for stream tags |
| `DocumentRefType` | Storage type for document references |

## Azure Blob Storage Settings

### EventStreamBlobSettings

```csharp
services.ConfigureBlobEventStore(new EventStreamBlobSettings(
    defaultDataStore: "Store",              // Required: Named BlobServiceClient
    defaultDocumentStore: null,             // Falls back to defaultDataStore
    defaultSnapShotStore: null,             // Falls back to defaultDataStore
    defaultDocumentTagStore: null,          // Falls back to defaultDataStore
    autoCreateContainer: true,              // Auto-create containers
    enableStreamChunks: false,              // Enable event chunking
    defaultChunkSize: 1000,                 // Events per chunk
    defaultDocumentContainerName: "object-document-store"  // Container name
));
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultDataStore` | string | (required) | Named BlobServiceClient key |
| `DefaultDocumentStore` | string | null | Document store key |
| `DefaultSnapShotStore` | string | null | Snapshot store key |
| `DefaultDocumentTagStore` | string | null | Tag store key |
| `AutoCreateContainer` | bool | true | Auto-create missing containers |
| `EnableStreamChunks` | bool | false | Enable event stream chunking |
| `DefaultChunkSize` | int | 1000 | Events per chunk |
| `DefaultDocumentContainerName` | string | "object-document-store" | Container for documents |

## Azure Table Storage Settings

### EventStreamTableSettings

```csharp
services.ConfigureTableEventStore(new EventStreamTableSettings(
    defaultDataStore: "Store",
    autoCreateTable: true,
    enableStreamChunks: false,
    defaultChunkSize: 1000,
    defaultDocumentTableName: "objectdocumentstore",
    defaultEventTableName: "eventstream",
    defaultSnapshotTableName: "snapshots",
    defaultDocumentTagTableName: "documenttags",
    defaultStreamTagTableName: "streamtags",
    defaultStreamChunkTableName: "streamchunks",
    defaultDocumentSnapShotTableName: "documentsnapshots",
    defaultTerminatedStreamTableName: "terminatedstreams"
));
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultDataStore` | string | (required) | Named TableServiceClient key |
| `AutoCreateTable` | bool | true | Auto-create missing tables |
| `EnableStreamChunks` | bool | false | Enable event chunking |
| `DefaultChunkSize` | int | 1000 | Events per chunk |
| `DefaultDocumentTableName` | string | "objectdocumentstore" | Documents table |
| `DefaultEventTableName` | string | "eventstream" | Events table |
| `DefaultSnapshotTableName` | string | "snapshots" | Snapshots table |
| `DefaultDocumentTagTableName` | string | "documenttags" | Document tags table |
| `DefaultStreamTagTableName` | string | "streamtags" | Stream tags table |
| `DefaultStreamChunkTableName` | string | "streamchunks" | Chunk metadata table |
| `DefaultDocumentSnapShotTableName` | string | "documentsnapshots" | Document snapshots table |
| `DefaultTerminatedStreamTableName` | string | "terminatedstreams" | Terminated streams table |

## Cosmos DB Settings

### EventStreamCosmosDbSettings

```csharp
services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings
{
    DatabaseName = "eventstore",
    DocumentsContainerName = "documents",
    EventsContainerName = "events",
    SnapshotsContainerName = "snapshots",
    TagsContainerName = "tags",
    ProjectionsContainerName = "projections",
    AutoCreateContainers = true,
    EnableBulkExecution = false,
    MaxBatchSize = 100,
    UseOptimisticConcurrency = true,
    DefaultTimeToLiveSeconds = -1,
    EventsThroughput = new ThroughputSettings { AutoscaleMaxThroughput = 4000 },
    DatabaseThroughput = new ThroughputSettings { AutoscaleMaxThroughput = 4000 }
});
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DatabaseName` | string | "eventstore" | Database name |
| `DocumentsContainerName` | string | "documents" | Documents container |
| `EventsContainerName` | string | "events" | Events container |
| `SnapshotsContainerName` | string | "snapshots" | Snapshots container |
| `TagsContainerName` | string | "tags" | Tags container |
| `ProjectionsContainerName` | string | "projections" | Projections container |
| `AutoCreateContainers` | bool | true | Auto-create database/containers |
| `EnableBulkExecution` | bool | false | Enable bulk execution mode |
| `MaxBatchSize` | int | 100 | Max items per transaction batch |
| `UseOptimisticConcurrency` | bool | true | Use ETag-based concurrency |
| `DefaultTimeToLiveSeconds` | int | -1 | TTL for events (-1 = infinite) |

### ThroughputSettings

```csharp
// Autoscale (recommended)
new ThroughputSettings { AutoscaleMaxThroughput = 4000 }

// Manual throughput
new ThroughputSettings { ManualThroughput = 400 }

// Use shared database throughput
null  // Set container throughput to null
```

| Property | Type | Description |
|----------|------|-------------|
| `ManualThroughput` | int? | Fixed RU/s (mutually exclusive) |
| `AutoscaleMaxThroughput` | int? | Max autoscale RU/s (min = 10%) |

## Aggregate Configuration

### Per-Aggregate Storage Settings

Override storage type per aggregate using attributes:

```csharp
[Aggregate]
[EventStreamType("cosmosdb", "cosmosdb")]  // Stream and document types
public partial class Sprint : Aggregate { }

[Aggregate]
[EventStreamType("table", "table")]
public partial class Epic : Aggregate { }

[Aggregate]
[EventStreamBlobSettings("CustomConnection")]  // Custom blob connection
public partial class Order : Aggregate { }
```

### EventStreamTypeAttribute

```csharp
[EventStreamType("streamType", "documentType")]
```

| Parameter | Description |
|-----------|-------------|
| `streamType` | Storage type for event stream ("blob", "table", "cosmosdb") |
| `documentType` | Storage type for object document |

### EventStreamBlobSettingsAttribute

```csharp
[EventStreamBlobSettings("ConnectionName")]
```

| Parameter | Description |
|-----------|-------------|
| `connectionName` | Named connection for this aggregate's blob storage |

## Projection Configuration

### Projection Storage Attributes

```csharp
// Blob storage
[BlobJsonProjection("projections", Connection = "BlobStorage")]
public partial class Dashboard : Projection { }

// Table storage (via generated code)
[TableProjection("projections")]
public partial class Dashboard : Projection { }

// Cosmos DB
[CosmosDbJsonProjection("projections", Connection = "cosmosdb")]
public partial class Dashboard : Projection { }
```

### BlobJsonProjection Parameters

| Parameter | Description |
|-----------|-------------|
| `path` | Container path/name |
| `Connection` | Named connection string |

### Checkpoint Configuration

```csharp
// Store checkpoint externally (separate file)
[BlobJsonProjection("projections")]
[ProjectionWithExternalCheckpoint]
public partial class Dashboard : Projection { }
```

## Connection Strings

### Azure Storage

```json
{
  "ConnectionStrings": {
    "Storage": "DefaultEndpointsProtocol=https;AccountName=xxx;AccountKey=xxx;EndpointSuffix=core.windows.net",
    "BlobStorage": "...",
    "TableStorage": "..."
  }
}
```

### Cosmos DB

```json
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://xxx.documents.azure.com:443/;AccountKey=xxx;"
  }
}
```

### Local Development (Azurite)

```json
{
  "ConnectionStrings": {
    "Storage": "UseDevelopmentStorage=true"
  }
}
```

## Azure Functions Configuration

```csharp
var builder = FunctionsApplication.CreateBuilder(args);

// Configure storage
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store"));
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// Configure function bindings
builder.ConfigureEventStoreBindings();

// Register projection factories for [ProjectionInput]
builder.Services.AddSingleton<DashboardFactory>();
builder.Services.AddSingleton<IProjectionFactory<Dashboard>>(
    sp => sp.GetRequiredService<DashboardFactory>());
builder.Services.AddSingleton<IProjectionFactory>(
    sp => sp.GetRequiredService<DashboardFactory>());
```

## Minimal APIs Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure storage
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store"));
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// Configure minimal API bindings
builder.Services.AddEventStoreMinimalApis();

var app = builder.Build();
app.UseEventStoreMinimalApis();
```

## Multiple Storage Providers

Use different storage for different aggregates:

```csharp
// Configure all providers
builder.Services.AddAzureClients(clients =>
{
    clients.AddBlobServiceClient(blobConnection).WithName("Blob");
    clients.AddTableServiceClient(tableConnection).WithName("Table");
    clients.AddCosmosClient(cosmosConnection).WithName("Cosmos");
});

// Register all storage types
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Blob"));
builder.Services.ConfigureTableEventStore(new EventStreamTableSettings("Table"));
builder.Services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings());

// Set default
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// Then per-aggregate attributes override defaults:
// [EventStreamType("cosmosdb", "cosmosdb")] on specific aggregates
```

## Environment-Specific Configuration

### Development

```csharp
if (builder.Environment.IsDevelopment())
{
    // Use Azurite local emulator
    builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings(
        "LocalStore",
        autoCreateContainer: true));
}
```

### Production

```csharp
if (builder.Environment.IsProduction())
{
    // Use Azure with specific settings
    builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings(
        "ProdStore",
        autoCreateContainer: false,  // Containers pre-created
        enableStreamChunks: true,
        defaultChunkSize: 500));
}
```

## Performance Tuning

### High-Volume Writes

```csharp
// Enable bulk execution for Cosmos DB
services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings
{
    EnableBulkExecution = true,
    MaxBatchSize = 100,
    EventsThroughput = new ThroughputSettings { AutoscaleMaxThroughput = 10000 }
});

// Enable chunking for blob storage
services.ConfigureBlobEventStore(new EventStreamBlobSettings(
    "Store",
    enableStreamChunks: true,
    defaultChunkSize: 500));
```

### Read Optimization

```csharp
// Use Table Storage for fast point reads
services.ConfigureTableEventStore(new EventStreamTableSettings(
    "Store",
    autoCreateTable: true));

// Or Cosmos DB with proper partition keys
services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings
{
    UseOptimisticConcurrency = true
});
```

## See Also

- [Getting Started](GettingStarted.md) - Initial setup
- [Storage Providers](StorageProviders.md) - Provider details
- [Aggregates](Aggregates.md) - Aggregate configuration
- [Projections](Projections.md) - Projection configuration
- [Azure Functions](AzureFunctions.md) - Functions setup
- [Minimal APIs](MinimalApis.md) - Minimal API setup
