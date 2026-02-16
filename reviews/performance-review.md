# Performance Review: ErikLieben.FA.ES Event Sourcing Library

**Date:** 2026-02-16
**Scope:** `D:\ErikLieben.FA.ES\src\` (all source projects)
**Branch:** `vnext`

---

## Executive Summary

The library demonstrates strong architectural foundations with AOT-compatible source-generated serialization, frozen dictionary lookups for event type resolution, and proper observability instrumentation. However, there are several significant performance issues in hot paths -- particularly in Blob/S3 storage providers that use a read-modify-write pattern requiring full JSON document download and re-upload for every append. Table storage has a costly "stream closed" check that scans the entire partition on every write. Several areas show unnecessary list materializations and memory allocations on read paths.

---

## 1. Hot Path: Blob/S3 Read-Modify-Write Pattern

### CRITICAL -- Full Document Download + Re-Upload on Every Append

**Files:**
- `src/ErikLieben.FA.ES.AzureStorage/Blob/BlobDataStore.cs:316-345`
- `src/ErikLieben.FA.ES.S3/S3DataStore.cs:268-306`

**Description:**
The `AppendToExistingBlobAsync` method in `BlobDataStore` downloads the entire blob document (all events), deserializes it, appends new events to the in-memory list, re-serializes everything, and uploads the whole document back. The S3 data store follows the same pattern.

```csharp
// BlobDataStore.cs:330-342
var doc = (await blob.AsEntityAsync(
    BlobDataStoreDocumentContext.Default.BlobDataStoreDocument,
    new BlobRequestConditions { IfMatch = etag })).Item1;
// ...
doc.Events.AddRange(events.Select(e => BlobJsonEvent.From(e, preserveTimestamp))!);
await blob.SaveEntityAsync(doc, ...);
```

**Impact:**
For a stream with 500 events (each ~2KB payload), every single append operation:
1. Downloads ~1MB of JSON
2. Deserializes all 500 events into memory
3. Adds 1 event
4. Re-serializes all 501 events
5. Uploads ~1MB+ of JSON

This is O(N) in both network I/O and memory for every write, where N is the total number of events in the stream. For streams with thousands of events, this becomes a severe bottleneck. The cost grows linearly with stream age.

**Suggested Fix:**
- Enable chunking by default or recommend it in documentation for production use. The chunking implementation already limits the read-modify-write window to the chunk size (default 1000).
- For blob storage, consider using Azure Append Blobs instead of Block Blobs for event data, which support atomic append operations without reading the existing content.
- For S3, consider using separate objects per event or per batch (similar to how CosmosDB stores individual event entities), rather than a single monolithic JSON document.

---

### CRITICAL -- Blob Storage Double Network Round-Trip on Append

**File:** `src/ErikLieben.FA.ES.AzureStorage/Blob/BlobDataStore.cs:257-264`

**Description:**
Before appending to a blob, the code calls `blob.ExistsAsync()` followed by either `CreateNewBlobAsync` or `AppendToExistingBlobAsync`. The existing blob path then calls `blob.GetPropertiesAsync()` for the ETag, followed by `blob.AsEntityAsync()` for the download. This is 3 network round-trips minimum:

```csharp
if (!await blob.ExistsAsync(cancellationToken))  // 1st call
{
    await CreateNewBlobAsync(...);                 // 1 call (upload)
    return;
}
await AppendToExistingBlobAsync(...);              // 2 more calls (GetProperties + Download + Upload)
```

**Impact:** Each append operation requires 3-4 network round-trips to Azure Blob Storage. At typical Azure latencies of 5-20ms per call, this adds 15-80ms of pure network overhead per append.

**Suggested Fix:**
Eliminate the `ExistsAsync` check by attempting the download directly and catching the 404. The `AsEntityAsync` extension already handles `BlobNotFound`:

```csharp
// Try to download directly - handles 404 internally
var (doc, etag) = await blob.AsEntityAsync(...);
if (doc == null)
{
    await CreateNewBlobAsync(...);
    return;
}
await AppendToExistingBlobAsync(doc, etag, ...);
```

This saves one network round-trip on every append to an existing stream (the common case).

---

### CRITICAL -- S3 Triple Network Round-Trip on Append

**File:** `src/ErikLieben.FA.ES.S3/S3DataStore.cs:225-306`

**Description:**
The S3 append path has the same issue but worse -- it calls `EnsureBucketAsync`, `ObjectExistsAsync`, `GetObjectETagAsync`, and then `GetObjectAsEntityAsync` before the final `PutObjectAsEntityAsync`:

```csharp
await s3Client.EnsureBucketAsync(bucketName);           // 1st call
var exists = await s3Client.ObjectExistsAsync(bucketName, key);  // 2nd call
// ...
var etag = await s3Client.GetObjectETagAsync(bucketName, key);   // 3rd call
var downloadResult = await s3Client.GetObjectAsEntityAsync(...); // 4th call
await s3Client.PutObjectAsEntityAsync(...);                      // 5th call
```

**Impact:** Up to 5 network round-trips per append operation for S3. With typical S3 latencies (20-50ms), this is 100-250ms of pure network overhead.

**Suggested Fix:**
- Cache the bucket existence check (buckets do not disappear in normal operation).
- Combine the ETag + download into a single `GetObject` call with conditional request.
- Remove the `ObjectExistsAsync` check and handle the "not found" case from the download attempt.

---

## 2. Hot Path: Table Storage "Stream Closed" Check

### MAJOR -- Full Partition Scan on Every Append

**File:** `src/ErikLieben.FA.ES.AzureStorage/Table/TableDataStore.cs:440-471`

**Description:**
`CheckStreamNotClosedAsync` queries ALL events in the partition to find the last one and check if it's a "closed" event:

```csharp
var filter = $"PartitionKey eq '{partitionKey}'";

