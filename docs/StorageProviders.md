# Storage Providers

The event sourcing framework supports multiple Azure storage backends for event streams and projections.

## Overview

| Provider | Package | Best For |
|----------|---------|----------|
| Azure Blob Storage | `ErikLieben.FA.ES.AzureStorage` | Large event payloads, cost-effective storage |
| Azure Table Storage | `ErikLieben.FA.ES.AzureStorage` | Simple queries, structured data |
| Azure Cosmos DB | `ErikLieben.FA.ES.CosmosDb` | Global distribution, low latency, complex queries |

## Azure Blob Storage

Blob storage provides cost-effective storage for event streams with support for large payloads and chunking.

### Setup

```csharp
// Program.cs or Startup.cs
using ErikLieben.FA.ES.AzureStorage;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Blob Storage event sourcing
builder.Services.AddEventSourcingWithBlobStorage(options =>
{
    options.ConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
    options.ContainerName = "eventstreams";
});

// Register your aggregates
builder.Services.AddAggregate<Order, OrderState>();
builder.Services.AddAggregate<Customer, CustomerState>();
```

### Configuration

```json
// appsettings.json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "eventstreams",
    "ProjectionsContainer": "projections",
    "SnapshotsContainer": "snapshots"
  }
}
```

```csharp
// Or using EventStreamBlobSettings
services.Configure<EventStreamBlobSettings>(options =>
{
    options.ConnectionString = connectionString;
    options.ContainerName = "eventstreams";
    options.ChunkSize = 1000; // Events per chunk
    options.EnableCompression = true;
});
```

### Features

- **Chunking** - Events are stored in chunks for efficient reading/writing
- **Compression** - Optional compression reduces storage costs
- **Append-only blobs** - Efficient append operations
- **Blob leases** - Distributed locking support

## Azure Table Storage

Table storage provides structured storage with efficient partition-based queries.

### Setup

```csharp
// Program.cs or Startup.cs
using ErikLieben.FA.ES.AzureStorage;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Table Storage event sourcing
builder.Services.AddEventSourcingWithTableStorage(options =>
{
    options.ConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
    options.TableName = "EventStreams";
});

// The factory creates event streams for your aggregates
builder.Services.AddSingleton<IEventStreamFactory, TableEventStreamFactory>();
```

### Configuration

```json
// appsettings.json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "TableName": "EventStreams",
    "ProjectionsTable": "Projections",
    "SnapshotsTable": "Snapshots"
  }
}
```

```csharp
// Or using EventStreamTableSettings
services.Configure<EventStreamTableSettings>(options =>
{
    options.ConnectionString = connectionString;
    options.TableName = "EventStreams";
    options.BatchSize = 100; // Batch operations
});
```

### Features

- **Partition-based queries** - Efficient aggregate lookups
- **Batch operations** - Multiple operations in single transaction
- **Strong consistency** - Within partition scope
- **Cost-effective** - Pay per operation

## Azure Cosmos DB

Cosmos DB provides global distribution, low latency, and rich querying capabilities.

### Setup

```csharp
// Program.cs or Startup.cs
using ErikLieben.FA.ES.CosmosDb;

var builder = WebApplication.CreateBuilder(args);

// Add Cosmos DB event sourcing
builder.Services.AddEventSourcingWithCosmosDb(options =>
{
    options.ConnectionString = builder.Configuration["CosmosDb:ConnectionString"];
    options.DatabaseName = "EventStore";
    options.ContainerName = "Events";
});

// Configure the Cosmos client for performance
builder.Services.AddSingleton(sp =>
{
    var cosmosClient = new CosmosClient(connectionString, new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        },
        ConnectionMode = ConnectionMode.Direct
    });
    return cosmosClient;
});
```

### Configuration

```json
// appsettings.json
{
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://...;AccountKey=...",
    "DatabaseName": "EventStore",
    "ContainerName": "Events",
    "PartitionKeyPath": "/objectId"
  }
}
```

```csharp
// Or using EventStreamCosmosDbSettings
services.Configure<EventStreamCosmosDbSettings>(options =>
{
    options.ConnectionString = connectionString;
    options.DatabaseName = "EventStore";
    options.ContainerName = "Events";
    options.PartitionKeyPath = "/objectId";
    options.ThroughputMode = ThroughputMode.Autoscale;
    options.MaxAutoscaleThroughput = 4000;
});
```

### Features

- **Global distribution** - Multi-region replication
- **Low latency** - Single-digit millisecond reads/writes
- **Change feed** - Real-time stream of changes
- **Rich queries** - SQL-like query language
- **Autoscale** - Automatic throughput scaling

## Best Practices

1. **Choose based on requirements** - Don't over-engineer; start simple
2. **Use connection pooling** - Reuse clients across requests
3. **Configure retry policies** - Handle transient failures gracefully
4. **Monitor costs** - Set up alerts for unexpected usage
5. **Use appropriate consistency levels** - Balance consistency vs. performance
6. **Enable compression** - Reduces storage costs and network bandwidth
