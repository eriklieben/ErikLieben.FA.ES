# Storage Providers

The event sourcing framework supports multiple Azure storage backends for event streams, documents, projections, and snapshots.

## Overview

| Provider | Package | Best For |
|----------|---------|----------|
| Azure Blob Storage | `ErikLieben.FA.ES.AzureStorage` | Large event payloads, cost-effective storage |
| Azure Table Storage | `ErikLieben.FA.ES.AzureStorage` | Fast point reads, structured queries |
| Azure Cosmos DB | `ErikLieben.FA.ES.CosmosDb` | Global distribution, low latency, complex queries |
| In-Memory | `ErikLieben.FA.ES.Testing` | Unit testing, development |

## Azure Blob Storage

Blob storage provides cost-effective storage for event streams with support for large payloads and chunking.

### Installation

```bash
dotnet add package ErikLieben.FA.ES.AzureStorage
```

### Configuration

```csharp
using Microsoft.Extensions.Azure;
using ErikLieben.FA.ES.AzureStorage;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Azure clients
builder.Services.AddAzureClients(clients =>
{
    clients.AddBlobServiceClient(
        builder.Configuration.GetConnectionString("Storage"))
        .WithName("Store");
});

// 2. Configure blob event store
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings(
    defaultDataStore: "Store",
    defaultDocumentStore: "Store",
    defaultSnapShotStore: "Store",
    defaultDocumentTagStore: "Store",
    autoCreateContainer: true,
    enableStreamChunks: false,
    defaultChunkSize: 1000,
    defaultDocumentContainerName: "object-document-store"
));

// 3. Set as default storage type
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// 4. Register domain (generated)
builder.Services.ConfigureMyDomainFactory();
```

### Settings Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultDataStore` | string | (required) | Named BlobServiceClient |
| `DefaultDocumentStore` | string | null | Document store (falls back to DefaultDataStore) |
| `DefaultSnapShotStore` | string | null | Snapshot store |
| `DefaultDocumentTagStore` | string | null | Tag store |
| `AutoCreateContainer` | bool | true | Auto-create missing containers |
| `EnableStreamChunks` | bool | false | Enable event chunking |
| `DefaultChunkSize` | int | 1000 | Events per chunk |
| `DefaultDocumentContainerName` | string | "object-document-store" | Container name |

### Blob Storage Layout

```
{container}/
├── {objectName}/
│   ├── {objectId}/
│   │   ├── document.json      # Object document (metadata)
│   │   ├── events.json        # Event stream
│   │   └── snapshot-v42.json  # Snapshot at version 42
│   └── ...
└── projections/
    ├── dashboard.json
    └── kanban/
        └── project-{id}.json
```

### Features

- **Append-only blobs** - Efficient append operations for events
- **Chunking** - Split large streams across multiple blobs
- **Compression** - Reduce storage costs (optional)
- **Blob leases** - Distributed locking for migrations

## Azure Table Storage

Table storage provides structured storage with efficient partition-based queries.

### Configuration

```csharp
using Microsoft.Extensions.Azure;
using ErikLieben.FA.ES.AzureStorage;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Azure clients
builder.Services.AddAzureClients(clients =>
{
    clients.AddTableServiceClient(
        builder.Configuration.GetConnectionString("Storage"))
        .WithName("Store");
});

// 2. Configure table event store
builder.Services.ConfigureTableEventStore(new EventStreamTableSettings(
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

// 3. Set as default storage type
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("table"));

// 4. Register domain
builder.Services.ConfigureMyDomainFactory();
```

### Settings Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultDataStore` | string | (required) | Named TableServiceClient |
| `AutoCreateTable` | bool | true | Auto-create missing tables |
| `EnableStreamChunks` | bool | false | Enable event chunking |
| `DefaultChunkSize` | int | 1000 | Events per chunk |
| `DefaultDocumentTableName` | string | "objectdocumentstore" | Documents table |
| `DefaultEventTableName` | string | "eventstream" | Events table |
| `DefaultSnapshotTableName` | string | "snapshots" | Snapshots table |
| `DefaultDocumentTagTableName` | string | "documenttags" | Document tags table |
| `DefaultStreamTagTableName` | string | "streamtags" | Stream tags table |

### Table Schema

**Events Table** (PartitionKey: `{objectName}__{objectId}`, RowKey: `{version}`)

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Object identifier |
| RowKey | string | Event version (zero-padded) |
| EventName | string | Event type name |
| Payload | string | JSON event data |
| Timestamp | DateTime | Event timestamp |

### Features

- **Partition queries** - Fast aggregate lookups
- **Batch operations** - Transaction batches (up to 100 operations)
- **Point reads** - O(1) event retrieval by version
- **Strong consistency** - Within partition scope

## Azure Cosmos DB

Cosmos DB provides global distribution, low latency, and rich querying capabilities.

### Installation

```bash
dotnet add package ErikLieben.FA.ES.CosmosDb
```

### Configuration

