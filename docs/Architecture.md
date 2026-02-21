# Architecture Overview

This document describes the architectural design of the ErikLieben.FA.ES event sourcing framework.

## Core Concepts

```
┌─────────────────────────────────────────────────────────────────┐
│                        Application Layer                         │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐ │
│  │  Aggregates  │  │  Projections │  │  Commands / Queries    │ │
│  └──────┬───────┘  └──────┬───────┘  └────────────────────────┘ │
└─────────┼─────────────────┼─────────────────────────────────────┘
          │                 │
┌─────────┴─────────────────┴─────────────────────────────────────┐
│                      Framework Layer                             │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐ │
│  │ EventStream  │  │  Factories   │  │  Version Tokens        │ │
│  └──────┬───────┘  └──────┬───────┘  └────────────────────────┘ │
└─────────┼─────────────────┼─────────────────────────────────────┘
          │                 │
┌─────────┴─────────────────┴─────────────────────────────────────┐
│                      Storage Layer                               │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐ │
│  │  DataStore   │  │ DocumentStore│  │  SnapshotStore         │ │
│  │  (Events)    │  │  (Metadata)  │  │  (State Cache)         │ │
│  └──────┬───────┘  └──────┬───────┘  └───────────┬────────────┘ │
└─────────┼─────────────────┼─────────────────────┬┘
          │                 │                      │
┌─────────┴─────────────────┴──────────────────────┴──────────────┐
│                   Storage Providers                              │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐ │
│  │ Azure Blob   │  │ Azure Table  │  │  Cosmos DB             │ │
│  └──────────────┘  └──────────────┘  └────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Package Structure

| Package | Purpose |
|---------|---------|
| `ErikLieben.FA.ES` | Core abstractions and interfaces |
| `ErikLieben.FA.ES.AzureStorage` | Azure Blob and Table storage providers |
| `ErikLieben.FA.ES.CosmosDb` | Cosmos DB storage provider |
| `ErikLieben.FA.ES.CLI` | Code generation tool (`dotnet faes`) |
| `ErikLieben.FA.ES.Analyzers` | Roslyn analyzers for compile-time checks |
| `ErikLieben.FA.ES.Testing` | Test builders and in-memory stores |
| `ErikLieben.FA.ES.AspNetCore.MinimalApis` | ASP.NET Core Minimal APIs integration |
| `ErikLieben.FA.ES.Azure.Functions.Worker.Extensions` | Azure Functions bindings |
| `ErikLieben.FA.ES.EventStreamManagement` | Migration and backup services |

## Core Interfaces

### IDataStore

The fundamental storage interface for event persistence:

```csharp
public interface IDataStore
{
    Task AppendAsync(IObjectDocument document, params IEvent[] events);
    Task AppendAsync(IObjectDocument document, bool preserveTimestamp, params IEvent[] events);
    Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document,
        int startVersion = 0, int? untilVersion = null, int? chunk = null);
}
```

### IObjectDocument

Represents metadata about an aggregate's event stream:

```csharp
public interface IObjectDocument
{
    StreamInformation Active { get; }
    string ObjectId { get; }
    string ObjectName { get; }
    List<TerminatedStream> TerminatedStreams { get; }
    string? SchemaVersion { get; }
    string? Hash { get; }
}
```

### IEventStream

Provides the event stream operations for an aggregate:

- `ReadAsync()` - Read events from the stream
- `Session()` - Create a session for appending events
- `Snapshot<T>()` - Create a point-in-time snapshot
- `RegisterEvent<T>()` - Register event types for serialization
- `RegisterUpcast()` - Register schema evolution handlers
- `RegisterAction()` / `RegisterNotification()` - Add extensibility hooks

### IAggregateFactory<T>

Factory for loading and creating aggregates:

```csharp
public interface IAggregateFactory<T> where T : IBase
{
    string GetObjectName();
    Task<T> CreateAsync(string id);
    Task<T> GetAsync(string id, int? upToVersion = null);
    Task<T?> GetFirstByDocumentTag(string tag);
    Task<IEnumerable<T>> GetAllByDocumentTag(string tag);
}
```

### IProjectionFactory<T>

Factory for loading and updating projections:

```csharp
public interface IProjectionFactory<T> where T : Projection
{
    Task<T> GetAsync();
    Task SaveAsync(T projection);
    T Create();
}
```

## Event Flow

### Writing Events

```
1. Application calls aggregate command
2. Aggregate creates event via Fold(context.Append(...))
3. LeasedSession buffers events
4. PreAppendActions execute (validation, enrichment)
5. DataStore.AppendAsync persists events
6. ObjectDocumentFactory updates document metadata
7. PostAppendActions execute (notifications, triggers)
8. Session commits
```

### Reading Events

```
1. AggregateFactory.GetAsync(id) called
2. ObjectDocumentFactory loads document metadata
3. EventStreamFactory creates stream from document
4. DataStore.ReadAsync retrieves events
5. EventUpcasterRegistry applies schema migrations
6. Aggregate.Fold processes each event
7. PostReadActions execute
8. Aggregate returned to caller
```

### Projection Updates

```
1. Event trigger fires (Azure Function, SignalR, polling)
2. ProjectionFactory.GetAsync loads projection
3. Projection.Fold processes event
4. Checkpoint updated with version token
5. ProjectionFactory.SaveAsync persists state
```

## Aggregate Lifecycle

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Create    │────▶│    Load     │────▶│   Replay    │
│  (Factory)  │     │ (Document)  │     │  (Events)   │
└─────────────┘     └─────────────┘     └──────┬──────┘
                                               │
                                               ▼
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Return    │◀────│   Commit    │◀────│   Command   │
│ (Aggregate) │     │  (Session)  │     │   (Fold)    │
└─────────────┘     └─────────────┘     └─────────────┘
```