TableEventEntity? lastEvent = null;
await foreach (var entity in tableClient.QueryAsync<TableEventEntity>(filter, ...))
{
    if (lastEvent == null || entity.EventVersion > lastEvent.EventVersion)
    {
        lastEvent = entity;
    }
}
```

This downloads every single entity in the partition, iterating through all events just to check the last one.

**Impact:** For a stream with 1000 events in Table Storage, every append scans all 1000 entities. This is O(N) in both time and RU cost, and is completely unnecessary work.

**Suggested Fix:**
1. Query with a reverse sort or use `top 1` with a descending RowKey filter:
```csharp
// RowKey is zero-padded version, so highest version = lexicographic last
var filter = $"PartitionKey eq '{partitionKey}' and EventType eq 'EventStream.Closed'";
```
2. Alternatively, adopt the same pattern as CosmosDB: cache closed stream IDs in a static `HashSet` (the `ClosedStreamCache` pattern already used in `CosmosDbDataStore.cs:35`).

---

### MAJOR -- Table Storage: `CreateIfNotExistsAsync` Called on Every Operation

**File:** `src/ErikLieben.FA.ES.AzureStorage/Table/TableDataStore.cs:486-492`

**Description:**
`GetTableClientAsync` is called on every read and write operation. When `settings.AutoCreateTable` is true, it calls `tableClient.CreateIfNotExistsAsync()` on every single operation:

```csharp
if (settings.AutoCreateTable)
{
    await tableClient.CreateIfNotExistsAsync();
}
```

**Impact:** Extra network call on every single read/write. In production, this table will always exist, so this check is pure waste after the first successful call.

**Suggested Fix:**
Cache the table client after the first successful creation:

```csharp
private TableClient? _cachedTableClient;

