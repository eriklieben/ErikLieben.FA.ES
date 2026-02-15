# Observability Guide

This guide covers how to configure OpenTelemetry tracing and metrics for the ErikLieben.FA.ES event sourcing library.

## Quick Start

Add the OpenTelemetry packages to your project:

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Configure OpenTelemetry in your `Program.cs`:

```csharp
using ErikLieben.FA.ES.Observability;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("MyEventSourcedApp"))
    .WithTracing(t => t
        .AddSource(FaesInstrumentation.ActivitySources.CoreName)
        .AddSource(FaesInstrumentation.ActivitySources.StorageName)
        .AddSource(FaesInstrumentation.ActivitySources.ProjectionsName)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMeter(FaesInstrumentation.Meters.CoreName)
        .AddMeter(FaesInstrumentation.Meters.StorageName)
        .AddMeter(FaesInstrumentation.Meters.ProjectionsName)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
```

You can also use the string constants directly:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource("ErikLieben.FA.ES")
        .AddSource("ErikLieben.FA.ES.Storage")
        .AddSource("ErikLieben.FA.ES.Projections")
        .AddOtlpExporter());
```

## ActivitySource Names

The library uses three ActivitySource instances for distributed tracing:

| Source Name | Description |
|-------------|-------------|
| `ErikLieben.FA.ES` | Core event stream operations (read, write, session, aggregate) |
| `ErikLieben.FA.ES.Storage` | Storage provider operations (Blob, Table, CosmosDB) |
| `ErikLieben.FA.ES.Projections` | Projection operations (update, catch-up, factory) |

### Selective Configuration

For fine-grained control, configure only the sources you need:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource("ErikLieben.FA.ES")           // Core only
        .AddSource("ErikLieben.FA.ES.Projections") // Projections only (skip storage)
        .AddOtlpExporter());
```

## Available Traces

### Core Operations

| Activity Name | Description | Key Tags |
|--------------|-------------|----------|
| `EventStream.Read` | Reading events from a stream | `faes.stream.id`, `faes.start.version`, `faes.event.count` |
| `EventStream.Session` | Starting a write session | `faes.stream.id`, `faes.session.constraint` |
| `Session.Commit` | Committing events to storage | `faes.event.count`, `faes.chunking.enabled` |
| `Session.Append` | Appending an event to the buffer | `faes.event.type`, `faes.event.version` |
| `Aggregate.Fold` | Folding events into aggregate state | `faes.events.folded` |

### Projection Operations

| Activity Name | Description | Key Tags |
|--------------|-------------|----------|
| `Projection.UpdateToVersion` | Updating a projection | `faes.projection.type`, `faes.object.id`, `faes.events.folded` |
| `BlobProjectionFactory.GetOrCreate` | Loading/creating a projection from Blob | `faes.projection.type`, `faes.loaded_from_cache` |
| `BlobProjectionFactory.Save` | Saving a projection to Blob | `faes.projection.type`, `faes.projection.status` |
| `CosmosDbProjectionFactory.GetOrCreate` | Loading/creating a projection from CosmosDB | `faes.projection.type`, `faes.loaded_from_cache` |
| `CosmosDbProjectionFactory.Save` | Saving a projection to CosmosDB | `faes.projection.type`, `faes.projection.status` |
| `CatchUp.Discover` | Discovering work items for catch-up | `faes.page.size`, `faes.has_continuation`, `faes.event.count` |
| `CatchUp.Stream` | Streaming work items for catch-up | `faes.event.count` |
| `CatchUp.Estimate` | Estimating total work items | `faes.total.estimate` |

### Storage Operations

| Activity Name | Description | Key Tags |
|--------------|-------------|----------|
| `BlobDataStore.Read` | Reading from Blob storage | `db.system`, `faes.object.name`, `faes.object.id` |
| `BlobDataStore.Append` | Writing to Blob storage | `db.system`, `faes.object.name`, `faes.event.count` |
| `TableDataStore.Read` | Reading from Table storage | `db.system`, `faes.object.name`, `faes.object.id` |
| `CosmosDbDataStore.Read` | Reading from CosmosDB | `db.system`, `faes.object.name`, `faes.object.id` |
| `ResilientDataStore.Read` | Resilient read with retry | `faes.object.name`, `faes.object.id` |
| `ResilientDataStore.Append` | Resilient write with retry | `faes.object.name`, `faes.object.id` |

