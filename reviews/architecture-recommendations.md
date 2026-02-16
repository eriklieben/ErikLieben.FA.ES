# Architecture Review & Design Recommendations

**Library:** ErikLieben.FA.ES Event Sourcing Framework
**Date:** 2026-02-16
**Branch:** `vnext`
**Reviewer:** Automated Architecture Review (Claude Opus 4.6)
**Input:** Performance review, security review, completeness review, v2 readiness summary, source code analysis

---

## Table of Contents

1. [Read-Modify-Write Pattern (PA1)](#1-read-modify-write-pattern-pa1)
2. [Storage Provider Benchmarks (PA2)](#2-storage-provider-benchmarks-pa2)
3. [Authentication Architecture (SA1/SA2)](#3-authentication-architecture-sa1sa2)
4. [S3 Concurrency Model (SA3)](#4-s3-concurrency-model-sa3)
5. [WebJobs.Isolated.Extensions Future (CA1)](#5-webjobsisolatedextensions-future-ca1)
6. [Public API Tracking (CA2)](#6-public-api-tracking-ca2)
7. [Provider Parity Strategy (CA3-CA5)](#7-provider-parity-strategy-ca3-ca5)
8. [S3 Integration Testing (CA6)](#8-s3-integration-testing-ca6)
9. [Newtonsoft.Json / AOT Conflict (CA7)](#9-newtonsoftjson--aot-conflict-ca7)
10. [Documentation Gap Analysis (CA8)](#10-documentation-gap-analysis-ca8)

---

## 1. Read-Modify-Write Pattern (PA1)

### Current State

Both the Blob and S3 storage providers store all events for a stream in a single JSON document. Every append operation performs a full read-modify-write cycle:

**Blob Storage** (`src/ErikLieben.FA.ES.AzureStorage/Blob/BlobDataStore.cs:230-273`):
1. `GetPropertiesAsync` to get the ETag (line 264)
2. `AsEntityAsync` to download and deserialize the entire document (line 338-340)
3. `AddRange` new events to the in-memory list (line 347)
4. `SaveEntityAsync` to re-serialize and upload the entire document (line 348-350)

**S3 Storage** (`src/ErikLieben.FA.ES.S3/S3DataStore.cs:198-315`):
1. `GetObjectAsEntityAsync` downloads the full object (line 237-240)
2. `AddRange` new events (line 301)
3. `PutObjectAsEntityAsync` to upload the full document (line 302-307)

Recent work has already improved the network round-trip situation -- the `ExistsAsync` pre-check has been removed from Blob (replaced with a direct `GetPropertiesAsync` catching 404), and S3 no longer calls `ObjectExistsAsync` or `GetObjectETagAsync` separately. These are good improvements that brought Blob from 3-4 round-trips to 2-3, and S3 from 5 to 2.

However, the fundamental issue remains: the full document is downloaded and re-uploaded on every append.

### Impact Analysis

| Stream Size (events) | Est. JSON Size | Download + Upload Bandwidth | Latency Overhead |
|----------------------|---------------|----------------------------|------------------|
| 50 | ~100 KB | ~200 KB | ~20ms |
| 500 | ~1 MB | ~2 MB | ~100ms |
| 2,000 | ~4 MB | ~8 MB | ~400ms |
| 5,000 | ~10 MB | ~20 MB | ~1,000ms+ |

The existing chunking mechanism (`LeasedSession.cs:360-389`) mitigates this by splitting streams into chunks of configurable size (default 1000 events). With chunking enabled, append operations only touch the active chunk. This is the most important mitigation already in place.

### Option A: Azure Append Blobs

Azure Append Blobs support `AppendBlock` operations that add data to the end of a blob without downloading existing content.

**Pros:**
- True O(1) append -- no download required
- Up to 50,000 append operations per blob (adequate for most streams)
- Atomic append with ETag-based concurrency

**Cons:**
- Append Blobs do not support random access writes -- events cannot be modified after append
- Maximum blob size of ~195 GB (4.75 MB per block x 50,000 blocks)
- JSON format complications: a single JSON array `[event1, event2, ...]` cannot be appended to without reading the closing bracket. Would require newline-delimited JSON (NDJSON) or a custom binary format.
- Append Blobs are not available in all regions / storage account types (e.g., not available in premium block blob storage)
- Reading still requires downloading the full blob and parsing all events
- Breaking change to storage format -- existing streams would need migration

**Migration path:** New streams could use Append Blobs while existing Block Blob streams remain readable. A migration utility could convert existing streams to the new format.

### Option B: Event-per-Blob/Object

Store each event as a separate blob or S3 object, indexed by version number.

**Pros:**
- True O(1) append: upload a single small object
- Each event is independently addressable and cacheable
- Easy version-range reads via listing with prefix
- Simplifies eventual consistency models
- No concurrency concern on append (create with `If-None-Match: *`)

**Cons:**
- Read performance degrades significantly: reading 500 events requires 500 GET operations (or listing + downloading). Even with parallelization, this is much slower than a single blob download.
- Higher storage transaction costs: 1 PUT per event vs 1 PUT per batch
- Blob/S3 listing latency for reads (pagination overhead)
- Significantly more objects to manage for retention and cleanup
- Existing chunking logic would need to be reimagined

### Option C: Hybrid Chunking Improvements

Enhance the existing chunking system without changing the storage format.

**Pros:**
- No breaking changes -- existing consumers unaffected
- Chunking already works and is tested
- The improvement is a matter of configuration and documentation

**Cons:**
- Does not eliminate the O(N) cost -- only bounds N to chunk size
- Chunk management adds complexity to the commit path

**Specific improvements:**
1. **Enable chunking by default** for all new streams. The current default is no chunking, which means users must opt in. A default chunk size of 500-1000 events would provide a good balance.
2. **Reduce default chunk size** from 1000 to 500 for Blob/S3. At ~2KB/event, a 500-event chunk is ~1MB -- a reasonable read-modify-write window.
3. **Add chunk size recommendations to documentation** based on average event payload size.
4. **Consider a `SmartChunkStrategy`** that monitors per-stream append latency and automatically adjusts chunk size.

### Recommendation

**Option C (Hybrid Chunking Improvements) for v2 stable.** This is the pragmatic choice:
- Zero breaking changes
- Immediate improvement by enabling chunking by default
- Already tested and working in the current codebase

For a future v3, consider **Option B (Event-per-Blob)** for a new "event-per-object" storage format alongside the existing format, with the `DataStore` interface abstracting the difference. This would give advanced users true O(1) append at the cost of read performance, which is acceptable for write-heavy workloads.

**Effort: Small** (enabling chunking by default and documentation). Medium for `SmartChunkStrategy`.

---

## 2. Storage Provider Benchmarks (PA2)

### Current Benchmark Coverage

The benchmark suite at `benchmarks/ErikLieben.FA.ES.Benchmarks/` covers:

| Benchmark | File | What's Measured |
|-----------|------|-----------------|
| EventStreamBenchmarks | `Core/EventStreamBenchmarks.cs` | Append, Read, Stream (in-memory) |
| SessionBenchmarks | `Core/SessionBenchmarks.cs` | Single/multi append, sequential sessions |
| AggregateFoldBenchmarks | `Folding/AggregateFoldBenchmarks.cs` | Fold 1-10000 events, with/without deserialization |
| JsonEventBenchmarks | `Serialization/JsonEventBenchmarks.cs` | Source-gen vs reflection, small/medium/large |
| EventTypeRegistryBenchmarks | `Registry/EventTypeRegistryBenchmarks.cs` | Lookup performance |
| SnapshotBenchmarks | `Snapshots/SnapshotBenchmarks.cs` | Snapshot operations |

### What's Missing

1. **No storage provider benchmarks** -- all benchmarks use `InMemoryDataStore`. The critical Blob read-modify-write pattern, Table Storage round-trips, CosmosDB RU costs, and S3 latencies are not measured.
2. **No concurrency benchmarks** -- no tests for parallel session commits, concurrent reads, or lock contention.
3. **No projection benchmarks** -- projection load/save/update cycles are not measured.
4. **No chunk vs non-chunk comparison** -- the primary mitigation for the read-modify-write cost is not benchmarked.
5. **Limited event counts** -- `EventStreamBenchmarks` only tests 1 and 10 events. Real degradation occurs at 100+ events.

### Proposed Benchmark Suite Design

```
benchmarks/ErikLieben.FA.ES.Benchmarks/
  Core/
    EventStreamBenchmarks.cs          (existing - expand event counts)
    SessionBenchmarks.cs              (existing - add concurrent sessions)
    ChunkingBenchmarks.cs             (NEW - chunk vs non-chunk at various sizes)
  Storage/
    BlobStorageBenchmarks.cs          (NEW - Azurite-backed, append/read at various sizes)
    TableStorageBenchmarks.cs         (NEW - Azurite-backed, batch insert/query)
    S3StorageBenchmarks.cs            (NEW - MinIO-backed, append/read at various sizes)
    CosmosDbStorageBenchmarks.cs      (NEW - CosmosDB emulator, batch write/query)
  Projections/
    ProjectionLoadBenchmarks.cs       (NEW - load/update/save cycle)
    ProjectionCatchUpBenchmarks.cs    (NEW - catch-up discovery throughput)
  Concurrency/
    ConcurrentSessionBenchmarks.cs    (NEW - parallel writers to same stream)
    ConcurrentReadBenchmarks.cs       (NEW - parallel readers)
```

**Key parameterization:**

```csharp
[Params(10, 100, 500, 1000, 5000)]
public int ExistingEventCount { get; set; }

[Params(true, false)]
public bool ChunkingEnabled { get; set; }

[Params(1, 5, 10)]
public int ConcurrentWriters { get; set; }
```

Storage provider benchmarks would require containerized infrastructure (Azurite for Blob/Table, MinIO for S3, CosmosDB emulator for CosmosDB). These should be separate from the in-memory benchmarks and gated behind an environment variable or BenchmarkDotNet filter so they only run when infrastructure is available.

### Recommendation

1. **Immediate:** Expand `EventStreamBenchmarks` to test at 100, 500, 1000 events. Add a `ChunkingBenchmarks.cs` comparing chunked vs non-chunked at various stream sizes using `InMemoryDataStore`.
2. **For v2:** Add `BlobStorageBenchmarks` using Azurite (available via `Aspire.Hosting.Azure.Storage` in the demo already). This is the most commonly used provider and measuring real-world append latency is critical.
3. **Post-v2:** Add remaining provider benchmarks and concurrency benchmarks.

**Effort: Small** (in-memory expansion), **Medium** (Azurite-backed Blob benchmarks), **Large** (full suite).

---

## 3. Authentication Architecture (SA1/SA2)

### Current State

The TaskFlow demo application has **zero authentication**. The security review identified:

1. **`CurrentUserMiddleware`** (`demo/src/TaskFlow.Api/Middleware/CurrentUserMiddleware.cs:23-34`) extracts user identity from the `X-Current-User` header with no validation. When absent, it defaults to the admin user.
2. **Admin endpoints** at `/api/admin` expose connection strings, storage debug info, and `ClearAllStorage` without any auth.
3. **S3 credentials** are hardcoded in `demo/src/TaskFlow.Api/Program.cs:263-271`.
4. **Connection strings** are logged at startup (line 79 logs the full `userDataConnectionString`).

This is a demo application, but the patterns established here will influence how consumers build their own applications.

### Option A: JWT-Based Auth for the Demo

Add JWT bearer authentication with a development-mode identity provider (e.g., `dotnet user-jwts` tool or a simple in-process token generator).

**Pros:**
- Demonstrates production-like authentication patterns
- Properly gates admin endpoints
- Shows how `[EventStream]` and `[Projection]` binders work with authenticated users

**Cons:**
- Significant implementation effort for a demo
- Adds infrastructure complexity (token issuer, validation config)
- Distracts from the library's event sourcing focus

### Option B: API Key Middleware

Add simple API key-based auth for admin endpoints only.

**Pros:**
- Simple to implement (~20 lines of middleware)
- Gates the most dangerous endpoints
- API key configurable via environment variable

**Cons:**
- Not representative of real auth patterns
- Still no per-user identity (X-Current-User header still spoofable)

### Option C: Development-Only Admin Restriction

Restrict admin endpoints to development mode and add prominent warnings.

```csharp
// In Program.cs
if (app.Environment.IsDevelopment())
{
    app.MapAdminEndpoints();
}
else
{
    // No admin endpoints in non-development
    app.MapGet("/api/admin/{**path}", () =>
        Results.Problem("Admin endpoints are only available in development mode."));
}
```

**Pros:**
- Minimal implementation effort
- Eliminates the most dangerous exposure (credential leakage in production)
- Does not add authentication complexity to the demo

**Cons:**
- Does not demonstrate auth patterns for library consumers
- X-Current-User header spoofing remains possible

### Recommendation

**Option C (Development-Only) for v2 stable**, combined with:

1. **Restrict admin endpoints** behind `IsDevelopment()` check -- this is the minimum viable security fix.
2. **Add a throw in `CurrentUserMiddleware`** for non-development environments:
   ```csharp
   if (!env.IsDevelopment())
       throw new InvalidOperationException("Demo auth middleware is only for development.");
   ```
3. **Move hardcoded S3 credentials** to `appsettings.Development.json` configuration.
4. **Redact connection string logging** -- log presence/absence only, never content.
5. **Add `[JsonIgnore]` to `EventStreamS3Settings.SecretKey`** at the library level to prevent accidental serialization/logging of credentials.

For documentation: add a "Security Considerations" section to the library README explaining that the demo uses development-only patterns and production applications must implement proper authentication.

**Effort: Small** (development restriction + credential cleanup). The library-level change (`[JsonIgnore]` on S3 settings) is a one-line change in `src/ErikLieben.FA.ES.S3/Configuration/EventStreamS3Settings.cs`.

---

## 4. S3 Concurrency Model (SA3)

### Current State

The S3 data store has a TOCTOU (time-of-check-time-of-use) race condition in new stream creation.

**File:** `src/ErikLieben.FA.ES.S3/S3DataStore.cs:237-277`

```csharp
var downloadResult = await s3Client.GetObjectAsEntityAsync(...);

if (downloadResult.Document == null)
{
    // Object does not exist -- create a new one
    var newDoc = new S3DataStoreDocument { ... };
    await s3Client.PutObjectAsEntityAsync(bucketName, key, newDoc, ...);
    return;
}
```

If two concurrent requests both get `downloadResult.Document == null` (the object does not yet exist), both will attempt `PutObjectAsEntityAsync`. S3's `PutObject` is a last-writer-wins operation -- the second write silently overwrites the first, and the first client's events are permanently lost.

For **existing** objects, the code passes an `etag` parameter to `PutObjectAsEntityAsync` (line 302-307), which should use `If-Match` conditional writes. However, S3 conditional write support varies by provider.

### S3 Conditional Write Support Across Providers

| Provider | `If-Match` on PutObject | `If-None-Match: *` on PutObject | Notes |
|----------|------------------------|--------------------------------|-------|
| AWS S3 | Yes (since Nov 2024) | Yes (since Nov 2024) | [S3 conditional writes](https://aws.amazon.com/about-aws/whats-new/2024/11/amazon-s3-conditional-writes/) |
| MinIO | Yes (partial) | Yes (partial) | Not all versions support conditional PutObject |
| Scaleway Object Storage | No | No | Only supports `If-None-Match` on multipart uploads |
| Backblaze B2 | No | No | S3-compatible API does not support conditional writes |
| DigitalOcean Spaces | No | No | Limited S3 API compatibility |
| Cloudflare R2 | Yes | Yes | Supports conditional writes |

### Options

**A. Application-Level Locking (DynamoDB / Table-based)**

Use an external lock provider (e.g., DynamoDB for AWS, or a separate locking service) to serialize writes to the same stream.

**Pros:** Works with any S3 provider.
**Cons:** External dependency, added latency, lock management complexity.

**B. S3 Conditional Writes (where supported)**

Use `If-None-Match: *` for new stream creation and `If-Match: <etag>` for existing stream updates.

**Pros:** No external dependencies, native S3 support.
**Cons:** Not universally supported. AWS and Cloudflare R2 support it; MinIO, Scaleway, DigitalOcean Spaces, and Backblaze B2 do not.

**C. Document-Based Locking**

Create a separate "lock" object in S3 before writing the event stream object. Use `If-None-Match: *` on the lock creation to ensure only one writer proceeds.

**Pros:** Works on most S3 providers that support `If-None-Match` on create.
**Cons:** Requires lock cleanup, adds a round-trip, risk of orphaned locks.

**D. Document Hash + Retry**

The current document hash mechanism (`doc.LastObjectDocumentHash != s3Doc.PrevHash`) provides some protection for existing streams. For new streams, add a read-after-write verification step.

**Pros:** No external dependencies, works with any S3 provider.
**Cons:** Does not prevent the race -- only detects it after the fact for existing streams. New stream race remains.

### Recommendation

**Option B with provider capability detection** for v2 stable:

1. Add a `SupportsConditionalWrites` property to `EventStreamS3Settings`:
   ```csharp
   /// <summary>
   /// When true, enables S3 conditional writes (If-Match, If-None-Match)
   /// for optimistic concurrency control. Supported by AWS S3 and Cloudflare R2.
   /// When false, falls back to document hash-based concurrency (weaker guarantees).
   /// </summary>
   public bool SupportsConditionalWrites { get; set; } = false;
   ```

2. When `SupportsConditionalWrites` is `true`, use `If-None-Match: *` for new stream creation in `S3DataStore.AppendAsync`.

3. **Document the concurrency limitations** clearly: "S3 providers without conditional write support have weaker concurrency guarantees. For production workloads requiring strict concurrency, use AWS S3, Cloudflare R2, or Azure Blob Storage."

4. Consider implementing `IDistributedLockProvider` for S3 as a future enhancement (using a DynamoDB or similar lock table).

**Effort: Small** (settings property + conditional header + documentation). Medium for `IDistributedLockProvider`.

---

## 5. WebJobs.Isolated.Extensions Future (CA1)

### Current State

**File:** `src/ErikLieben.FA.ES.WebJobs.Isolated.Extensions/ErikLieben.FA.ES.WebJobs.Isolated.Extensions.csproj`

The WebJobs package targets `net8.0` only and depends on `Microsoft.Azure.WebJobs` 3.0.44. Meanwhile:

- The core library targets `net9.0;net10.0`
- The Azure Functions isolated worker binding (`ErikLieben.FA.ES.Azure.Functions.Worker.Extensions`) already targets `net9.0;net10.0`
- The WebJobs test project targets `net9.0`, creating a framework mismatch with the `net8.0` source

### Context: Azure Functions Hosting Models

| Model | Status | SDK | Target Framework |
|-------|--------|-----|-----------------|
| In-process (WebJobs) | End of life Dec 2026 | `Microsoft.Azure.WebJobs` | net6.0-net8.0 |
| Isolated worker | Active / recommended | `Microsoft.Azure.Functions.Worker` | net8.0+ |

Microsoft has been deprecating the in-process model since 2023. The isolated worker model is now the default for all new Azure Functions projects.

### Options

**A. Deprecate (mark `[Obsolete]`) and remove from v2 release**

Mark the WebJobs package as deprecated in the next preview with a clear migration path to the isolated worker extensions.

**Pros:** Reduces maintenance burden. Eliminates the `net8.0` dependency that creates binary incompatibility with the rest of the library. Aligns with Microsoft's deprecation timeline.
**Cons:** Breaking change for any existing consumers on the in-process model.

**B. Upgrade to `net9.0;net10.0`**

Update the target framework and `Microsoft.Azure.WebJobs` dependency.

**Pros:** Maintains compatibility for in-process users.
**Cons:** `Microsoft.Azure.WebJobs` 3.0.44 targets `netstandard2.0` so it can work on any target, but the in-process hosting model itself does not support `net9.0+`. Upgrading the target framework would make the package unusable with in-process Functions hosts on `net8.0`.

**C. Maintain both: keep `net8.0` for WebJobs, add deprecation notice**

Keep the package as-is but add a clear deprecation notice and point users to the isolated worker extensions.

**Pros:** No breaking change. Existing users continue to work.
**Cons:** Framework mismatch remains. Test project mismatch needs fixing (downgrade test to `net8.0` or add multi-targeting).

### Recommendation

**Option A (Deprecate)** for v2 stable:

1. In the next preview (`2.0.0-preview.7`), add `[Obsolete("This package is deprecated. Use ErikLieben.FA.ES.Azure.Functions.Worker.Extensions instead.")]` to all public types.
2. Update the package description and README to direct users to the isolated worker extensions.
3. Fix the test project to target `net8.0` to match the source (or skip WebJobs tests if deprecating).
4. For v2 stable: consider whether to ship the WebJobs package at all. If shipped, version it separately from the main library and do not include it in the `net10.0` release.

The in-process model is approaching end of life. Any consumers still on in-process Functions should be migrating to isolated worker regardless.

**Effort: Small** (add `[Obsolete]` attributes + update documentation).

---

## 6. Public API Tracking (CA2)

### Current State

**Files:**
- `src/ErikLieben.FA.ES/PublicAPI.Shipped.txt` -- contains only `#nullable enable`
- `src/ErikLieben.FA.ES/PublicAPI.Unshipped.txt` -- contains only `#nullable enable`
- RS0016 (Symbol is not part of declared public API) is suppressed via `NoWarn` in the project file

Only the core project references `Microsoft.CodeAnalysis.PublicApiAnalyzers`. Storage providers and other packable projects do not have `PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt` files.

### Why PublicAPI Tracking Matters for v2

Without public API tracking:
- **No automatic detection of breaking changes** between previews
- **No tool-assisted changelog generation** for public surface area changes
- **Accidental API exposure** (a new public method or class) goes undetected until a consumer complains
- For a library marketed as v2 stable, consumers expect API stability guarantees

### How to Populate PublicAPI.Shipped.txt

The `Microsoft.CodeAnalysis.PublicApiAnalyzers` package provides the RS0016/RS0017 rules and a code fix that automatically adds missing members to the appropriate file.

**Steps:**

1. Remove `RS0016` from `NoWarn` in the core project's `.csproj`
2. Build the project -- the analyzer will report every public API member as RS0016
3. Apply the code fix "Add all missing members to public API" via Visual Studio or `dotnet format`
4. All current public API members will be added to `PublicAPI.Unshipped.txt`
5. Since this is the v2 "baseline", move all entries from `Unshipped` to `Shipped`

**For subsequent changes:**
- New APIs go into `Unshipped.txt` automatically
- When a preview is released, entries move from `Unshipped` to `Shipped`
- Removed APIs are flagged by RS0017

### Recommendation: Phased Rollout

**Phase 1 (preview.7):** Enable for the core library only.
1. Remove RS0016 from `NoWarn`
2. Populate `PublicAPI.Shipped.txt` with the current public surface
3. Verify the build succeeds with all entries recorded

**Phase 2 (v2 stable):** Extend to all packable projects.
1. Add `Microsoft.CodeAnalysis.PublicApiAnalyzers` to each storage provider project
2. Create `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` for each
3. Populate with current API surface

**Phase 3 (post-v2):** Integrate into CI/CD.
1. Add a CI check that fails the build if `PublicAPI.Unshipped.txt` has entries that are not in the changelog
2. Consider using [Microsoft.DotNet.ApiCompat](https://github.com/dotnet/sdk/tree/main/src/ApiCompat) for cross-assembly API compatibility checking

**Effort: Small** (Phase 1), **Medium** (Phase 2), **Small** (Phase 3 once CI is set up).

---

## 7. Provider Parity Strategy (CA3-CA5)

### Current Feature Parity Matrix

| Feature | Blob | Table | CosmosDB | S3 |
|---------|:----:|:-----:|:--------:|:--:|
| **Core Operations** | | | | |
| EventStream | Yes | Yes | Yes | Yes |
| DocumentStore | Yes | Yes | Yes | Yes |
| ObjectIdProvider | Yes | Yes | Yes | Yes |
| DocumentTagStore | Yes | Yes | Yes | Yes |
| SnapShotStore | Yes | Yes | Yes | Yes |
| ProjectionFactory | Yes | Yes | Yes | Yes |
| DataStoreRecovery | Yes | Yes | Yes | Yes |
| ReadAsStreamAsync | Yes | Yes | Yes | Yes |
| Fluent builder | Yes | Yes | Yes | Yes |
| Health checks | Yes | Yes | Yes | Yes |
| **Advanced Features** | | | | |
| IStreamMetadataProvider | Yes | **No** | **No** | Yes |
| IProjectionStatusCoordinator | Yes | Yes | Yes | **No** |
| Stream tiering | Yes | N/A | N/A | **No** |
| Multi-document projections | No | No | Yes | No |
| Migration routing table | Yes | **No** | **No** | **No** |
| Backup provider | Yes | **No** | **No** | **No** |
| Distributed lock provider | Yes | **No** | **No** | **No** |
| net10.0 support | Yes | Yes | Yes* | Yes |

\* CosmosDB now targets `net9.0;net10.0` (recently fixed in csproj).

### Which Gaps Matter Most for v2 Consumers

**Critical for v2:**
1. **IStreamMetadataProvider for Table and CosmosDB** -- Without this, `RetentionDiscoveryService` cannot evaluate retention policies for these providers. Consumers using retention features on Table or CosmosDB will get no results.

2. **IProjectionStatusCoordinator for S3** -- Without this, S3 consumers cannot use the projection rebuild lifecycle (Rebuilding -> CatchingUp -> Ready). This blocks the entire projection management story for S3.

**Important but can wait:**
3. **Migration routing for non-Blob providers** -- Live migration (cutover) only works when source and target both support routing tables. Since Blob is the most common production choice, this is less urgent.

4. **Backup for non-Blob providers** -- CosmosDB has its own backup mechanism, S3 has versioning/replication, Table Storage has geo-redundancy. Library-level backup is a convenience, not a necessity.

5. **Distributed lock for non-Blob providers** -- Only needed for live migration scenarios. CosmosDB has lease-based locking natively; S3 and Table lack a natural equivalent.

### Tiered Implementation Plan

**Tier 1 -- For v2 stable (must have):**

1. **`IStreamMetadataProvider` for Table Storage**

   Implementation approach: Query Table Storage for all events in a stream (using the partition key), count them, and extract the first and last `Timestamp` property.

   ```csharp
   // TableStreamMetadataProvider.cs
   public class TableStreamMetadataProvider : IStreamMetadataProvider
   {
       public async Task<StreamMetadata?> GetStreamMetadataAsync(
           string objectName, string objectId, CancellationToken ct)
       {
           var tableClient = await GetTableClientAsync(objectName);
           var partitionKey = objectId;
           var filter = TableClient.CreateQueryFilter(
               $"PartitionKey eq {partitionKey}");

           int count = 0;
           DateTimeOffset? oldest = null, newest = null;
           await foreach (var entity in tableClient.QueryAsync<TableEventEntity>(
               filter, select: new[] { "Timestamp" }, cancellationToken: ct))
           {
               count++;
               var ts = entity.Timestamp ?? DateTimeOffset.MinValue;
               if (oldest == null || ts < oldest) oldest = ts;
               if (newest == null || ts > newest) newest = ts;
           }

           return count > 0
               ? new StreamMetadata(objectName, objectId, count, oldest, newest)
               : null;
       }
   }
   ```

   **Effort: Small** -- follows the same pattern as `BlobStreamMetadataProvider`.

2. **`IStreamMetadataProvider` for CosmosDB**

   Implementation approach: Use a COUNT query and MIN/MAX on timestamp.

   ```csharp
   // CosmosDbStreamMetadataProvider.cs
   var query = new QueryDefinition(
       "SELECT COUNT(1) as count, MIN(c.timestamp) as oldest, MAX(c.timestamp) as newest " +
       "FROM c WHERE c.streamId = @streamId AND c._type = 'event'")
       .WithParameter("@streamId", streamId);
   ```

   **Effort: Small** -- single CosmosDB query.

3. **`IProjectionStatusCoordinator` for S3**

   Implementation approach: Store projection status as JSON objects in a dedicated S3 bucket (or prefix within the main bucket). Follow the same pattern as `BlobProjectionStatusCoordinator` but using S3 PUT/GET with ETags.

   The existing `BlobProjectionStatusCoordinator` (`src/ErikLieben.FA.ES.AzureStorage/Blob/BlobProjectionStatusCoordinator.cs`) is ~460 lines but most of it is status transition logic that can be shared. The S3 version only needs to replace blob upload/download with S3 PUT/GET.

   **Effort: Medium** -- new class, ~300 lines, plus tests and DI registration.

**Tier 2 -- For v2.1 (nice to have):**

4. Migration routing table for Table Storage and CosmosDB
5. S3 stream tiering (map S3 storage classes to the `IBlobStreamTieringService` abstraction -- create a provider-agnostic `IStreamTieringService` in core)
6. Distributed lock provider for CosmosDB (using CosmosDB lease-based locking)

**Tier 3 -- Post v2 (optional):**

7. Backup provider for CosmosDB, Table, and S3
8. Distributed lock provider for Table Storage and S3

### Recommendation

Implement Tier 1 items before v2 stable. Document the remaining gaps clearly in a "Provider Capabilities" matrix in the library documentation so consumers can make informed choices.

**Total Effort for Tier 1: Medium** (3 new classes with tests).

---

## 8. S3 Integration Testing (CA6)

### Current State

The S3 test project (`test/ErikLieben.FA.ES.S3.Tests/`) has ~20 test files that unit test against mocked `IAmazonS3` instances. There are no integration tests against a real S3-compatible service.

By comparison:
- Blob/Table tests use Azurite (via `AzuriteContainerFixture`)
- CosmosDB tests use the CosmosDB emulator (via `CosmosDbContainerFixture`)

### Proposed Approach: MinIO Testcontainers

Use [Testcontainers for .NET](https://dotnet.testcontainers.org/) with the MinIO Docker image to run S3 integration tests.

**Implementation:**

```csharp
public sealed class MinioContainerFixture : IAsyncLifetime
{
    private readonly IContainer _container;

    public string Endpoint => $"http://localhost:{_container.GetMappedPublicPort(9000)}";
    public string AccessKey => "minioadmin";
    public string SecretKey => "minioadmin";

    public MinioContainerFixture()
    {
        _container = new ContainerBuilder()
            .WithImage("minio/minio:latest")
            .WithCommand("server", "/data")
            .WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/minio/health/live")
                    .ForPort(9000)))
            .Build();
    }

    public async Task InitializeAsync() => await _container.StartAsync();
    public async Task DisposeAsync() => await _container.StopAsync();
}
```

**What to Test:**

| Test Category | Priority | What to Verify |
|--------------|----------|----------------|
| CRUD operations | P0 | Create, read, update, delete S3 documents |
| Event append | P0 | Append events, read back, verify order and content |
| Concurrency (ETag) | P0 | Two concurrent writers -- verify one gets a conflict |
| New stream race | P1 | Two concurrent creates -- verify only one succeeds (or document the behavior) |
| Bucket auto-creation | P1 | Verify `AutoCreateBucket` works correctly |
| Chunking | P2 | Verify chunked streams work end-to-end |
| Large payloads | P2 | Events with payloads >1MB |
| Snapshot store | P2 | S3 snapshot save and restore |

### CI/CD Considerations

1. **Docker requirement:** MinIO runs in Docker. Ensure CI pipeline supports Docker (GitHub Actions does by default; Azure DevOps requires a Linux agent or Docker-in-Docker).
2. **Parallel isolation:** Each test class should use its own bucket prefix to avoid cross-test interference.
3. **Conditional test execution:** Use `[Trait("Category", "Integration")]` and a CI variable to optionally skip integration tests when Docker is not available.
4. **Test duration:** MinIO container startup takes ~3-5 seconds. Use `IClassFixture<MinioContainerFixture>` to share a single container across all tests in a class.

### Recommendation

Add MinIO integration tests as a priority item for v2 stable. The S3 provider is the second-most likely choice for consumers (after Azure Blob), and having zero integration tests means the concurrency behavior, bucket creation, and end-to-end flows are not verified against a real S3 API.

Start with the P0 tests (CRUD, append/read, concurrency) -- these cover the most critical paths.

**Effort: Medium** (~1-2 days for fixture setup + P0 tests, ~1 day for P1/P2 tests).

---

## 9. Newtonsoft.Json / AOT Conflict (CA7)

### Current State

**File:** `src/ErikLieben.FA.ES.CosmosDb/ErikLieben.FA.ES.CosmosDb.csproj`

```xml
<PackageReference Include="Microsoft.Azure.Cosmos" Version="3.57.0" />
<PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonVersion)" />
```

The CosmosDB provider depends on `Microsoft.Azure.Cosmos`, which transitively depends on `Newtonsoft.Json`. The project also has an explicit `Newtonsoft.Json` reference.

The library claims AOT compatibility (`<IsAotCompatible>true</IsAotCompatible>`), and the rest of the library uses `System.Text.Json` with source-generated serializer contexts throughout. However, `Newtonsoft.Json` is not AOT-compatible -- it relies heavily on runtime reflection for serialization.

### Why the CosmosDB SDK Depends on Newtonsoft

The `Microsoft.Azure.Cosmos` SDK has historically used `Newtonsoft.Json` as its default serializer. Starting with v3.31.0, the SDK introduced `CosmosSystemTextJsonSerializer` as an alternative that uses `System.Text.Json`.

The ErikLieben.FA.ES CosmosDB provider likely uses a custom `System.Text.Json` serializer (the `CosmosDbSystemTextJsonSerializer` class, based on the completeness review's mention of it) to serialize event entities. However, the `Microsoft.Azure.Cosmos` SDK itself still uses Newtonsoft internally for:
- Request/response metadata serialization
- Change feed processing
- Some internal serialization paths

### Impact on AOT Compilation

| Component | AOT Compatible | Notes |
|-----------|---------------|-------|
| Library core (`ErikLieben.FA.ES`) | Yes | Source-generated STJ only |
| Blob provider | Yes | Source-generated STJ only |
| Table provider | Yes | Uses Azure.Data.Tables (STJ) |
| S3 provider | Yes | Source-generated STJ only |
| **CosmosDB provider** | **Partially** | Event serialization uses STJ; SDK internals use Newtonsoft |

In practice, AOT publishing with `PublishAot=true` will produce trim warnings for `Newtonsoft.Json` types used by the CosmosDB SDK. The application will likely work (Newtonsoft's core serialization paths are annotated for trimming in newer versions), but it is not guaranteed.

### Options

**A. Accept and Document**

Accept that the CosmosDB provider has a Newtonsoft dependency and document it clearly.

**Pros:** No code changes. Honest with consumers.
**Cons:** Marketing "AOT-compatible" while having a Newtonsoft dependency is misleading.

**B. Remove Explicit Newtonsoft Reference**

Remove the explicit `<PackageReference Include="Newtonsoft.Json" .../>` and rely only on the transitive dependency from `Microsoft.Azure.Cosmos`.

**Pros:** Reduces the appearance of a direct dependency. If the CosmosDB SDK moves away from Newtonsoft, the library automatically benefits.
**Cons:** Does not solve the underlying issue.

**C. Use `CosmosSystemTextJsonSerializer` and Suppress Trim Warnings**

Ensure the custom serializer is used for all library operations and add `<SuppressTrimAnalysisWarnings>` for the known Newtonsoft paths in the SDK.

**Pros:** Library code is fully STJ/AOT. Only the SDK's internal plumbing uses Newtonsoft.
**Cons:** Trim warnings are suppressed, not eliminated.

**D. Wait for Microsoft.Azure.Cosmos v4 (STJ-native)**

The CosmosDB SDK team has been working on a v4 SDK that is STJ-native. Preview packages exist.

**Pros:** Clean solution -- no Newtonsoft at all.
**Cons:** Timeline uncertain. v4 SDK may not be stable by v2 release.

### Recommendation

**Option C for v2 stable**, with a plan to adopt **Option D** when the CosmosDB SDK v4 stabilizes:

1. **Verify `CosmosSystemTextJsonSerializer` is used** for all event entity serialization (this is likely already the case).
2. **Remove the explicit `Newtonsoft.Json` PackageReference** if it is only present for the SDK's transitive dependency.
3. **Add clear documentation** in the CosmosDB provider's README: "The CosmosDB provider depends on `Microsoft.Azure.Cosmos`, which transitively includes `Newtonsoft.Json`. While the library's own serialization uses `System.Text.Json` source generators, the CosmosDB SDK's internal plumbing may produce AOT trim warnings. Full AOT support will be available when the CosmosDB SDK v4 (STJ-native) is released."
4. **Track Microsoft.Azure.Cosmos v4** (https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4436) and plan adoption.

**Effort: Small** (remove explicit reference + documentation). No code changes needed if STJ serializer is already configured.

---

## 10. Documentation Gap Analysis (CA8)

### Undocumented Features

The `CLAUDE.md` file serves as the primary AI assistant reference and contains good coverage of core patterns (aggregates, events, projections, testing, storage configuration). However, several significant features are undocumented.

| Feature | Source Files | Priority |
|---------|-------------|----------|
| **Retention policies** | `src/ErikLieben.FA.ES/Retention/` (RetentionPolicy, RetentionDiscoveryService, IRetentionPolicyProvider, IStreamMetadataProvider) | High |
| **Snapshot policies** | `src/ErikLieben.FA.ES/Snapshots/` (SnapshotPolicy, InlineSnapshotHandler, SnapshotCleanupService, ISnapshotPolicyProvider) | High |
| **Observability** | `src/ErikLieben.FA.ES/Observability/` (FaesMetrics, FaesInstrumentation, FaesSemanticConventions) | High |
| **Projection status lifecycle** | `src/ErikLieben.FA.ES/Projections/` (ProjectionStatus enum: Active/Rebuilding/CatchingUp/Ready/Failed/Disabled/Archived, IProjectionStatusCoordinator, RebuildToken, RebuildStrategy) | High |
| **Projection versioning** | `src/ErikLieben.FA.ES/Projections/` (ProjectionVersionAttribute, NeedsSchemaUpgrade, IProjectionLoader) | Medium |
| **Stream actions** | `src/ErikLieben.FA.ES/Actions/` (StreamActionAttribute, IPreAppendAction, IPostAppendAction, IPostReadAction, IAsyncPostCommitAction) | Medium |
| **Event stream management** | `src/ErikLieben.FA.ES.EventStreamManagement/` (MigrationBuilder, BulkMigrationBuilder, StreamTransformation, LiveMigration, BackupProvider, StreamRepair) | Medium |
| **Aggregate storage registry** | `src/ErikLieben.FA.ES/Aggregates/IAggregateStorageRegistry.cs` | Medium |
| **Validation** | `src/ErikLieben.FA.ES/Validation/` (AggregateValidationExtensions, CheckpointValidation, DecisionContext) | Medium |
| **Resilient data store** | `src/ErikLieben.FA.ES/EventStream/ResilientDataStore.cs` (retry policies, status code extractors) | Low |
| **Unique ID generation** | `src/ErikLieben.FA.ES/UniqueIdGenerator.cs` | Low |
| **Stream chunking configuration** | `src/ErikLieben.FA.ES/EventStream/StreamChunk.cs`, `ChunkSettings` | Medium |

### CLAUDE.md Inaccuracies

1. **Upcaster naming mismatch:** CLAUDE.md shows `IEventUpcast<TFrom, TTo>` and `[UseUpcaster<T>]`, but the actual interfaces are `IUpcastEvent` and the attribute is `[Upcaster]`. This will cause AI assistants to generate incorrect code.

2. **Result type usage context:** CLAUDE.md implies Result types are used throughout the library API, but they are actually consumer-facing utilities. The library's own APIs (`IEventStream.ReadAsync`, `IDocumentStore.GetAsync`, etc.) throw exceptions.

### Prioritized Documentation Roadmap

**Phase 1 -- v2 stable (CLAUDE.md updates):**

1. **Fix upcaster naming** -- correct `IEventUpcast` to `IUpcastEvent` and `[UseUpcaster<T>]` to `[Upcaster]`
2. **Add retention policy documentation** -- this is a key feature for production use
3. **Add snapshot policy documentation** -- closely related to retention
4. **Add observability section** -- consumers need to know how to integrate OpenTelemetry
5. **Add projection status lifecycle** -- essential for projection catch-up orchestration
6. **Add stream chunking guidance** -- critical given the read-modify-write performance implications

**Phase 2 -- post v2 stable:**

7. Add stream actions documentation
8. Add event stream management documentation (migration, transformation, backup)
9. Add validation documentation
10. Add aggregate storage registry documentation
11. Add projection versioning documentation

### CLAUDE.md vs External Documentation Strategy

The `CLAUDE.md` file is well-suited for its current purpose: giving AI assistants enough context to generate correct code. It should continue to focus on:
- Correct patterns with code examples
- Common mistakes to avoid
- File structure conventions
- Required workflow (e.g., `dotnet faes`)

For consumers, a separate documentation site (e.g., GitHub Pages with docfx or mdbook) would be more appropriate for:
- API reference (auto-generated from XML docs)
- Conceptual guides (architecture overview, getting started)
- Provider-specific guidance (choosing a provider, performance characteristics, feature matrix)
- Migration guides (upgrading from preview to stable, schema evolution)

### Recommendation

1. **Immediate:** Fix the upcaster naming mismatch in CLAUDE.md
2. **For v2 stable:** Add documentation for retention, snapshots, observability, projection lifecycle, and chunking to CLAUDE.md
3. **Post v2:** Create external documentation site with full API reference and conceptual guides

**Effort: Small** (CLAUDE.md fixes), **Medium** (Phase 1 documentation), **Large** (external documentation site).

---

## Summary: Recommended Priority Order

| Priority | Item | Section | Effort | Impact |
|----------|------|---------|--------|--------|
| 1 | Enable chunking by default + document chunking guidance | PA1 | Small | High -- immediate mitigation for read-modify-write |
| 2 | Restrict admin endpoints to development mode | SA1/SA2 | Small | High -- eliminates credential exposure risk |
| 3 | Add S3 `SupportsConditionalWrites` setting | SA3 | Small | Medium -- documents and optionally fixes concurrency gap |
| 4 | Deprecate WebJobs.Isolated.Extensions | CA1 | Small | Medium -- cleans up framework alignment |
| 5 | Enable public API tracking (core library) | CA2 | Small | High -- prevents breaking changes |
| 6 | Implement IStreamMetadataProvider for Table + CosmosDB | CA3 | Medium | High -- enables retention for all providers |
| 7 | Implement IProjectionStatusCoordinator for S3 | CA4 | Medium | High -- enables projection management for S3 |
| 8 | Add S3 integration tests with MinIO | CA6 | Medium | High -- verifies real S3 behavior |
| 9 | Fix CLAUDE.md upcaster naming + add undocumented features | CA8 | Medium | Medium -- prevents AI-generated incorrect code |
| 10 | Remove explicit Newtonsoft reference + document AOT status | CA7 | Small | Medium -- honest AOT story |
| 11 | Expand benchmark suite | PA2 | Medium | Medium -- establishes performance baselines |
| 12 | Extend public API tracking to all providers | CA2 | Medium | Medium -- full API stability guarantee |

### Estimated Total Effort

- **Small items (1-3 hours each):** Items 1, 2, 3, 4, 5, 10 = ~12 hours
- **Medium items (4-8 hours each):** Items 6, 7, 8, 9, 11, 12 = ~36 hours
- **Total for v2 stable readiness:** ~48 hours of focused work

This represents the critical path from the current preview to a v2 stable release. The library's core and Azure Blob provider are already mature -- the work is primarily about closing gaps in secondary providers, documentation, and operational hardening.