private async Task<TableClient> GetTableClientAsync(IObjectDocument document)
{
    if (_cachedTableClient != null) return _cachedTableClient;
    // ... create and optionally ensure exists ...
    _cachedTableClient = tableClient;
    return tableClient;
}
```

The CosmosDB data store already caches its container reference (`eventsContainer` field at line 22).

---

## 3. Hot Path: Blob Container Auto-Creation on Every Operation

### MAJOR -- Synchronous `CreateIfNotExists` on Every Blob Operation

**Files:**
- `src/ErikLieben.FA.ES.AzureStorage/Blob/BlobDataStore.cs:396`
- `src/ErikLieben.FA.ES.AzureStorage/Blob/BlobDocumentStore.cs:296-298`
- `src/ErikLieben.FA.ES.AzureStorage/Blob/BlobSnapShotStore.cs:298-303`

**Description:**
The `CreateBlobClient` method in `BlobDataStore` calls `container.CreateIfNotExists()` (synchronous!) on every operation when `autoCreateContainer` is true:

```csharp
// BlobDataStore.cs:396
if (autoCreateContainer)
{
    container.CreateIfNotExists();  // Synchronous!
}
```

Note this is the synchronous version, blocking the thread.

In `BlobDocumentStore.cs:296` and `BlobSnapShotStore.cs:300`, the same pattern appears with the async version.

**Impact:**
- The synchronous call blocks a thread pool thread on every blob operation.
- Even the async version adds an unnecessary network round-trip on every single operation.
- In production, containers always exist after initial setup.

**Suggested Fix:**
1. Change the synchronous call to async (`CreateIfNotExistsAsync`).
2. Cache container existence using a `ConcurrentDictionary<string, bool>` or similar, so the check only happens once per container name per process lifetime.

---

## 4. Hot Path: Aggregate Fold -- Unnecessary List Materialization

### MAJOR -- `events.ToList()` After `ReadAsync` Already Returns a List

**File:** `src/ErikLieben.FA.ES/Processors/Aggregate.cs:87`

**Description:**
The `Fold()` method reads events and then immediately calls `.ToList()`:

```csharp
// Line 84-87
events = await Stream.ReadAsync();
// ...
var eventsToFold = events.ToList();
```

`ReadAsync` already returns `IReadOnlyCollection<IEvent>`, and the underlying implementations (`BlobDataStore.ReadAsync`, `BaseEventStream.ReadAsync`) all materialize to `List<IEvent>` before returning. The `.ToList()` call creates a redundant copy.

**Impact:** For 1000 events, this unnecessarily allocates a second list copying all 1000 event references (~8KB wasted + GC pressure). More importantly, for the `ForEach` call on line 90, a simple `foreach` loop would suffice without any list materialization.

**Suggested Fix:**
```csharp
foreach (var e in events)
{
    Fold(e);
}
```

---

### MAJOR -- Double Materialization in `BaseEventStream.ReadAsync`

**File:** `src/ErikLieben.FA.ES/EventStream/BaseEventStream.cs:126-163`

**Description:**
The `ReadAsync` method creates a `List<IEvent>`, adds events from storage (which are already filtered), then applies several transformations that each create new lists:

```csharp
var events = new List<IEvent>();
// ... AddRange from storage ...

if (useExternalSequencer)
{
    events = [.. events.OrderBy(e => e.ExternalSequencer)];  // New list
}

if (UpCasters.Count != 0)
{
    events = TryUpcasting(events);  // May create new lists
}

