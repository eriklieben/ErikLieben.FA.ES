# Migrating to v2.0

This guide covers breaking changes when upgrading from v1.x to v2.0.

## Quick Start

For most users, the migration is simple:

```bash
# 1. Update NuGet packages to v2.0
dotnet add package ErikLieben.FA.ES --version 2.0.0

# 2. Regenerate code (handles most breaking changes automatically)
dotnet faes
```

If you have custom implementations of library interfaces, continue reading for interface changes.

---

## Breaking Changes

### 1. CancellationToken Parameters Added to Async Interfaces

All async interface methods now include optional `CancellationToken` parameters. This is **source-compatible for callers** (default parameters), but **breaking for implementers**.

#### IDataStore

```csharp
// v1.x
Task AppendAsync(IObjectDocument document, params IEvent[] events);
Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null, int? chunk = null);

// v2.0 - CancellationToken BEFORE params (C# requirement)
Task AppendAsync(IObjectDocument document, CancellationToken cancellationToken, params IEvent[] events);
Task AppendAsync(IObjectDocument document, bool preserveTimestamp, CancellationToken cancellationToken, params IEvent[] events);
Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null, int? chunk = null, CancellationToken cancellationToken = default);
```

#### ILeasedSession

```csharp
// v1.x
Task CommitAsync();
Task<bool> IsTerminatedASync(string streamIdentifier);  // Note: typo in v1.x
Task<IEnumerable<IEvent>?> ReadAsync(int startVersion = 0, int? untilVersion = null);

// v2.0
Task CommitAsync(CancellationToken cancellationToken = default);
Task<bool> IsTerminatedAsync(string streamIdentifier, CancellationToken cancellationToken = default);  // Fixed casing
Task<IEnumerable<IEvent>?> ReadAsync(int startVersion = 0, int? untilVersion = null, CancellationToken cancellationToken = default);
```

> **Note:** `IsTerminatedASync` was renamed to `IsTerminatedAsync` (proper casing).

#### IObjectDocumentFactory

```csharp
// v2.0 - All methods now have CancellationToken as last parameter
Task<IObjectDocument> GetAsync(..., CancellationToken cancellationToken = default);
Task<IObjectDocument> GetOrCreateAsync(..., CancellationToken cancellationToken = default);
Task<IObjectDocument?> GetFirstByObjectDocumentTag(..., CancellationToken cancellationToken = default);
Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(..., CancellationToken cancellationToken = default);
Task SetAsync(..., CancellationToken cancellationToken = default);
```

#### ISnapShotStore

```csharp
// v2.0
Task<T?> GetAsync<T>(..., CancellationToken cancellationToken = default);
Task<object?> GetAsync(..., CancellationToken cancellationToken = default);
Task SetAsync(..., CancellationToken cancellationToken = default);
Task<IReadOnlyList<SnapshotMetadata>> ListSnapshotsAsync(..., CancellationToken cancellationToken = default);
Task<bool> DeleteAsync(..., CancellationToken cancellationToken = default);
Task<int> DeleteManyAsync(..., CancellationToken cancellationToken = default);
```

#### IEventStream

```csharp
// v2.0
Task<IReadOnlyCollection<IEvent>> ReadAsync(..., CancellationToken cancellationToken = default);
Task Session(Action<ILeasedSession> context, Constraint constraint = Constraint.Loose, CancellationToken cancellationToken = default);
Task Snapshot<T>(..., CancellationToken cancellationToken = default);
Task<object?> GetSnapShot(..., CancellationToken cancellationToken = default);
```

### 2. IEvent Interface - New SchemaVersion Property

Events must now include a `SchemaVersion` property for schema evolution support:

```csharp
public interface IEvent
{
    // Existing properties...

    // NEW in v2.0
    int SchemaVersion { get; }
}
```

**Migration:** If you have custom `IEvent` implementations, add:
```csharp
public int SchemaVersion => 1;
```

### 3. IDataStore - New Methods

The `IDataStore` interface has new required methods:

```csharp
// New overload for migrations with timestamp preservation
Task AppendAsync(IObjectDocument document, bool preserveTimestamp, CancellationToken cancellationToken, params IEvent[] events);

// New streaming read for large event streams
IAsyncEnumerable<IEvent> ReadAsStreamAsync(
    IObjectDocument document,
    int startVersion = 0,
    int? untilVersion = null,
    int? chunk = null,
    CancellationToken cancellationToken = default);
```

