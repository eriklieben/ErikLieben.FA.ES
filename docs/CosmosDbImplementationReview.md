# CosmosDB Implementation Deep Review

**Date:** 2026-01-07
**Reviewer:** Claude (AI Assistant)
**Scope:** ErikLieben.FA.ES.CosmosDb library

## Executive Summary

A critical bug was discovered where events stored in CosmosDB couldn't be read back because queries used `c._type = 'event'` but the actual serialized property name was `type`. This bug wasn't caught by the test suite due to fundamental gaps in test coverage around serialization validation.

This document provides a comprehensive review of the CosmosDB implementation, identifies why the bug wasn't caught, and provides recommendations for improvements.

---

## Critical Issue #1: Serialization Configuration Mismatch

### Root Cause

The demo application (`Program.cs:186-192`) configures CosmosDB with:

```csharp
builder.AddAzureCosmosClient("CosmosDb", configureClientOptions: options =>
{
    options.SerializerOptions = new CosmosSerializationOptions
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    };
});
```

This uses the **Cosmos SDK's built-in serializer** (Newtonsoft.Json-based), which:
- Applies camelCase conversion to C# property names
- **IGNORES** `[JsonPropertyName("_type")]` attributes (these are System.Text.Json specific)
- Result: The C# property `Type` serializes to `type`, not `_type`

### Entity Definition vs Actual Behavior

**CosmosDbEventEntity.cs (line 74):**
```csharp
[JsonPropertyName("_type")]  // This attribute is IGNORED by Cosmos SDK serializer
public string Type { get; set; } = "event";
```

| Expected | Actual |
|----------|--------|
| Property serializes to `_type` | Property serializes to `type` |
| Query: `c._type = 'event'` | Query should be: `c.type = 'event'` |

### Why Tests Didn't Catch This

| Test Type | Configuration | Why It Missed the Bug |
|-----------|---------------|----------------------|
| **Unit Tests** | Mock `Container` interface | Never execute real serialization - mocks return pre-built entities |
| **Integration Tests** | `UseSystemTextJsonSerializerWithOptions` | Uses **different serializer** than production! System.Text.Json respects `[JsonPropertyName]` |
| **Both** | N/A | Never verify actual JSON property names in stored documents |

### Integration Test Fixture Configuration

**CosmosDbContainerFixture.cs (lines 44-47):**
```csharp
var cosmosClientOptions = new CosmosClientOptions
{
    ConnectionMode = ConnectionMode.Gateway,
    HttpClientFactory = () => _cosmosDbContainer.HttpClient,
    UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }
};
```

This uses `System.Text.Json` which **does respect** `[JsonPropertyName]` attributes, making tests pass while production fails.

---

## Critical Issue #2: Missing Test Categories

### Tests That Would Have Caught This Bug

#### 1. Raw Document Validation Test
```csharp
[Fact]
public async Task Should_serialize_type_discriminator_correctly()
{
    // Arrange
    var sut = new CosmosDbDataStore(_fixture.CosmosClient!, _settings);
    var objectDocument = CreateObjectDocument("test-stream-serialization");
    var jsonEvent = new JsonEvent { EventType = "TestEvent", EventVersion = 0, Payload = "{}" };

    // Act - Write event
    await sut.AppendAsync(objectDocument, jsonEvent);

    // Assert - Query raw document and verify JSON structure
    var container = _fixture.CosmosClient!.GetDatabase(_settings.DatabaseName)
        .GetContainer(_settings.EventsContainerName);

    var id = $"test-stream-serialization_{0:D20}";
    var response = await container.ReadItemStreamAsync(id, new PartitionKey("test-stream-serialization"));

    using var jsonDoc = await JsonDocument.ParseAsync(response.Content);

    // Verify the discriminator property name matches what queries expect
    Assert.True(jsonDoc.RootElement.TryGetProperty("type", out var typeProperty),
        "Expected 'type' property in serialized document");
    Assert.Equal("event", typeProperty.GetString());
}
```