var result = events.Where(e => e != null).ToList();  // Another new list (line 159)
```

The final `Where(e => e != null).ToList()` at line 159 always creates a third list, even when there are no null events (which should be the common case since the storage providers already filter nulls).

**Impact:** Three list allocations in the hot read path. For 1000 events at ~40 bytes per reference, that's ~120KB of unnecessary allocations per read.

**Suggested Fix:**
- Remove the null check or make it conditional.
- Avoid creating intermediate lists when upcasting and external sequencing are not used.
- Return the original events list directly in the common case.

---

## 5. Serialization: Double Serialization in Pre-Append Actions

### MAJOR -- Payload Serialized Twice When Pre-Append Actions Exist

**File:** `src/ErikLieben.FA.ES/EventStream/LeasedSession.cs:118-135`

**Description:**
In the `Append` method, the payload is first serialized to JSON:

```csharp
Payload = JsonSerializer.Serialize(payload, eventTypeInfo.JsonTypeInfo),  // Line 118
```

Then, if pre-append actions exist, the payload is deserialized by the action, modified, and re-serialized:

```csharp
foreach (var action in preAppendActions)
{
    @event = @event with
    {
        Payload = JsonSerializer.Serialize(action.PreAppend(payload, @event, document)(), eventTypeInfo.JsonTypeInfo),
    };
}
```

**Impact:** When pre-append actions are configured, the payload is serialized at least twice. For complex payloads (several KB), this doubles the serialization CPU cost.

**Suggested Fix:**
Defer the initial serialization until after pre-append actions have run:

```csharp
var modifiedPayload = payload;
foreach (var action in preAppendActions)
{
    modifiedPayload = action.PreAppend(modifiedPayload, @event, document)();
}
var serializedPayload = JsonSerializer.Serialize(modifiedPayload, eventTypeInfo.JsonTypeInfo);
```

---

## 6. Serialization: BlobExtensions String Allocation

### MINOR -- String Allocation from MemoryStream in `AsEntityAsync`

**File:** `src/ErikLieben.FA.ES.AzureStorage/Blob/Extensions/BlobExtensions.cs:30-35`

**Description:**
The `AsEntityAsync<T>` extension method downloads blob content to a `MemoryStream`, converts it to a string (allocating a new string), then deserializes from that string:

```csharp
using MemoryStream s = new();
await blobClient.DownloadToAsync(s, requestOptions);
var json = Encoding.UTF8.GetString(s.GetBuffer(), 0, (int)s.Length);
return (JsonSerializer.Deserialize(json, jsonTypeInfo), ComputeSha256Hash(json));
```

**Impact:** For a 1MB blob, this allocates: the MemoryStream buffer (~1MB), a UTF-8 string (~2MB due to .NET's UTF-16 internal representation), and then the deserialized object graph. The string intermediate is unnecessary since `System.Text.Json` can deserialize directly from a `Stream` or `ReadOnlySpan<byte>`.

Note: The SHA-256 hash computation over the entire JSON content is a requirement for concurrency control, so the string materialization serves that purpose. However, the hash could be computed directly from the byte buffer.

**Suggested Fix:**
Compute the hash from the raw bytes and deserialize from the stream:

```csharp
using MemoryStream s = new();
await blobClient.DownloadToAsync(s, requestOptions);
s.Position = 0;
var hash = ComputeSha256Hash(s.GetBuffer(), 0, (int)s.Length);
s.Position = 0;
var doc = await JsonSerializer.DeserializeAsync(s, jsonTypeInfo);
return (doc, hash);
```

This eliminates the intermediate string allocation entirely.

---

## 7. Session: LINQ OfType Allocation on Every Session

### MINOR -- `OfType<T>()` Called on Every Session Creation

**File:** `src/ErikLieben.FA.ES/EventStream/BaseEventStream.cs:449-458`

**Description:**
`GetSession` is called every time `Session()` is invoked. It filters the actions list using LINQ `OfType<T>()` four times:

```csharp
protected ILeasedSession GetSession(List<IAction> actions)
{
    return new LeasedSession(
        this,
        Document,
        StreamDependencies.DataStore,
        StreamDependencies.ObjectDocumentFactory,
        Notifications.OfType<IStreamDocumentChunkClosedNotification>(),
        actions.OfType<IAsyncPostCommitAction>(),
        actions.OfType<IPreAppendAction>(),
        actions.OfType<IPostReadAction>());
}
```

And then in the `LeasedSession` constructor, each of these is materialized into a list via `AddRange`.

**Impact:** Four LINQ enumerations + four list materializations per session. For high-throughput scenarios with frequent command operations, this adds up. The action lists are stable after initialization and should not need to be filtered on every session.

**Suggested Fix:**
Pre-compute and cache the filtered action lists during event stream setup, rather than filtering on every session creation.

---

## 8. Concurrency: CosmosDB Static HashSet Not Thread-Safe

### MAJOR -- `ClosedStreamCache` Uses Non-Thread-Safe `HashSet`

**File:** `src/ErikLieben.FA.ES.CosmosDb/CosmosDbDataStore.cs:35`

**Description:**
The `ClosedStreamCache` is a static `HashSet<string>` that's shared across all instances and threads:

```csharp
private static readonly HashSet<string> ClosedStreamCache = new(StringComparer.OrdinalIgnoreCase);
```

It is accessed with a `lock` for writes (lines 407-409) but without synchronization for reads (line 379):

```csharp
// Line 379 - READ without lock
if (ClosedStreamCache.Contains(streamId))

// Lines 407-409 - WRITE with lock
lock (ClosedStreamCache)
{
    ClosedStreamCache.Add(streamId);
}
```

**Impact:** `HashSet<T>.Contains` is not thread-safe when concurrent writes are happening. This can cause incorrect results, infinite loops during hash bucket traversal, or `NullReferenceException` in rare cases.

**Suggested Fix:**
Use `ConcurrentDictionary<string, byte>` or wrap all accesses (both reads and writes) in the lock:

```csharp
private static readonly ConcurrentDictionary<string, byte> ClosedStreamCache = new(StringComparer.OrdinalIgnoreCase);
```

---

## 9. Concurrency: ResilientDataStore Static List Not Thread-Safe

### MINOR -- `StatusCodeExtractors` List Accessed Without Synchronization

**File:** `src/ErikLieben.FA.ES/EventStream/ResilientDataStore.cs:217`

**Description:**
`StatusCodeExtractors` is a static `List<Func<Exception, int?>>` that can be mutated via `RegisterStatusCodeExtractor` and iterated via `GetStatusCodeFromException` without synchronization:

```csharp
private static readonly List<Func<Exception, int?>> StatusCodeExtractors = [];

