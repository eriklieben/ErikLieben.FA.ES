# Table Projection Checkpoints

This document describes the chunked checkpoint storage system for Azure Table Storage projections, including current features and planned future enhancements.

## Overview

Table projections store checkpoint data in a dedicated checkpoints table (`{projectionTableName}checkpoints`). The system supports:

- **Chunked storage**: Large checkpoints are split across multiple rows to overcome Azure Table Storage's 64KB property limit
- **GZip compression**: All checkpoint data is compressed before storage
- **Historical retention**: All checkpoint states are preserved, enabling point-in-time recovery
- **Fingerprint-based addressing**: Each checkpoint is identified by its SHA-256 fingerprint

## Table Schema

### Partition Key

All checkpoint data uses `"checkpoint"` as the partition key, enabling efficient queries.

### Row Types

| Row Type | RowKey Pattern | Purpose |
|----------|----------------|---------|
| Pointer | `{projectionName}_current` | Points to active fingerprint |
| Chunk | `{fingerprint}_{chunkIndex}` | Stores checkpoint data chunks |

### Pointer Row Properties

| Property | Type | Description |
|----------|------|-------------|
| `Fingerprint` | string | SHA-256 hash of current checkpoint (64 hex chars) |
| `LastUpdated` | DateTimeOffset | When the pointer was last updated |

### Chunk Row Properties

| Property | Type | Description |
|----------|------|-------------|
| `Data` | byte[] | GZip compressed chunk (max 60KB) |
| `TotalChunks` | int | Total number of chunks for this fingerprint |
| `ChunkIndex` | int | 0-based index of this chunk |
| `CreatedAt` | DateTimeOffset | When this checkpoint was created |
| `ProjectionName` | string | Name of the projection (for queries) |

## API Reference

### List Historical Checkpoints

```csharp
var factory = serviceProvider.GetRequiredService<WorkItemReportingIndexFactory>();
var history = await factory.GetHistoricalCheckpointsAsync();

foreach (var checkpoint in history)
{
    Console.WriteLine($"Fingerprint: {checkpoint.Fingerprint}");
    Console.WriteLine($"Created: {checkpoint.CreatedAt}");
    Console.WriteLine($"Chunks: {checkpoint.TotalChunks}");
}
```

### Load from Historical Checkpoint

```csharp
var projection = await factory.LoadFromHistoricalCheckpointAsync(
    objectDocumentFactory,
    eventStreamFactory,
    fingerprint: "a3f5b8c2e9d1f4a7b6c9e2f5a8b1d4e7...");
```

### Delete All Historical Data

```csharp
// WARNING: Permanently deletes all historical checkpoint data
await factory.DeleteAllHistoricalCheckpointsAsync();
```

## Example Data

After 3 projection builds:

```
PartitionKey: "checkpoint"

# Pointer to current checkpoint
RowKey: "WorkItemReportingIndex_current"
  Fingerprint: "def789..."
  LastUpdated: 2024-01-07T15:30:00Z

# First checkpoint (small, 1 chunk)
RowKey: "abc123..._0"
  Data: [compressed bytes]
  TotalChunks: 1
  ChunkIndex: 0
  CreatedAt: 2024-01-07T10:00:00Z

# Second checkpoint (large, 2 chunks)
RowKey: "def456..._0"
  Data: [compressed bytes - first 60KB]
  TotalChunks: 2
  ChunkIndex: 0
  CreatedAt: 2024-01-07T12:00:00Z

RowKey: "def456..._1"
  Data: [compressed bytes - remaining]
  TotalChunks: 2
  ChunkIndex: 1
  CreatedAt: 2024-01-07T12:00:00Z

# Current checkpoint
RowKey: "def789..._0"
  Data: [compressed bytes]
  TotalChunks: 1
  ChunkIndex: 0
  CreatedAt: 2024-01-07T15:30:00Z
```

## Backwards Compatibility

The system automatically handles legacy checkpoint formats:

1. First attempts to load using the new chunked format (pointer → chunks)
2. Falls back to legacy single-row format if not found
3. On next save, migrates to new format automatically

---

## Future Enhancements

The following features are planned for future releases:

### 1. Checkpoint Cleanup Policy

Automatically delete old checkpoints based on configurable retention rules.

#### Proposed API

```csharp
// Configuration
services.ConfigureTableProjection<WorkItemReportingIndex>(options =>
{
    options.CheckpointRetention = new CheckpointRetentionPolicy
    {
        // Keep checkpoints for 30 days
        MaxAge = TimeSpan.FromDays(30),

        // Or keep the last 10 checkpoints regardless of age
        MaxCount = 10,

        // Always keep at least one checkpoint
        MinCount = 1
    };
});

// Manual cleanup
await factory.CleanupOldCheckpointsAsync(
    olderThan: TimeSpan.FromDays(30),
    keepMinimum: 5);
```