#### 2. Query Validation Test
```csharp
[Fact]
public async Task Query_should_match_serialized_property_names()
{
    // Arrange
    var sut = new CosmosDbDataStore(_fixture.CosmosClient!, _settings);
    var objectDocument = CreateObjectDocument("test-stream-query");
    await sut.AppendAsync(objectDocument, new JsonEvent { EventType = "TestEvent", EventVersion = 0, Payload = "{}" });

    var container = _fixture.CosmosClient!.GetDatabase(_settings.DatabaseName)
        .GetContainer(_settings.EventsContainerName);

    // Act & Assert - Query by type discriminator should return results
    var query = new QueryDefinition("SELECT * FROM c WHERE c.type = 'event' AND c.streamId = @streamId")
        .WithParameter("@streamId", "test-stream-query");

    var iterator = container.GetItemQueryIterator<CosmosDbEventEntity>(query);
    var results = new List<CosmosDbEventEntity>();
    while (iterator.HasMoreResults)
    {
        var response = await iterator.ReadNextAsync();
        results.AddRange(response);
    }

    Assert.Single(results);
}
```

#### 3. Mixed Document Type Test
```csharp
[Fact]
public async Task Should_filter_by_type_when_container_has_mixed_documents()
{
    // This test verifies the discriminator field works correctly
    // when events, snapshots, and documents share the same container

    // Arrange - Insert event
    await dataStore.AppendAsync(document, event);

    // Arrange - Insert snapshot
    await snapshotStore.SetAsync(document, snapshot);

    // Act - Query for events only
    var events = await dataStore.ReadAsync(document);

    // Assert - Should only return events, not snapshots
    Assert.All(events, e => Assert.Equal("event", ((CosmosDbJsonEvent)e).Type));
}
```

---

## Issue #3: Inconsistent Serialization Architecture

### Current State

The codebase has multiple serialization configurations that don't align:

| Component | Serializer | Attribute System |
|-----------|------------|------------------|
| `CosmosDbJsonContext` | System.Text.Json (source-generated) | `[JsonPropertyName]` |
| Entity Classes | N/A (attributes only) | `[JsonPropertyName]` |
| Demo App CosmosClient | Cosmos SDK (Newtonsoft-based) | Ignores `[JsonPropertyName]` |
| Test Fixture CosmosClient | System.Text.Json | Respects `[JsonPropertyName]` |

### The AOT Serialization Context Is Never Used

**CosmosDbJsonContext.cs:**
```csharp
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CosmosDbEventEntity))]
// ... other types
public partial class CosmosDbJsonContext : JsonSerializerContext
{
}
```

This AOT-compatible context is defined but never passed to the Cosmos SDK, making it unused.

---

## Issue #4: RU Efficiency Opportunities

### Current Patterns and Recommendations

| Current Pattern | Issue | Recommendation | Impact |
|-----------------|-------|----------------|--------|
| `CheckStreamNotClosedAsync` runs on every append | Queries DB before each write | Cache closed status per stream, or use optimistic approach with conflict handling | Saves 1 RU per append |
| Point reads return full entity | Unnecessary data transfer for existence checks | Use `EnableContentResponseOnWrite = false` | Reduces latency |
| No composite indexes defined | Cross-partition queries are expensive | Add indexes for common patterns: `(streamId, version)`, `(objectName, objectId)` | Reduces RU for queries |
| `MaxItemCount = -1` in queries | SDK chooses page size | Tune based on average event size; consider 100-500 for typical events | More predictable memory usage |
| Individual deletes in `RemoveEventsForFailedCommitAsync` | Multiple round trips | Use transactional batch for deletes | Reduces latency |

### Recommended Index Policy

```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [
    { "path": "/streamId/?" },
    { "path": "/version/?" },
    { "path": "/type/?" },
    { "path": "/eventType/?" },
    { "path": "/objectName/?" },
    { "path": "/tagKey/?" }
  ],
  "excludedPaths": [
    { "path": "/data/*" },
    { "path": "/_etag/?" }
  ],
  "compositeIndexes": [
    [
      { "path": "/streamId", "order": "ascending" },
      { "path": "/version", "order": "ascending" }
    ]
  ]
}
```