public static void RegisterStatusCodeExtractor(Func<Exception, int?> extractor)
{
    StatusCodeExtractors.Add(extractor);  // No lock
}

private static int? GetStatusCodeFromException(Exception exception)
{
    foreach (var extractor in StatusCodeExtractors)  // No lock
    {
        ...
    }
}
```

**Impact:** If extractors are registered during application startup while retries are already happening (possible in dynamic DI scenarios), this can cause `InvalidOperationException` due to collection modified during enumeration.

**Suggested Fix:**
Use a `ConcurrentBag` or an immutable list pattern with `Interlocked.Exchange`.

---

## 10. Upcasting: List Reconstruction with Spread Operator

### MINOR -- Inefficient List Splicing in Upcast

**File:** `src/ErikLieben.FA.ES/EventStream/BaseEventStream.cs:185-210`

**Description:**
The `Upcast` method uses spread operators to reconstruct the entire events list when an upcast produces multiple events:

```csharp
case > 1:
{
    var nextItem = i < events.Count ? (Index)(i + 1) : (Index)(events.Count - 1);
    var prevItem = i > 0 ? (Index)(i - 1) : (Index)0;
    events = [.. events[0..prevItem], .. upcastedTo, .. events[nextItem..]];
    break;
}
```

**Impact:** Each multi-event upcast creates a completely new list by copying all elements. For streams with many upcasted events, this becomes O(N*M) where N is event count and M is the number of upcasted events. Additionally, the index calculations have boundary issues -- when `i == 0`, `prevItem` becomes 0, and `events[0..0]` is empty, which means the first event before position 0 is lost.

**Suggested Fix:**
Use `List.InsertRange()` and `List.RemoveAt()` for in-place modification, or build a new result list in a single pass.

---

## 11. Storage: Blob Document Store Double Hash Computation

### MINOR -- SHA-256 Computed Twice in `CreateAsync`

**File:** `src/ErikLieben.FA.ES.AzureStorage/Blob/BlobDocumentStore.cs:140-141`

**Description:**
After downloading the document, `ComputeSha256Hash(json)` is called twice with the same input:

```csharp
newDoc.SetHash(ComputeSha256Hash(json), ComputeSha256Hash(json));
```

**Impact:** SHA-256 hashing is CPU-intensive. For a typical document JSON of 2-5KB, each hash computation takes several microseconds. Calling it twice is wasteful.

**Suggested Fix:**
Compute once and reuse:
```csharp
var hash = ComputeSha256Hash(json);
newDoc.SetHash(hash, hash);
```

---

## 12. Benchmarks Assessment

### Existing Coverage

The benchmark suite in `benchmarks/ErikLieben.FA.ES.Benchmarks/` covers:

| Area | File | What's Measured |
|------|------|----------------|
| Event stream R/W | `Core/EventStreamBenchmarks.cs` | Append, Read, Stream (in-memory) |
| Session operations | `Core/SessionBenchmarks.cs` | Single/multi append, sequential sessions |
| Aggregate folding | `Folding/AggregateFoldBenchmarks.cs` | Fold 1-10000 events, with/without deserialization |
| JSON serialization | `Serialization/JsonEventBenchmarks.cs` | Source-gen vs reflection, small/medium/large |
| Event type registry | `Registry/EventTypeRegistryBenchmarks.cs` | Lookup performance |
| Snapshots | `Snapshots/SnapshotBenchmarks.cs` | Snapshot operations |

### Gaps

1. **No storage provider benchmarks**: All benchmarks use `InMemoryDataStore`. The critical Blob read-modify-write pattern, Table Storage round-trips, and CosmosDB RU costs are not measured.
2. **No concurrency benchmarks**: No tests for parallel session commits, concurrent reads, or lock contention.
3. **No projection benchmarks**: Projection load/save/update cycles are not measured.
4. **Limited event counts**: `EventStreamBenchmarks` only tests 1 and 10 events. The real performance degradation occurs at 100+ events per stream.
5. **No chunk vs non-chunk comparison**: The chunking feature designed to mitigate the read-modify-write cost is not benchmarked.

### Recommended Additions

```csharp
// Benchmark the read-modify-write cost at different stream sizes
[Params(10, 100, 500, 1000, 5000)]
public int ExistingEventCount { get; set; }