### Snapshot Operations

| Activity Name | Description | Key Tags |
|--------------|-------------|----------|
| `BlobSnapShotStore.Set` | Saving snapshot to Blob | `db.system`, `faes.snapshot.version`, `faes.snapshot.name` |
| `BlobSnapShotStore.Get` | Loading snapshot from Blob | `db.system`, `faes.snapshot.version`, `faes.snapshot.name` |
| `BlobSnapShotStore.List` | Listing snapshots in Blob | `db.system`, `faes.snapshot.count` |
| `BlobSnapShotStore.Delete` | Deleting snapshot from Blob | `db.system`, `faes.snapshot.version`, `faes.success` |
| `BlobSnapShotStore.DeleteMany` | Bulk delete snapshots | `db.system`, `faes.snapshot.deleted_count` |
| `CosmosDbSnapShotStore.Set` | Saving snapshot to CosmosDB | `db.system`, `faes.snapshot.version`, `faes.snapshot.name` |
| `CosmosDbSnapShotStore.Get` | Loading snapshot from CosmosDB | `db.system`, `faes.snapshot.version`, `faes.snapshot.name` |
| `CosmosDbSnapShotStore.List` | Listing snapshots in CosmosDB | `db.system`, `faes.snapshot.count` |
| `CosmosDbSnapShotStore.Delete` | Deleting snapshot from CosmosDB | `db.system`, `faes.snapshot.version`, `faes.success` |
| `CosmosDbSnapShotStore.DeleteMany` | Bulk delete snapshots | `db.system`, `faes.snapshot.deleted_count` |
| `TableSnapShotStore.Set` | Saving snapshot to Table | `db.system`, `faes.snapshot.version`, `faes.snapshot.name` |
| `TableSnapShotStore.Get` | Loading snapshot from Table | `db.system`, `faes.snapshot.version`, `faes.snapshot.name`, `faes.snapshot.found` |
| `TableSnapShotStore.List` | Listing snapshots in Table | `db.system`, `faes.snapshot.count` |
| `TableSnapShotStore.Delete` | Deleting snapshot from Table | `db.system`, `faes.snapshot.version`, `faes.success` |
| `TableSnapShotStore.DeleteMany` | Bulk delete snapshots from Table | `db.system`, `faes.snapshot.deleted_count` |

### Event Upcasting Operations

| Activity Name | Description | Key Tags |
|--------------|-------------|----------|
| `EventUpcaster.UpcastToVersion` | Upcasting events to newer schema | `faes.event.name`, `faes.upcast.from_version`, `faes.upcast.to_version`, `faes.upcast.count` |

### Post-Commit Actions

| Activity Name | Description | Key Tags |
|--------------|-------------|----------|
| `PostCommit.{ActionType}` | Executing post-commit action | `faes.action.type`, `faes.retry.attempt`, `faes.duration_ms`, `faes.success` |

## Meter Names

The library uses three Meter instances for metrics:

| Meter Name | Description |
|------------|-------------|
| `ErikLieben.FA.ES` | Core event stream metrics |
| `ErikLieben.FA.ES.Storage` | Storage provider metrics |
| `ErikLieben.FA.ES.Projections` | Projection metrics |

## Available Metrics

### Counters

| Metric Name | Unit | Description | Tags |
|-------------|------|-------------|------|
| `faes.events.appended` | events | Total events appended | `faes.object.name`, `faes.storage.provider` |
| `faes.events.read` | events | Total events read | `faes.object.name`, `faes.storage.provider` |
| `faes.commits.total` | commits | Total commit operations | `faes.object.name`, `faes.storage.provider`, `faes.success` |
| `faes.projections.updates` | updates | Projection update operations | `faes.projection.type`, `faes.storage.provider` |
| `faes.snapshots.created` | snapshots | Snapshots created | `faes.object.name` |
| `faes.upcasts.performed` | upcasts | Event upcasts performed | `faes.event.type`, `faes.upcast.from_version`, `faes.upcast.to_version` |
| `faes.catchup.items_processed` | items | Catch-up work items processed | `faes.object.name` |