## Code Generation

The `dotnet faes` CLI generates boilerplate code for:

1. **Aggregate Registration** - Factory registration and DI setup
2. **Event Registration** - JSON serialization contexts and type mappings
3. **Projection Registration** - Factory implementations and checkpoint handling
4. **Extension Methods** - Domain-specific service collection extensions

### Generated Code Structure

```
{ProjectName}/
├── Aggregates/
│   └── {Aggregate}.Generated.cs     // Factory and stream setup
├── Projections/
│   └── {Projection}.Generated.cs    // Fold method and checkpoints
├── Events/
│   └── {Event}JsonContext.cs        // AOT-compatible serialization
└── Extensions/
    └── ServiceCollectionExtensions.cs  // DI registration
```

## Storage Providers

### Azure Blob Storage

- Events stored as JSON blobs per stream
- Documents stored in `object-document-store` container
- Supports chunking for large streams
- Good for: Small to medium workloads, cost-effective

### Azure Table Storage

- Events stored as table entities
- Partitioned by object name and ID
- Fast point reads and range queries
- Good for: High-throughput reads, structured queries

### Cosmos DB

- Events stored in containers with partition keys
- Native support for global distribution
- Automatic indexing and scaling
- Good for: Large scale, global applications

### Provider Selection Matrix

| Requirement | Blob | Table | Cosmos DB |
|------------|------|-------|-----------|
| Cost | Low | Low | Higher |
| Read Performance | Moderate | Fast | Fast |
| Write Performance | Moderate | Fast | Fast |
| Query Flexibility | Limited | Moderate | High |
| Global Distribution | Manual | Manual | Built-in |
| Auto-scaling | Limited | Limited | Built-in |

## Extensibility Points

### Actions

Execute custom logic at specific points:

- `IPreAppendAction` - Before events are persisted
- `IPostAppendAction` - After events are persisted
- `IPostReadAction` - After events are read
- `IAsyncPostCommitAction` - After session commits

### Notifications

React to stream events:

- `INotification` - Base notification interface
- `IStreamDocumentChunkClosedNotification` - When a chunk is closed

### Upcasting

Handle schema evolution:

- `IUpcastEvent` - Interface-based upcaster
- `EventUpcasterRegistry` - Registry-based upcaster (AOT-friendly)

## Concurrency Model

### Optimistic Concurrency

The framework uses version tokens for optimistic concurrency:

```csharp
// Version token format
{ObjectName}__{ObjectId}__{StreamIdentifier}__{VersionString}

// Example
order__order-123__order__00000000000000000042
```

### Session Constraints

Control aggregate existence requirements:

```csharp
await Stream.Session(context => ..., Constraint.Loose);    // Default, no check
await Stream.Session(context => ..., Constraint.Existing); // Must exist
await Stream.Session(context => ..., Constraint.New);      // Must not exist
```

## Testing Architecture

The testing package provides:

- **InMemoryDataStore** - Event storage for unit tests
- **InMemoryDocumentStore** - Document storage for unit tests
- **AggregateTestBuilder** - Fluent aggregate testing
- **ProjectionTestBuilder** - Fluent projection testing
- **TestClock** - Deterministic time control

### Test Flow

```csharp
// Arrange
var context = TestSetup.GetContext();
var builder = new AggregateTestBuilder<Order>(context)
    .GivenEvents(new OrderCreated("customer-1"));

// Act & Assert
await builder
    .When(order => order.Ship("TRACK-123"))
    .ThenExpectEvent<OrderShipped>(e => e.TrackingNumber == "TRACK-123")
    .Build();
```

## AOT Compatibility

The framework is designed for Native AOT:

1. **Source Generators** - No runtime reflection
2. **JsonTypeInfo<T>** - Pre-compiled JSON serialization
3. **EventTypeRegistry** - Compile-time event type mapping
4. **EventUpcasterRegistry** - Type-safe upcasting without reflection

## Thread Safety

- **Aggregates** - Not thread-safe, use one per request
- **Projections** - Thread-safe for reads, single-writer for updates
- **Factories** - Thread-safe, singleton-scoped
- **DataStores** - Thread-safe, connection-pooled

## Error Handling

### Exception Hierarchy

```
Exception
├── ConstraintException          // Session constraint violations
├── CommitFailedException        // Event append failures
├── CommitCleanupFailedException // Post-commit cleanup failures
├── SnapshotJsonTypeInfoNotSetException // Missing serialization config
└── Various storage-specific exceptions
```

## See Also

- [Getting Started](GettingStarted.md) - Initial setup
- [Aggregates](Aggregates.md) - Aggregate patterns
- [Projections](Projections.md) - Projection patterns
- [Configuration](Configuration.md) - All settings
- [Storage Providers](StorageProviders.md) - Provider details
- [Testing](Testing.md) - Testing patterns