// Benchmark chunked vs non-chunked append
[Params(true, false)]
public bool ChunkingEnabled { get; set; }

// Benchmark concurrent sessions
[Params(1, 5, 10)]
public int ConcurrentWriters { get; set; }
```

---

## 13. Memory: New Dictionary Allocation on Every Append

### MINOR -- Empty Dictionary Created on Every Event

**File:** `src/ErikLieben.FA.ES/EventStream/LeasedSession.cs:121`

**Description:**
When no metadata is provided, a new empty `Dictionary<string, string>` is allocated:

```csharp
Metadata = metadata ?? new Dictionary<string, string>(),
```

Similarly, a new `ActionMetadata` is created at line 119:
```csharp
ActionMetadata = actionMetadata ?? new ActionMetadata(),
```

**Impact:** For high-throughput scenarios appending many events, this creates many small heap allocations that add GC pressure.

**Suggested Fix:**
Use a shared empty instance:
```csharp
private static readonly Dictionary<string, string> EmptyMetadata = new();
private static readonly ActionMetadata EmptyActionMetadata = new();
```

---

## Summary of Findings

| # | Severity | Component | Issue | Est. Impact |
|---|----------|-----------|-------|-------------|
| 1 | CRITICAL | Blob/S3 DataStore | Full document read-modify-write on every append | O(N) network + memory per append |
| 2 | CRITICAL | BlobDataStore | Double network round-trip (ExistsAsync + GetProperties) | +15-80ms per append |
| 3 | CRITICAL | S3DataStore | Up to 5 network round-trips per append | +100-250ms per append |
| 4 | MAJOR | TableDataStore | Full partition scan for "stream closed" check | O(N) per append |
| 5 | MAJOR | TableDataStore | CreateIfNotExistsAsync on every operation | +5-20ms per operation |
| 6 | MAJOR | BlobDataStore | Synchronous CreateIfNotExists on every operation | Thread blocking + extra call |
| 7 | MAJOR | Aggregate.Fold | Unnecessary .ToList() after ReadAsync | Redundant allocation |
| 8 | MAJOR | BaseEventStream.ReadAsync | Triple list materialization | ~120KB wasted per 1000 events |
| 9 | MAJOR | LeasedSession.Append | Double serialization with pre-append actions | 2x serialization cost |
| 10 | MAJOR | CosmosDbDataStore | Static HashSet not thread-safe for concurrent reads | Data corruption risk |
| 11 | MINOR | BaseEventStream.GetSession | OfType LINQ allocation on every session | 4 enumerations per session |
| 12 | MINOR | ResilientDataStore | Static List not thread-safe | Collection modification during enumeration |
| 13 | MINOR | BaseEventStream.Upcast | List reconstruction with spread operator | O(N*M) for multi-event upcasts |
| 14 | MINOR | BlobDocumentStore | Double SHA-256 hash computation | Redundant CPU work |
| 15 | MINOR | BlobExtensions | Intermediate string allocation for deserialization | ~2x memory for large blobs |
| 16 | MINOR | LeasedSession.Append | New Dictionary/ActionMetadata on every event | GC pressure |

### Priority Recommendations

1. **Immediate**: Fix the thread-safety issue in `CosmosDbDataStore.ClosedStreamCache` (#10) -- this is a correctness bug.
2. **High**: Cache container/table existence checks (#5, #6) -- easy win, eliminates a network call per operation.
3. **High**: Optimize the Table Storage "stream closed" check (#4) -- change to a targeted query or use caching.
4. **High**: Reduce network round-trips in Blob/S3 append (#2, #3) -- remove redundant ExistsAsync calls.
5. **Medium**: Address the read-modify-write pattern (#1) -- consider recommending chunking by default or exploring append blob support.
6. **Medium**: Eliminate unnecessary list materializations (#7, #8) -- straightforward code changes.
7. **Low**: Address the remaining MINOR issues for incremental improvement.