### Histograms

| Metric Name | Unit | Description | Tags |
|-------------|------|-------------|------|
| `faes.commit.duration` | ms | Commit operation duration | `faes.object.name`, `faes.storage.provider` |
| `faes.projection.update.duration` | ms | Projection update duration | `faes.projection.type` |
| `faes.storage.read.duration` | ms | Storage read latency | `faes.storage.provider`, `faes.object.name` |
| `faes.storage.write.duration` | ms | Storage write latency | `faes.storage.provider`, `faes.object.name` |
| `faes.events_per_commit` | events | Events per commit | `faes.object.name` |
| `faes.projection.events_folded` | events | Events folded per projection update | `faes.projection.type` |

## Semantic Conventions

All tags follow OpenTelemetry semantic conventions with a `faes.` prefix for domain-specific attributes.

### Domain Attributes

| Attribute | Description | Example Values |
|-----------|-------------|----------------|
| `faes.stream.id` | Event stream identifier | `"order__order-123"` |
| `faes.object.name` | Object type name | `"order"`, `"workitem"` |
| `faes.object.id` | Object instance ID | `"order-123"` |
| `faes.event.count` | Number of events | `5` |
| `faes.event.type` | Event CLR type | `"OrderCreated"` |
| `faes.projection.type` | Projection CLR type | `"OrderDashboard"` |
| `faes.storage.provider` | Storage provider | `"blob"`, `"table"`, `"cosmosdb"` |

### Standard OTel Attributes

| Attribute | Description | Example Values |
|-----------|-------------|----------------|
| `db.system` | Database system | `"azure_blob"`, `"azure_table"`, `"cosmosdb"` |
| `db.operation` | Database operation | `"read"`, `"write"` |
| `db.name` | Database/container name | `"events"`, `"projections"` |

## Performance Considerations

The library follows OpenTelemetry best practices for minimal overhead:

1. **Null activity check**: `StartActivity` returns `null` when no listeners are registered, avoiding unnecessary work.

2. **Conditional tag setting**: Expensive tag operations are guarded by `IsAllDataRequested`:
   ```csharp
   if (activity?.IsAllDataRequested == true)
   {
       activity.SetTag("expensive.tag", ComputeValue());
   }
   ```

3. **Static ActivitySource instances**: All ActivitySource instances are static readonly, avoiding repeated allocation.

4. **Zero overhead when disabled**: When tracing is not enabled, the instrumentation adds no measurable overhead.

### Benchmarks

Typical overhead per operation (when tracing is enabled):
- Activity creation: ~470ns
- Tag setting: ~50ns per tag

When tracing is disabled, overhead is effectively zero due to the `StartActivity` returning `null`.

## Viewing Traces

### Jaeger

```bash
docker run -d --name jaeger \
  -p 16686:16686 \
  -p 4317:4317 \
  jaegertracing/all-in-one:latest
```

Configure OTLP exporter:
```csharp
.AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"))
```

### Azure Application Insights

```bash
dotnet add package Azure.Monitor.OpenTelemetry.Exporter
```

```csharp
.AddAzureMonitorTraceExporter(o => o.ConnectionString = "...")
.AddAzureMonitorMetricExporter(o => o.ConnectionString = "...")
```

## Sample Queries

### Grafana/Prometheus

```promql
# Average commit duration by object type
histogram_quantile(0.95,
  sum(rate(faes_commit_duration_bucket[5m])) by (le, faes_object_name)
)

# Events per second by storage provider
rate(faes_events_appended_total[1m])

# Projection update latency
histogram_quantile(0.99,
  sum(rate(faes_projection_update_duration_bucket[5m])) by (le, faes_projection_type)
)
```

### Jaeger Search

- Service: `MyEventSourcedApp`
- Operation: `Projection.UpdateToVersion`
- Tags: `faes.projection.type=OrderDashboard`