```csharp
using Microsoft.Extensions.Azure;
using ErikLieben.FA.ES.CosmosDb;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Cosmos client
builder.Services.AddAzureClients(clients =>
{
    clients.AddCosmosClient(
        builder.Configuration.GetConnectionString("CosmosDb"))
        .WithName("Cosmos");
});

// 2. Configure Cosmos DB event store
builder.Services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings
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

// 3. Set as default storage type
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("cosmosdb"));

// 4. Register domain
builder.Services.ConfigureMyDomainFactory();
```

### Settings Reference

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
| `MaxBatchSize` | int | 100 | Max items per transaction |
| `UseOptimisticConcurrency` | bool | true | Use ETag-based concurrency |
| `DefaultTimeToLiveSeconds` | int | -1 | TTL for items (-1 = infinite) |

### Throughput Settings

```csharp
// Autoscale (recommended)
new ThroughputSettings { AutoscaleMaxThroughput = 4000 }

// Manual (fixed)
new ThroughputSettings { ManualThroughput = 400 }

// Shared database throughput
DatabaseThroughput = new ThroughputSettings { AutoscaleMaxThroughput = 4000 },
EventsThroughput = null  // Uses shared database throughput
```

### Container Schema

**Events Container** (Partition Key: `/objectId`)

```json
{
    "id": "order__order-123__00000000000000000001",
    "objectName": "order",
    "objectId": "order-123",
    "version": 1,
    "eventName": "OrderCreated",
    "payload": { "customerId": "customer-1" },
    "timestamp": "2024-01-15T10:30:00Z",
    "_etag": "..."
}
```

### Features

- **Global distribution** - Multi-region active-active
- **Low latency** - Single-digit millisecond reads/writes
- **Change feed** - Real-time stream of changes
- **SQL queries** - Rich query language
- **Autoscale** - Automatic throughput scaling
- **TTL** - Automatic expiration of old events

## Mixed Storage Providers

Use different providers for different aggregates:

```csharp
// Configure all providers
builder.Services.AddAzureClients(clients =>
{
    clients.AddBlobServiceClient(blobConnection).WithName("Blob");
    clients.AddTableServiceClient(tableConnection).WithName("Table");
    clients.AddCosmosClient(cosmosConnection).WithName("Cosmos");
});

builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Blob"));
builder.Services.ConfigureTableEventStore(new EventStreamTableSettings("Table"));
builder.Services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings());

// Set default
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));
```

Override per aggregate:

```csharp
[Aggregate]
[EventStreamType("cosmosdb", "cosmosdb")]  // High-volume aggregate
public partial class Order : Aggregate { }

[Aggregate]
[EventStreamType("table", "table")]  // Fast reads needed
public partial class Product : Aggregate { }

[Aggregate]  // Uses default (blob)
public partial class Customer : Aggregate { }
```

## In-Memory Storage (Testing)

For unit tests, use the in-memory implementations:

```csharp
using ErikLieben.FA.ES.Testing;

// Get test context
var context = TestSetup.GetContext();

// Access stores
var dataStore = context.GetService<IDataStore>();  // InMemoryDataStore
var documentStore = context.GetService<IDocumentStore>();  // InMemoryDocumentStore
var tagStore = context.GetService<IDocumentTagStore>();  // InMemoryDocumentTagStore

// Use in tests
var orderFactory = context.GetService<IAggregateFactory<Order>>();
var order = await orderFactory.CreateAsync("order-1");
```

## Local Development (Azurite)

Use the Azurite emulator for local development:

```json
{
  "ConnectionStrings": {
    "Storage": "UseDevelopmentStorage=true"
  }
}
```

Start Azurite:

```bash
# Docker
docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite

# Or npm
npx azurite --silent --location ./azurite
```

## Performance Comparison

| Operation | Blob | Table | Cosmos DB |
|-----------|------|-------|-----------|
| Append Event | ~50ms | ~10ms | ~5ms |
| Read Stream (100 events) | ~100ms | ~20ms | ~15ms |
| Read Single Event | ~50ms | ~5ms | ~3ms |
| Query by Tag | ~200ms | ~30ms | ~10ms |
| Cost (per 1M ops) | $0.50 | $0.36 | $0.25-$2.00 |

*Note: Actual performance varies based on region, payload size, and configuration.*

## Best Practices

### General

1. **Start simple** - Use Blob storage unless you have specific requirements
2. **Use connection pooling** - Reuse clients across requests (singleton)
3. **Configure retry policies** - Handle transient failures gracefully
4. **Monitor costs** - Set up alerts for unexpected usage
5. **Use appropriate regions** - Deploy storage close to your application

### Blob Storage

- Enable chunking for streams with >1000 events
- Use compression for large event payloads
- Consider separate containers for projections

### Table Storage

- Keep events small (< 64KB per entity)
- Use batch operations for bulk inserts
- Partition by aggregate for best performance

### Cosmos DB

- Choose partition key wisely (objectId recommended)
- Use autoscale for variable workloads
- Enable bulk execution for migrations
- Consider shared throughput for development

## See Also

- [Configuration](Configuration.md) - All settings reference
- [Architecture](Architecture.md) - System design
- [Aggregates](Aggregates.md) - Aggregate patterns
- [Projections](Projections.md) - Projection patterns
- [Testing](Testing.md) - Testing with in-memory stores