### 4. IAggregateFactory - New upToVersion Parameter

The `GetAsync` method now supports loading aggregates at a specific version:

```csharp
// v1.x
Task<T> GetAsync(string id);

// v2.0
Task<T> GetAsync(string id, int? upToVersion = null);
```

This is source-compatible for callers but breaking for custom implementations.

### 5. IDocumentTagStore - New RemoveAsync Method

Tag stores must now support tag removal:

```csharp
// NEW in v2.0
Task RemoveAsync(IObjectDocument document, string tag);
```

### 6. IDocumentTagDocumentFactory - New Methods

```csharp
// NEW in v2.0
IDocumentTagStore CreateDocumentTagStore();  // Uses default settings
IDocumentTagStore CreateStreamTagStore();
IDocumentTagStore CreateStreamTagStore(IObjectDocument document);
```

### 7. Deprecated Properties in IStreamInformation

The following properties are deprecated and will be removed in a future version:

| Deprecated | Replacement |
|------------|-------------|
| `DocumentTagConnectionName` | `DocumentTagStore` |
| `SnapShotConnectionName` | `SnapShotStore` |
| `StreamConnectionName` | `DataStore` |
| `StreamTagConnectionName` | `StreamTagStore` |
| `DocumentConnectionName` | `DocumentStore` |

### 8. IProjectionBase - Deprecated Fold Overloads

The `IObjectDocument`-based Fold overloads are deprecated:

```csharp
// Deprecated
Task Fold<T>(IEvent @event, IObjectDocument document, T? data = null, IExecutionContext? context = null);
Task Fold(IEvent @event, IObjectDocument document);

// Use instead
Task Fold<T>(IEvent @event, VersionToken token, T? data = null, IExecutionContext? context = null);
Task Fold(IEvent @event, VersionToken token);
```

---

## New Features in v2.0

### Event Upcasting (Schema Evolution)

Register upcasters to transform old event schemas:

```csharp
stream.RegisterUpcaster<OrderCreatedV1, OrderCreatedV2>(
    "Order.Created",
    fromVersion: 1,
    toVersion: 2,
    v1 => new OrderCreatedV2(v1.CustomerId, v1.CreatedAt, Email: null));
```

### Streaming Reads

For large event streams, use streaming to avoid memory issues:

```csharp
await foreach (var @event in dataStore.ReadAsStreamAsync(document))
{
    // Process event without loading all into memory
}
```

### IEventStream Extensions

New properties and methods:
- `EventUpcasterRegistry` - Registry for schema version upcasters
- `RegisterEvent<T>(eventName, schemaVersion, jsonTypeInfo)` - Register versioned events
- `RegisterUpcast(IUpcastEvent upcast)` - Register upcasters
- `RegisterUpcaster<TFrom, TTo>(...)` - AOT-friendly upcaster registration

### New Interfaces

- `ITestableAggregate<TSelf>` - AOT-friendly testing support
- `IAggregateStorageRegistry` - Multi-storage aggregate routing
- `IDocumentStore` - Storage-agnostic document operations
- `IDataStoreRecovery` - Recovery operations for failed commits
- `IProjectionFactory<T>` - Typed projection factory
- `IProjectionWhenParameterValueFactoryWithVersionToken<T>` - VersionToken-based parameter factories

---

## Generated Code Changes

If you use the CLI code generator (`dotnet faes`), regenerating will automatically handle:

- Updated method signatures in generated factories
- New `SchemaVersion` property in generated events
- Updated `Fold` method calls using `VersionToken`
- New upcaster registrations

Simply run:
```bash
dotnet faes
```

---

## Migration Checklist

- [ ] Update NuGet packages to v2.0
- [ ] Run `dotnet faes` to regenerate code
- [ ] If implementing `IDataStore`: Add `CancellationToken` parameters and new methods
- [ ] If implementing `ILeasedSession`: Rename `IsTerminatedASync` to `IsTerminatedAsync`
- [ ] If implementing `IEvent`: Add `SchemaVersion` property
- [ ] If implementing `IDocumentTagStore`: Add `RemoveAsync` method
- [ ] If implementing `IObjectDocumentFactory`: Add `CancellationToken` parameters
- [ ] If implementing `ISnapShotStore`: Add `CancellationToken` parameters and new methods
- [ ] Replace deprecated `*ConnectionName` properties with `*Store` equivalents
- [ ] Update `Fold` calls to use `VersionToken` instead of `IObjectDocument`