## Troubleshooting

### Traces Not Appearing

1. Verify ActivitySource is registered:
   ```csharp
   .AddSource("ErikLieben.FA.ES")
   ```

2. Check exporter configuration (endpoint, authentication)

3. Ensure `TracerProvider` is not disposed too early

### High Cardinality Tags

Avoid adding high-cardinality values (like unique IDs) to metrics tags. Use traces for per-operation details.

### Memory Leaks

Ensure `TracerProvider` and `MeterProvider` are properly disposed at application shutdown (handled automatically with `AddOpenTelemetry`).

## Health Checks

The library provides health checks for storage providers to monitor connectivity and availability.

### Installation

Health checks are included in the storage provider packages:
- `ErikLieben.FA.ES.AzureStorage` - Blob and Table storage health checks
- `ErikLieben.FA.ES.CosmosDb` - Cosmos DB health check

### Configuration

```csharp
using ErikLieben.FA.ES.AzureStorage.HealthChecks;
using ErikLieben.FA.ES.CosmosDb.HealthChecks;

builder.Services.AddHealthChecks()
    .AddBlobStorageHealthCheck()      // Azure Blob Storage
    .AddTableStorageHealthCheck()     // Azure Table Storage
    .AddCosmosDbHealthCheck();        // Azure Cosmos DB
```

### Available Health Checks

| Health Check | Package | Description |
|--------------|---------|-------------|
| `BlobStorageHealthCheck` | AzureStorage | Verifies Azure Blob Storage connectivity |
| `TableStorageHealthCheck` | AzureStorage | Verifies Azure Table Storage connectivity |
| `CosmosDbHealthCheck` | CosmosDb | Verifies Azure Cosmos DB connectivity |

### Configuration Options

Each health check accepts optional parameters:

```csharp
builder.Services.AddHealthChecks()
    .AddBlobStorageHealthCheck(
        name: "azure-blob-storage",     // Health check name
        clientName: "Store",            // Named client from Azure client factory
        failureStatus: HealthStatus.Degraded,
        tags: ["storage", "azure"],
        timeout: TimeSpan.FromSeconds(5))
    .AddTableStorageHealthCheck(
        name: "azure-table-storage",
        clientName: "Store")
    .AddCosmosDbHealthCheck(
        name: "azure-cosmosdb",
        tags: ["storage", "azure"]);
```

### Combined Health Check

For convenience, you can add all Azure Storage health checks at once:

```csharp
builder.Services.AddHealthChecks()
    .AddAzureStorageHealthChecks(
        clientName: "Store",
        tags: ["storage"]);
```

This adds both Blob and Table storage health checks if their respective services are configured.

### Health Check Tracing

Health checks are instrumented with OpenTelemetry tracing:

| Activity Name | Description | Key Tags |
|--------------|-------------|----------|
| `BlobStorageHealthCheck` | Blob storage connectivity check | `db.system`, `faes.storage.provider`, `faes.success` |
| `TableStorageHealthCheck` | Table storage connectivity check | `db.system`, `faes.storage.provider`, `faes.success` |
| `CosmosDbHealthCheck` | Cosmos DB connectivity check | `db.system`, `faes.storage.provider`, `db.name`, `faes.success` |

### Example Response

A healthy response includes additional data about the storage:

```json
{
  "status": "Healthy",
  "results": {
    "azure-blob-storage": {
      "status": "Healthy",
      "description": "Azure Blob Storage is accessible",
      "data": {
        "AccountKind": "StorageV2",
        "SkuName": "Standard_LRS"
      }
    },
    "azure-cosmosdb": {
      "status": "Healthy",
      "description": "Azure Cosmos DB is accessible",
      "data": {
        "DatabaseId": "eventstore",
        "AccountId": "myaccount",
        "ConsistencyLevel": "Session",
        "ReadableRegions": 2
      }
    }
  }
}
```

### ASP.NET Core Integration

Map health check endpoints in your application:

```csharp
app.MapHealthChecks("/health");

// Or with filtering
app.MapHealthChecks("/health/storage", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("storage")
});
```
