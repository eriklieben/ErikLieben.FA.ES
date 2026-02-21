# Live Stream Migration

This guide explains how to perform **live migrations** - moving events from one stream to another while the source stream remains active, without data loss or service interruption.

## Table of Contents

- [Overview](#overview)
- [How It Works](#how-it-works)
- [Quick Start](#quick-start)
- [Automatic Retry Behavior](#automatic-retry-behavior)
- [Code Samples](#code-samples)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)

---

## Overview

### The Problem

In event-sourced systems, you sometimes need to move an aggregate's event stream to a new location:

- **Storage migration**: Moving from Blob Storage to Cosmos DB
- **Region migration**: Moving data closer to users
- **Schema changes**: Stream restructuring that requires a fresh stream
- **Performance**: Splitting hot streams or consolidating cold ones

Traditional migration requires **downtime**: stop writes, copy events, switch over. Live migration eliminates this downtime.

### The Solution

Live migration copies events while the source stream remains active:

```
Before Migration:
  Source: [E1] [E2] [E3] ... [E100] ← Writers adding events here

During Migration:
  Source: [E1] [E2] [E3] ... [E100] [E101] [E102] ← Still active!
  Target: [E1] [E2] [E3] ... [E100]                ← Catching up

After Migration:
  Source: [E1] ... [E102] [CLOSED]  ← Sealed with CloseEvent
  Target: [E1] ... [E102] [E103] [E104] ← Now the active stream
```

### Key Guarantees

1. **Zero data loss**: Every event written to source ends up in target
2. **Correct ordering**: Events maintain their original order
3. **Automatic failover**: Writers automatically retry on the new stream
4. **No duplicates**: Optimistic concurrency prevents double-writes

---

## How It Works

### The Migration Flow

```
┌────────────────────────────────────────────────────────────────┐
│  PHASE 1: CATCH-UP                                             │
│                                                                │
│  Migration process copies events from source to target         │
│  while source continues to receive new writes.                 │
│                                                                │
│  IMPORTANT: Only business events are copied.                   │
│  The CloseEvent is NEVER copied to the target stream!          │
│                                                                │
│  Source: [E1][E2][E3]...[E100] ← Writers still active          │
│  Target: [E1][E2][E3]...[E95]  ← Copying in progress           │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│  PHASE 2: SYNC CHECK                                           │
│                                                                │
│  Verify target has all events from source.                     │
│                                                                │
│  Source version: 102                                           │
│  Target version: 102 ✓ Synced!                                 │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│  PHASE 3: ATOMIC CLOSE                                         │
│                                                                │
│  Attempt to append CloseEvent to source using optimistic       │
│  concurrency (expectedVersion = 102).                          │
│                                                                │
│  ┌─ SUCCESS ──────────────────────────────────────────┐        │
│  │ No new events arrived. Source is now closed.       │        │
│  │ Target is guaranteed to have all events.           │        │
│  └────────────────────────────────────────────────────┘        │
│                                                                │
│  ┌─ CONFLICT ─────────────────────────────────────────┐        │
│  │ New events arrived (E103, E104...)!                │        │
│  │ Go back to Phase 1 and copy them.                  │        │
│  └────────────────────────────────────────────────────┘        │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│  PHASE 4: UPDATE ROUTING                                       │
│                                                                │
│  Update ObjectDocument.Active to point to target stream.       │
│  Add source to TerminatedStreams with continuation pointer.    │
│                                                                │
│  ObjectDocument:                                               │
│    Active: target-stream-456                                   │
│    TerminatedStreams:                                          │
│      - source-stream-123 → continues at target-stream-456      │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│  PHASE 5: AUTOMATIC FAILOVER                                   │
│                                                                │
│  Writers that still try to write to source:                    │
│  1. Get EventStreamClosedException                             │
│  2. Automatically retry on target (continuation stream)        │
│  3. Success! Event written to target                           │
│                                                                │
│  This happens transparently - no code changes needed.          │
└────────────────────────────────────────────────────────────────┘
```

### Why This Works

The **optimistic concurrency check** on the CloseEvent is the key:

```
Scenario: Source at version 100, target synced to 100

Attempt close with expectedVersion=100:
  ├─ If source still at 100 → SUCCESS, CloseEvent written at 101
  └─ If source now at 102   → CONFLICT, must copy E101, E102 first

This guarantees: When close succeeds, target has ALL source events.
```

### No Dual-Write (By Design)

You might wonder: "Why not write to both streams during migration?"

Dual-write causes **ordering problems**:

```
BAD - With dual-write:
  Source: [E1]...[E100] ← Writer A writes E101
  Target: [E1]...[E95]  ← Writer A also writes E101 here

  Result: Target has [E1]...[E95][E101][E96][E97]... WRONG ORDER!

GOOD - Without dual-write:
  1. Copy E96-E100 to target
  2. Close source at version 100
  3. Writer A's E101 goes to target

  Result: Target has [E1]...[E100][E101] CORRECT ORDER!
```

---

## Quick Start

### Basic Live Migration

```csharp
// Get the migration service (injected via DI)
var migrationService = serviceProvider.GetRequiredService<IEventStreamMigrationService>();

// Execute a live migration
var result = await migrationService
    .ForDocument(orderDocument)
    .WithSourceStream("orders-12345")
    .WithTargetStream("orders-12345-v2")
    .WithLiveMigration()  // Enable live migration mode
    .ExecuteAsync();

if (result.IsSuccess)
{
    Console.WriteLine($"Migration completed!");
    Console.WriteLine($"  Events migrated: {result.Statistics.TotalEvents}");
    Console.WriteLine($"  Catch-up attempts: {result.Statistics.CatchUpAttempts}");
}
```

### Live Migration with Progress Monitoring

```csharp
var result = await migrationService
    .ForDocument(orderDocument)
    .WithSourceStream("orders-12345")
    .WithTargetStream("orders-12345-v2")
    .WithLiveMigration(options => options
        .WithCloseTimeout(TimeSpan.FromMinutes(5))
        .OnCatchUpProgress(progress =>
        {
            Console.WriteLine($"Catch-up iteration {progress.Iteration}:");
            Console.WriteLine($"  Source version: {progress.SourceVersion}");
            Console.WriteLine($"  Target version: {progress.TargetVersion}");
            Console.WriteLine($"  Events behind: {progress.EventsBehind}");
        }))
    .WithProgress(progress => progress
        .WithInterval(TimeSpan.FromSeconds(2))
        .OnProgress(p => Console.WriteLine($"Overall: {p.PercentComplete:F1}%")))
    .ExecuteAsync();
```

### Live Migration with Transformation

```csharp
// Migrate AND transform events (e.g., schema upgrade)
var result = await migrationService
    .ForDocument(orderDocument)
    .WithSourceStream("orders-12345")
    .WithTargetStream("orders-12345-v2")
    .WithLiveMigration()
    .WithTransformer(new FunctionTransformer(
        canTransform: (name, version) => name == "OrderCreated" && version == 1,
        transform: async (evt, ct) =>
        {
            // Upgrade OrderCreated v1 to v2
            var v1 = JsonSerializer.Deserialize<OrderCreatedV1>(evt.Payload);
            var v2 = new OrderCreatedV2
            {
                OrderId = v1.OrderId,
                CustomerId = v1.CustomerId,
                CustomerEmail = v1.Email,  // Renamed field
                Items = v1.Items,
                CreatedAt = v1.Timestamp   // Renamed field
            };
            return evt.WithPayload(v2, version: 2);
        }))
    .ExecuteAsync();
```

---

## Automatic Retry Behavior

When a stream is closed, writers automatically retry on the continuation stream. This is **enabled by default**.

### How It Works

```csharp
// Your existing code - no changes needed!
await orderStream.Session(session =>
{
    session.Append(new OrderShipped { OrderId = orderId, ShippedAt = DateTime.UtcNow });
});

// Behind the scenes, if the stream was just closed:
// 1. Append attempt fails with EventStreamClosedException
// 2. Library reloads ObjectDocument (now points to new stream)
// 3. Library retries the append on the new stream
// 4. Success! Your code doesn't even know migration happened.
```

### Retry Flow Diagram

```
Your Code                    Library                         Storage
    │                           │                               │
    ├── Append(OrderShipped) ──→│                               │
    │                           ├── Write to source-stream ────→│
    │                           │                               │
    │                           │←── ERROR: Stream closed! ─────┤
    │                           │    (CloseEvent detected)      │
    │                           │                               │
    │                           ├── Reload ObjectDocument       │
    │                           │   (Active = target-stream)    │
    │                           │                               │
    │                           ├── Write to target-stream ────→│
    │                           │                               │
    │                           │←── SUCCESS ───────────────────┤
    │                           │                               │
    │←── Success ───────────────┤                               │
    │                           │                               │
```

### Disabling Automatic Retry

If you need manual control over stream closures:

```csharp
// Option 1: Disable globally in settings
services.Configure<EventStreamSettings>(settings =>
{
    settings.AutoRetryOnStreamClosure = false;
});

// Option 2: Handle the exception yourself
try
{
    await orderStream.Session(session =>
    {
        session.Append(new OrderShipped { OrderId = orderId });
    });
}
catch (EventStreamClosedException ex) when (ex.HasContinuation)
{
    // Log, alert, or handle manually
    logger.LogWarning(
        "Stream {StreamId} was closed. Continuation: {ContinuationId}",
        ex.StreamIdentifier,
        ex.ContinuationStreamId);

    // Reload and retry manually
    var newDocument = await documentFactory.GetAsync("Order", orderId);
    var newStream = streamFactory.Create(newDocument);
    await newStream.Session(session =>
    {
        session.Append(new OrderShipped { OrderId = orderId });
    });
}
```

---

## Code Samples

### Sample 1: Basic Aggregate Usage (No Changes Needed)

Your existing aggregate code works unchanged during and after migration:

```csharp
public class OrderService
{
    private readonly IAggregateFactory<Order> _orderFactory;

    public async Task ShipOrderAsync(string orderId, string trackingNumber)
    {
        // Load aggregate (automatically uses correct stream)
        var order = await _orderFactory.GetAsync(orderId);

        // Apply domain logic
        order.Ship(trackingNumber);

        // Save (automatic retry if stream was just migrated)
        await order.SaveAsync();
    }
}
```

### Sample 2: Handling Migration in Background Service

```csharp
public class StreamMigrationService : BackgroundService
{
    private readonly IEventStreamMigrationService _migrationService;
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<StreamMigrationService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Get streams that need migration (your criteria)
        var streamsToMigrate = await GetStreamsNeedingMigrationAsync();

        foreach (var streamInfo in streamsToMigrate)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await MigrateStreamAsync(streamInfo, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate stream {StreamId}",
                    streamInfo.SourceStreamId);
            }
        }
    }

    private async Task MigrateStreamAsync(
        StreamMigrationInfo info,
        CancellationToken ct)
    {
        var document = await _documentStore.GetAsync(info.ObjectName, info.ObjectId);

        var result = await _migrationService
            .ForDocument(document)
            .WithSourceStream(info.SourceStreamId)
            .WithTargetStream(info.TargetStreamId)
            .WithLiveMigration(options => options
                .WithCloseTimeout(TimeSpan.FromMinutes(10))
                .OnCatchUpProgress(p => _logger.LogInformation(
                    "Migration {ObjectId}: {Behind} events behind",
                    info.ObjectId, p.EventsBehind)))
            .WithDistributedLock()  // Prevent concurrent migrations
            .ExecuteAsync(ct);

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Migrated {ObjectId}: {Events} events in {Attempts} catch-up attempts",
                info.ObjectId,
                result.Statistics.TotalEvents,
                result.Statistics.CatchUpAttempts);
        }
        else
        {
            _logger.LogError("Migration failed for {ObjectId}: {Error}",
                info.ObjectId, result.Error);
        }
    }
}
```

### Sample 3: Reading from Migrated Streams

When reading historical events, you may need to follow the continuation chain:

```csharp
public class EventHistoryService
{
    private readonly IDataStoreFactory _dataStoreFactory;
    private readonly IDocumentStore _documentStore;

    public async Task<IReadOnlyList<IEvent>> GetFullHistoryAsync(
        string objectName,
        string objectId)
    {
        var document = await _documentStore.GetAsync(objectName, objectId);
        var allEvents = new List<IEvent>();

        // Read from terminated streams first (oldest to newest)
        foreach (var terminated in document.TerminatedStreams.OrderBy(t => t.TerminationDate))
        {
            var dataStore = _dataStoreFactory.Get(terminated.StreamConnectionName);
            var events = await dataStore.ReadAsync(
                terminated.StreamIdentifier,
                startVersion: 0);

            if (events != null)
            {
                // IMPORTANT: Exclude the CloseEvent from history!
                // The CloseEvent is a system event, not a business event.
                // It should never be replayed or included in projections.
                allEvents.AddRange(events.Where(e => e.EventType != "EventStream.Closed"));
            }
        }

        // Read from active stream
        var activeDataStore = _dataStoreFactory.Get(document.Active.DataStore);
        var activeEvents = await activeDataStore.ReadAsync(
            document.Active.StreamIdentifier,
            startVersion: 0);

        if (activeEvents != null)
        {
            allEvents.AddRange(activeEvents);
        }

        return allEvents;
    }
}
```

### Sample 4: Monitoring Active Migrations

```csharp
public class MigrationMonitor
{
    private readonly IMigrationRoutingTable _routingTable;

    public async Task<IReadOnlyList<ActiveMigration>> GetActiveMigrationsAsync()
    {
        var entries = await _routingTable.GetAllEntriesAsync();

        return entries
            .Where(e => e.Phase != MigrationPhase.BookClosed)
            .Select(e => new ActiveMigration
            {
                ObjectId = e.ObjectId,
                SourceStream = e.OldStream,
                TargetStream = e.NewStream,
                Phase = e.Phase,
                StartedAt = e.CreatedAt,
                LastUpdated = e.UpdatedAt
            })
            .ToList();
    }
}

public record ActiveMigration
{
    public required string ObjectId { get; init; }
    public required string SourceStream { get; init; }
    public required string TargetStream { get; init; }
    public required MigrationPhase Phase { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }
}
```

### Sample 5: Graceful Shutdown During Migration

```csharp
public class GracefulMigrationService
{
    private readonly IEventStreamMigrationService _migrationService;
    private readonly CancellationTokenSource _shutdownCts = new();

    public async Task StartMigrationAsync(IObjectDocument document)
    {
        var result = await _migrationService
            .ForDocument(document)
            .WithSourceStream(document.Active.StreamIdentifier)
            .WithTargetStream($"{document.Active.StreamIdentifier}-v2")
            .WithLiveMigration(options => options
                .OnConvergenceFailure(ConvergenceFailureStrategy.KeepTrying))
            .ExecuteAsync(_shutdownCts.Token);

        // If cancelled, migration state is preserved
        // Can be resumed later
        if (result.Status == MigrationStatus.Cancelled)
        {
            Console.WriteLine("Migration paused for shutdown. Can be resumed.");
        }
    }

    public void RequestShutdown()
    {
        // Signal migration to stop gracefully
        _shutdownCts.Cancel();
    }
}
```

### Sample 6: Testing with Simulated Migration

```csharp
[Test]
public async Task OrderAggregate_SurvivesMigration_WithoutDataLoss()
{
    // Arrange
    var orderId = "order-123";
    var orderFactory = _serviceProvider.GetRequiredService<IAggregateFactory<Order>>();

    // Create order with some events
    var order = await orderFactory.CreateAsync(orderId);
    order.Create(customerId: "cust-1", items: new[] { "item-1", "item-2" });
    order.Confirm();
    await order.SaveAsync();

    // Act - Simulate live migration while adding more events
    var migrationTask = Task.Run(async () =>
    {
        await Task.Delay(50); // Let some writes happen first

        var document = await _documentStore.GetAsync("Order", orderId);
        await _migrationService
            .ForDocument(document)
            .WithSourceStream(document.Active.StreamIdentifier)
            .WithTargetStream($"{document.Active.StreamIdentifier}-migrated")
            .WithLiveMigration()
            .ExecuteAsync();
    });

    // Concurrent writes during migration
    for (int i = 0; i < 10; i++)
    {
        order = await orderFactory.GetAsync(orderId);
        order.AddNote($"Note {i}");
        await order.SaveAsync();
        await Task.Delay(10);
    }

    await migrationTask;

    // Assert - All events present after migration
    order = await orderFactory.GetAsync(orderId);
    Assert.That(order.Notes.Count, Is.EqualTo(10));
    Assert.That(order.Status, Is.EqualTo(OrderStatus.Confirmed));

    // Verify stream was migrated
    var (_, document) = await orderFactory.GetWithDocumentAsync(orderId);
    Assert.That(document.Active.StreamIdentifier, Does.Contain("-migrated"));
    Assert.That(document.TerminatedStreams, Has.Count.EqualTo(1));
}
```

---

## Configuration

### EventStreamSettings

```csharp
services.Configure<EventStreamSettings>(settings =>
{
    // Automatic retry when stream is closed (default: true)
    settings.AutoRetryOnStreamClosure = true;

    // Max retry attempts when following continuations (default: 3)
    // Handles edge case of multiple rapid migrations
    settings.MaxStreamContinuationRetries = 3;
});
```

### Live Migration Options

```csharp
.WithLiveMigration(options => options
    // Max time to keep trying to close the source stream
    .WithCloseTimeout(TimeSpan.FromMinutes(5))

    // Delay between catch-up attempts (prevents tight loops)
    .WithCatchUpDelay(TimeSpan.FromMilliseconds(100))

    // Progress callback for monitoring
    .OnCatchUpProgress(progress => { /* ... */ })

    // What to do if migration can't converge
    .OnConvergenceFailure(ConvergenceFailureStrategy.KeepTrying)
)
```

### Convergence Failure Strategies

| Strategy | Description | Use When |
|----------|-------------|----------|
| `KeepTrying` | Keep attempting until timeout | Stream is moderately active |
| `Fail` | Fail the migration immediately | Need manual intervention |
| `PauseSource` | Temporarily pause source writes | Stream is very active (requires coordination) |

---

## Troubleshooting

### Migration Takes Too Long

**Symptom**: Many catch-up iterations, migration doesn't converge.

**Cause**: Source stream is receiving writes faster than migration can copy.

**Solutions**:
1. Schedule migration during low-traffic periods
2. Increase `CatchUpDelay` to batch more events per iteration
3. Consider `PauseSource` strategy for very active streams
4. Check for any performance bottlenecks in target storage

### EventStreamClosedException Not Retried

**Symptom**: Application throws `EventStreamClosedException` instead of retrying.

**Possible Causes**:
1. `AutoRetryOnStreamClosure` is disabled
2. `MaxStreamContinuationRetries` exceeded
3. CloseEvent doesn't have continuation info

**Check**:
```csharp
catch (EventStreamClosedException ex)
{
    Console.WriteLine($"HasContinuation: {ex.HasContinuation}");
    Console.WriteLine($"ContinuationStreamId: {ex.ContinuationStreamId}");
}
```

### Duplicate Events After Migration

**Symptom**: Same event appears twice in target stream.

**This should not happen** with the optimistic concurrency design. If you see this:
1. Check that you're not using dual-write
2. Verify the migration completed successfully (wasn't interrupted)
3. Check for bugs in custom retry logic (if AutoRetry is disabled)

### ObjectDocument Not Updated

**Symptom**: Migration completed but ObjectDocument.Active still points to source.

**Possible Causes**:
1. Migration was interrupted after CloseEvent but before document update
2. Document store write failed

**Recovery**:
```csharp
// Manual recovery: update ObjectDocument to point to target
var document = await documentStore.GetAsync(objectName, objectId);

if (document.Active.StreamIdentifier == sourceStreamId)
{
    // Check if source is actually closed
    var events = await dataStore.ReadAsync(sourceStreamId, 0);
    var closeEvent = events?.LastOrDefault(e => e.EventType == "EventStream.Closed");

    if (closeEvent != null)
    {
        // Source is closed, update document manually
        var closedData = DeserializeCloseEvent(closeEvent);
        // ... update document.Active to point to continuation
        await documentStore.SetAsync(document);
    }
}
```

---

## FAQ

### Q: Can I migrate to a different storage provider?

**Yes!** You can migrate from Blob Storage to Cosmos DB, for example:

```csharp
await migrationService
    .ForDocument(document)
    .WithSourceStream("orders-123")  // In Blob Storage
    .WithTargetStream("orders-123")  // Same ID, different provider
    .WithTargetStreamType("cosmos")  // Specify target provider
    .WithTargetDataStore("cosmosdb-connection")
    .WithLiveMigration()
    .ExecuteAsync();
```

### Q: What happens if the migration process crashes?

The migration is **crash-safe**:
- Target stream exists with copied events (can be detected and resumed)
- Source stream is still open (not yet closed)
- Distributed lock expires, allowing retry
- On restart, detect partial migration and resume catch-up

### Q: Can I roll back a migration?

Not automatically, but you can:
1. Run another live migration back to the original stream
2. Or manually update ObjectDocument.Active to point back

### Q: How long does migration take?

Depends on:
- Number of events to copy
- How active the source stream is
- Network latency between source and target
- Storage performance

Typical: seconds to minutes for most streams. Very active streams may take longer due to catch-up iterations.

### Q: Can I run multiple migrations concurrently?

For **different** objects: Yes, fully supported.

For the **same** object: No, distributed locking prevents this. One migration must complete before another can start.

### Q: Do I need to update my application code?

**No!** That's the beauty of live migration:
- Automatic retry handles the switchover
- Aggregates load from the correct stream automatically
- Your business logic code is unchanged

---

## See Also

- [Event Stream Management Overview](EventStreamManagement.md)
- [Live Migration Implementation Plan](LiveMigrationPlan.md) (technical details)