#### Use Cases

- **Storage cost reduction**: Remove old checkpoints to save storage costs
- **Compliance**: Ensure data is not retained beyond required periods
- **Performance**: Reduce query times when listing historical checkpoints

#### Implementation Notes

- Cleanup runs on a background timer or can be triggered manually
- Never deletes the current checkpoint (pointer row's fingerprint)
- Respects `MinCount` to ensure recovery is always possible

---

### 2. Checkpoint Comparison API

Compare two checkpoint fingerprints to see what changed between them.

#### Proposed API

```csharp
var diff = await factory.CompareCheckpointsAsync(
    oldFingerprint: "abc123...",
    newFingerprint: "def456...");

// See which streams changed
Console.WriteLine($"Streams added: {diff.AddedStreams.Count}");
Console.WriteLine($"Streams removed: {diff.RemovedStreams.Count}");
Console.WriteLine($"Streams updated: {diff.UpdatedStreams.Count}");

// Details of changes
foreach (var update in diff.UpdatedStreams)
{
    Console.WriteLine($"Stream {update.StreamId}:");
    Console.WriteLine($"  Old version: {update.OldVersion}");
    Console.WriteLine($"  New version: {update.NewVersion}");
    Console.WriteLine($"  Events processed: {update.NewVersion - update.OldVersion}");
}
```

#### Response Model

```csharp
public record CheckpointDiff
{
    // Streams that exist in new but not in old
    public IReadOnlyList<StreamInfo> AddedStreams { get; init; }

    // Streams that exist in old but not in new
    public IReadOnlyList<StreamInfo> RemovedStreams { get; init; }

    // Streams that exist in both but have different versions
    public IReadOnlyList<StreamVersionChange> UpdatedStreams { get; init; }

    // Summary statistics
    public int TotalEventsProcessed { get; init; }
    public TimeSpan? TimeBetweenCheckpoints { get; init; }
}

public record StreamVersionChange(
    string StreamId,
    long OldVersion,
    long NewVersion);
```

#### Use Cases

- **Debugging**: Understand what events were processed between builds
- **Auditing**: Track changes over time for compliance
- **Performance analysis**: Identify which streams have high event throughput

---

### 3. Restore from Historical Checkpoint

Rebuild a projection from a specific historical checkpoint state.

#### Proposed API

```csharp
// Restore to a specific point in time
await factory.RestoreFromCheckpointAsync(
    fingerprint: "abc123...",
    rebuildFromEvents: true);  // Optionally replay events from that point

// Or restore to a timestamp
await factory.RestoreToPointInTimeAsync(
    pointInTime: new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
```

#### Workflow

```
1. Find checkpoint closest to requested point
2. Load checkpoint state (Checkpoint dictionary with stream versions)
3. Update current pointer to that fingerprint
4. If rebuildFromEvents:
   a. For each stream in checkpoint, note the version
   b. Replay events from those versions forward
   c. Save new checkpoint when complete
```

#### Use Cases

- **Disaster recovery**: Roll back to a known good state after data corruption
- **Testing**: Compare projection state at different points in time
- **Debugging**: Reproduce issues by restoring to the state when they occurred

#### Safety Considerations

- Creates a new checkpoint rather than modifying historical data
- Optionally validates checkpoint integrity before restore
- Can run in dry-run mode to preview what would change

---

## Implementation Priority

| Feature | Priority | Complexity | Benefit |
|---------|----------|------------|---------|
| Cleanup Policy | High | Medium | Reduces storage costs |
| Checkpoint Comparison | Medium | Low | Improves debugging |
| Restore from Historical | Medium | High | Enables disaster recovery |

---

## Clarification: Stream Chunking vs Payload Chunking

The library has **two different chunking concepts** that shouldn't be confused:

### Existing: Stream Chunking (Event Count)

The library already supports splitting large **event streams** into multiple storage files/partitions based on **event count**. This is implemented in Blob Storage:

```csharp
// Configuration
services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store",
    enableStreamChunks: true,
    defaultChunkSize: 1000));  // 1000 events per chunk
```

**How it works:**
- Streams with >1000 events are split into multiple blob files
- Naming: `{stream-id}-0000000000.json`, `{stream-id}-0000000001.json`, etc.
- Metadata tracks which events are in which chunk
- Reading automatically iterates through all chunks

**This does NOT solve the 64KB payload limit** - it only manages streams with many small events.

### Future: Payload Chunking (Event Size)

A **separate feature** to handle individual events that exceed 64KB.

---

## Large Event Payload Chunking (Implemented)

Azure Table Storage has a 64KB property size limit, which restricts the maximum size of individual event payloads. This is a different problem than having many events.

### 4. Conditional Large Event Payload Chunking

Automatically chunk individual events only when their payload exceeds the 64KB limit. **This feature is now implemented.**

#### Problem Statement

Currently, if an event's serialized JSON payload exceeds 64KB, Azure Table Storage will reject it with an error. Developers must either:
- Use a different storage provider (Blob, CosmosDB)
- Manually compress or split large payloads
- Ensure all events stay under 64KB

**Note:** The existing `ChunkIdentifier` property on `TableEventEntity` is for stream chunking (event count), not payload chunking.

#### Proposed Solution

Implement automatic, conditional chunking for large event payloads:

1. **Detection**: After serializing the event, check if payload > 60KB (threshold with buffer)
2. **Compression**: Apply GZip compression to the payload
3. **Chunking**: If still > 60KB, split payload into multiple table rows
4. **Reassembly**: When reading, automatically detect and reassemble chunked payloads

#### Storage Schema

For normal events (< 64KB payload):
```
RowKey: "00000000000000000042"  (version number)
Payload: "{...json...}"
PayloadChunked: false (or null)
```

For chunked event payloads (>= 64KB):
```
# First chunk contains metadata
RowKey: "00000000000000000042"
Payload: "[compressed chunk 0]"
PayloadChunked: true
PayloadTotalChunks: 3
PayloadCompressed: true

# Additional payload chunks (same event version, different payload chunk index)
RowKey: "00000000000000000042_p1"
PayloadChunkData: "[compressed chunk 1]"
PayloadChunkIndex: 1

RowKey: "00000000000000000042_p2"
PayloadChunkData: "[compressed chunk 2]"
PayloadChunkIndex: 2
```

**Note:** Using `_p{index}` suffix to distinguish from stream chunks which use different naming.

#### Configuration

```csharp
services.ConfigureTableEventStore(new EventStreamTableSettings(
    "Store",
    enableLargePayloadChunking: true,      // Enable automatic chunking for large event payloads
    payloadChunkThresholdBytes: 60 * 1024, // Maximum payload size before chunking (default 60KB)
    compressLargePayloads: true            // Compress large payloads before chunking
));
```

#### Reading Flow

```
1. Query events for stream
2. For each event:
   a. If PayloadChunked is false/null → normal event, return as-is
   b. If PayloadChunked is true:
      - Read PayloadTotalChunks from metadata
      - Fetch all payload chunk rows (same version, _p{index} suffix)
      - Concatenate payload chunks in order
      - Decompress if PayloadCompressed
      - Return reassembled event
```

#### Writing Flow

```
1. Serialize event to JSON
2. Check payload size:
   a. If < threshold → store normally (PayloadChunked = false)
   b. If >= threshold:
      - Compress with GZip
      - If still >= threshold → split into payload chunks
      - Store main event row with PayloadChunked = true
      - Store additional rows for remaining chunks
```

#### Use Cases

- **Storing large aggregates**: Orders with many line items, documents with rich content
- **Embedded attachments**: Base64-encoded files in event payloads
- **Complex domain events**: Events with large nested structures

#### Performance Considerations

- Chunked payloads require multiple round-trips to read
- Consider using Blob Storage if most events exceed 64KB
- Compression adds CPU overhead but significantly reduces storage size

#### Backward Compatibility

- Existing events without PayloadChunked continue to work unchanged
- Only new large events use the payload chunking mechanism
- No migration required for existing data

#### Comparison: Stream Chunking vs Payload Chunking

| Aspect | Stream Chunking | Payload Chunking |
|--------|-----------------|------------------|
| **Problem** | Too many events in one stream | Individual event too large |
| **Trigger** | Event count > threshold | Payload size > 64KB |
| **Default** | 1000 events per chunk | 60KB per payload chunk |
| **Storage** | Separate files/partitions | Additional rows per event |
| **Providers** | Blob Storage | Table Storage only |
| **Status** | Implemented | Implemented |

---

## Storage Provider Size Limits Reference

| Provider | Event Payload Limit | Notes |
|----------|---------------------|-------|
| Azure Table Storage | 64 KB | Property size limit |
| Azure Blob Storage | Unlimited* | Limited by blob size (190 GB block blob) |
| Azure CosmosDB | 2 MB | Document size limit |

*Blob Storage stores all events in a single JSON file, so practical limits depend on stream size.

---

## Related Documentation

- [Projections](./Projections.md) - Overview of projection patterns
- [Storage Providers](./StorageProviders.md) - Azure Table Storage configuration
- [Backup & Restore](./BackupRestore.md) - Data protection strategies