---

## Issue #5: Debug Console.WriteLine Statements

Production code contains debug logging that should be removed or converted to proper logging:

| File | Line | Statement |
|------|------|-----------|
| `CosmosDbDataStore.cs` | 129 | `Console.WriteLine($"[COSMOSDB-DATASTORE] AppendAsync called...")` |
| `CosmosDbObjectDocumentFactory.cs` | 55 | `Console.WriteLine($"[COSMOSDB-FACTORY] CreateAsync...")` |
| `CosmosDbObjectDocumentFactory.cs` | 68 | `Console.WriteLine($"[COSMOSDB-FACTORY] GetAsync...")` |

### Recommendation

Replace with `ILogger<T>` injection:

```csharp
public class CosmosDbDataStore : IDataStore, IDataStoreRecovery
{
    private readonly ILogger<CosmosDbDataStore>? _logger;

    public CosmosDbDataStore(
        CosmosClient cosmosClient,
        EventStreamCosmosDbSettings settings,
        ILogger<CosmosDbDataStore>? logger = null)
    {
        _logger = logger;
        // ...
    }

    public async Task AppendAsync(IObjectDocument document, bool preserveTimestamp, params IEvent[] events)
    {
        _logger?.LogDebug("AppendAsync called with {EventCount} events for stream {StreamId}",
            events.Length, document.Active.StreamIdentifier);
        // ...
    }
}
```

---

## Issue #6: Missing Resilience Patterns

### Not Implemented or Tested

| Pattern | Current State | Risk |
|---------|---------------|------|
| Request timeout handling | Not implemented | Requests hang indefinitely on network issues |
| Throttling (429) retry | Not implemented | Operations fail instead of backing off |
| Transient failure retry | Not implemented | Temporary failures cause operation failures |
| Circuit breaker | Not implemented | Cascading failures possible |
| Connection pool management | Default SDK settings | May exhaust connections under load |

### Recommended Implementation

```csharp
// Using Microsoft.Extensions.Http.Resilience
builder.Services.AddCosmosClient(options =>
{
    options.CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests = 9;
    options.CosmosClientOptions.MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30);
});

// Or using Polly directly
var retryPolicy = Policy
    .Handle<CosmosException>(ex => ex.StatusCode == HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(
        retryCount: 5,
        sleepDurationProvider: (retryAttempt, exception, context) =>
        {
            var cosmosException = exception as CosmosException;
            return cosmosException?.RetryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
        });
```

---

## Strengths of Current Implementation

### Partition Key Design (Excellent)

| Container | Partition Key | Rationale |
|-----------|---------------|-----------|
| Events | `/streamId` | All events for a stream in single partition - efficient reads |
| Documents | `/objectName` | Groups by aggregate type - enables cross-object queries |
| Snapshots | `/streamId` | Colocates with events - efficient snapshot + events reads |
| Tags | `/tagKey` | Enables efficient tag-based lookups |

### Transactional Batches

Events are appended atomically using transactional batches:
- Up to `MaxBatchSize` (default: 100) events per batch
- Automatic splitting for larger batches
- Proper error handling for batch failures

### Optimistic Concurrency

Document updates use ETag-based optimistic concurrency:
- Hash field computed from stream state
- Conditional updates prevent lost writes
- Configurable via `UseOptimisticConcurrency` setting

### Test Coverage (Good for Business Logic)

| Category | Count |
|----------|-------|
| Unit test methods | ~80+ |
| Integration test methods | ~35+ |
| Test classes | 20+ |
| Data stores covered | 5 (DataStore, DocumentStore, SnapshotStore, DocumentTagStore, StreamTagStore) |

---

## Recommended Actions

### Immediate (P0)

1. **Clean up entity classes** - Either:
   - Remove misleading `[JsonPropertyName("_type")]` attributes, OR
   - Add comments explaining they're ignored by Cosmos SDK serializer

2. **Align test fixture with production** - Change test fixture to use same serializer configuration as production

3. **Add serialization contract tests** - Verify JSON property names match query expectations

### Short-term (P1)

