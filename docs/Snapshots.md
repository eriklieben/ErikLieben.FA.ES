# Snapshots

Snapshots capture aggregate state at a specific version, enabling faster loading by skipping event replay for already-processed events.

## Overview

Without snapshots, loading an aggregate replays all events from the beginning. For aggregates with thousands of events, this can be slow. Snapshots store the aggregate state at a point in time, allowing you to:

1. Load the snapshot
2. Replay only events after the snapshot version
3. Significantly reduce load time

## When to Use Snapshots

| Scenario | Recommendation |
|----------|----------------|
| < 100 events | Usually not needed |
| 100-1000 events | Consider for frequently accessed aggregates |
| > 1000 events | Strongly recommended |
| Time-sensitive operations | Recommended |

## Creating Snapshots

### In Aggregate Commands

```csharp
[Aggregate]
public partial class Order : Aggregate
{
    public Order(IEventStream stream) : base(stream) { }

    public int EventCount { get; private set; }

    // Create snapshot after significant operations
    public async Task Complete(string completedBy)
    {
        await Stream.Session(context =>
            Fold(context.Append(new OrderCompleted(completedBy, DateTime.UtcNow))));

        // Create snapshot if event count exceeds threshold
        if (EventCount > 100)
        {
            await Stream.Snapshot<Order>(Stream.CurrentVersion);
        }
    }

    private void When(OrderCompleted @event)
    {
        Status = OrderStatus.Completed;
        EventCount++;
    }
}
```

### Named Snapshots

```csharp
// Create a named snapshot for specific milestones
await Stream.Snapshot<Order>(Stream.CurrentVersion, "pre-migration");
await Stream.Snapshot<Order>(Stream.CurrentVersion, "end-of-month");
```

### Periodic Snapshots

```csharp
public class SnapshotService
{
    private readonly IAggregateFactory<Order> _factory;
    private const int SnapshotThreshold = 500;

    public async Task CreateSnapshotIfNeeded(string orderId)
    {
        var order = await _factory.GetAsync(orderId);

        // Check if snapshot needed
        var lastSnapshotVersion = order.Stream.Document.Active.SnapShots
            .OrderByDescending(s => s.UntilVersion)
            .FirstOrDefault()?.UntilVersion ?? 0;

        var eventsSinceSnapshot = order.Stream.CurrentVersion - lastSnapshotVersion;

        if (eventsSinceSnapshot >= SnapshotThreshold)
        {
            await order.Stream.Snapshot<Order>(order.Stream.CurrentVersion);
        }
    }
}
```

## Loading with Snapshots

Snapshots are used automatically when loading aggregates. The system:

1. Checks for the latest snapshot
2. Loads the snapshot state
3. Replays events after the snapshot version

```csharp
// This automatically uses snapshots if available
var order = await orderFactory.GetAsync(orderId);
```

## Retrieving Snapshots Manually

```csharp
// Get snapshot at specific version
var snapshot = await stream.GetSnapShot(version: 100);

// Get named snapshot
var preReleaseSnapshot = await stream.GetSnapShot(version: 500, name: "pre-release");
```

## Snapshot Storage

Snapshots are stored in the configured snapshot store:

### Blob Storage

```csharp
services.ConfigureBlobEventStore(new EventStreamBlobSettings(
    defaultDataStore: "Store",
    defaultSnapShotStore: "Store"  // Can use separate connection
));
```

### Table Storage

```csharp
services.ConfigureTableEventStore(new EventStreamTableSettings(
    defaultDataStore: "Store",
    defaultSnapShotStore: "Store",
    defaultSnapshotTableName: "snapshots"
));
```

### Cosmos DB

```csharp
services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings
{
    SnapshotsContainerName = "snapshots"
});
```

## JSON Type Configuration

Snapshots require JSON serialization configuration:

```csharp
// Generated code sets this up automatically
// For manual configuration:
stream.SetSnapShotType(OrderJsonSerializerContext.Default.Order);
stream.SetAggregateType(OrderJsonSerializerContext.Default.Order);
```

## Snapshot Metadata

Snapshot information is tracked in the object document:

```csharp
// Access snapshot metadata
var snapshots = order.Stream.Document.Active.SnapShots;

foreach (var snapshot in snapshots)
{
    Console.WriteLine($"Version: {snapshot.UntilVersion}, Name: {snapshot.Name}");
}
```

## Best Practices

### Do

- Create snapshots at regular intervals (every N events)
- Use named snapshots for significant milestones
- Include snapshot creation in batch/background jobs
- Monitor aggregate event counts
- Run `dotnet faes` after modifying aggregates (generates JSON contexts)

### Don't

- Don't create snapshots too frequently (storage overhead)
- Don't rely on snapshots for data integrity (events are source of truth)
- Don't delete events after creating snapshots
- Don't manually modify snapshot data

## Snapshot Strategy Examples

### Event Count Based

```csharp
private const int SnapshotInterval = 100;

public async Task ProcessEvent()
{
    // ... process event ...

    if (Stream.CurrentVersion % SnapshotInterval == 0)
    {
        await Stream.Snapshot<MyAggregate>(Stream.CurrentVersion);
    }
}
```

### Time Based

```csharp
public async Task DailySnapshotJob()
{
    var aggregateIds = await GetActiveAggregateIds();

    foreach (var id in aggregateIds)
    {
        var aggregate = await factory.GetAsync(id);
        await aggregate.Stream.Snapshot<MyAggregate>(
            aggregate.Stream.CurrentVersion,
            $"daily-{DateTime.UtcNow:yyyy-MM-dd}");
    }
}
```

### Milestone Based

```csharp
public async Task CompletePhase(string phase)
{
    await Stream.Session(context =>
        Fold(context.Append(new PhaseCompleted(phase))));

    // Create milestone snapshot
    await Stream.Snapshot<Project>(Stream.CurrentVersion, $"phase-{phase}");
}
```

## Backup Considerations

When using backup/restore:

```csharp
var backupOptions = new BackupOptions
{
    IncludeSnapshots = true  // Include snapshots in backup
};

var restoreOptions = new RestoreOptions
{
    RestoreSnapshots = true  // Restore snapshots from backup
};
```

## See Also

- [Aggregates](Aggregates.md) - Aggregate patterns
- [Configuration](Configuration.md) - Storage configuration
- [Backup and Restore](BackupRestore.md) - Backup with snapshots
- [Storage Providers](StorageProviders.md) - Provider details