4. **Remove Console.WriteLine statements** - Replace with proper `ILogger` usage

5. **Add query validation tests** - Ensure queries work with actual serialized data

6. **Document the serialization behavior** - Add XML docs explaining the camelCase conversion

### Medium-term (P2)

7. **Implement resilience patterns** - Add retry policies for transient failures

8. **Add RU tracking in tests** - Monitor RU consumption to catch inefficiencies

9. **Consider using System.Text.Json serializer** - For AOT compatibility and attribute consistency:
   ```csharp
   options.UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
   {
       PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
       TypeInfoResolver = CosmosDbJsonContext.Default
   };
   ```

---

## Summary of Test Coverage Gaps

| Gap Category | Missing Tests | Priority |
|--------------|---------------|----------|
| Serialization | JSON property name validation, discriminator field verification | P0 |
| Query Validation | SQL query vs serialized property alignment | P0 |
| Serializer Alignment | Test fixture uses different serializer than production | P0 |
| Concurrency | Real concurrent write conflicts (not mocked) | P1 |
| Resilience | Timeout, throttling, network failures | P1 |
| Performance | Large datasets, RU consumption tracking | P2 |
| Schema Evolution | Event upcasting in CosmosDB context | P2 |
| Mixed Documents | Multiple document types in same container/partition | P1 |

---

## Appendix: Files Reviewed

### Source Files (21 files)

**Data Stores:**
- `src/ErikLieben.FA.ES.CosmosDb/CosmosDbDataStore.cs`
- `src/ErikLieben.FA.ES.CosmosDb/CosmosDbDocumentStore.cs`
- `src/ErikLieben.FA.ES.CosmosDb/CosmosDbSnapShotStore.cs`
- `src/ErikLieben.FA.ES.CosmosDb/CosmosDbStreamTagStore.cs`
- `src/ErikLieben.FA.ES.CosmosDb/CosmosDbDocumentTagStore.cs`

**Factories:**
- `src/ErikLieben.FA.ES.CosmosDb/CosmosDbEventStreamFactory.cs`
- `src/ErikLieben.FA.ES.CosmosDb/CosmosDbObjectDocumentFactory.cs`
- `src/ErikLieben.FA.ES.CosmosDb/CosmosDbProjectionFactory.cs`
- `src/ErikLieben.FA.ES.CosmosDb/CosmosDbTagFactory.cs`
- `src/ErikLieben.FA.ES.CosmosDb/CosmosDbObjectIdProvider.cs`

**Models:**
- `src/ErikLieben.FA.ES.CosmosDb/Model/CosmosDbEventEntity.cs`
- `src/ErikLieben.FA.ES.CosmosDb/Model/CosmosDbDocumentEntity.cs`
- `src/ErikLieben.FA.ES.CosmosDb/Model/CosmosDbSnapshotEntity.cs`
- `src/ErikLieben.FA.ES.CosmosDb/Model/CosmosDbTagEntity.cs`
- `src/ErikLieben.FA.ES.CosmosDb/Model/CosmosDbJsonEvent.cs`
- `src/ErikLieben.FA.ES.CosmosDb/Model/CosmosDbJsonContext.cs`

**Configuration:**
- `src/ErikLieben.FA.ES.CosmosDb/Configuration/EventStreamCosmosDbSettings.cs`
- `src/ErikLieben.FA.ES.CosmosDb/ServiceCollectionExtensions.cs`

### Test Files (20+ files)

- `test/ErikLieben.FA.ES.CosmosDb.Tests/CosmosDbDataStoreTests.cs`
- `test/ErikLieben.FA.ES.CosmosDb.Tests/Integration/CosmosDbDataStoreIntegrationTests.cs`
- `test/ErikLieben.FA.ES.CosmosDb.Tests/Integration/CosmosDbContainerFixture.cs`
- *(and 17+ additional test files)*

### Demo Application

- `demo/src/TaskFlow.Api/Program.cs` (CosmosDB configuration at lines 186-192)
- `demo/src/TaskFlow.Domain/TaskFlow.Domain.csproj` (package references)
