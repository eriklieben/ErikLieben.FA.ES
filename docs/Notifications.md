# Notifications

Notifications provide a mechanism to react to changes in event streams and their underlying storage. They enable scenarios like cache invalidation, replication, and archiving.

## Overview

The notification system provides three types of notifications:

| Notification Type | Interface | When Triggered |
|-------------------|-----------|----------------|
| Document Updated | `IStreamDocumentUpdatedNotification` | When the stream document metadata changes |
| Chunk Updated | `IStreamDocumentChunkUpdatedNotification` | When new events are added to a chunk |
| Chunk Closed | `IStreamDocumentChunkClosedNotification` | When a chunk is finalized and closed |

## Notification Types

### Document Updated Notification

Triggered when the stream document's metadata is updated. Use for cache invalidation or synchronization.

```csharp
public class CacheInvalidationNotification : IStreamDocumentUpdatedNotification
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheInvalidationNotification> _logger;

    public CacheInvalidationNotification(
        IDistributedCache cache,
        ILogger<CacheInvalidationNotification> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Action DocumentUpdated() => () =>
    {
        // Invalidate cache entries when the stream document changes
        _logger.LogInformation("Stream document updated, invalidating cache");
        _cache.Remove("stream-document-cache-key");
    };
}
```

### Chunk Updated Notification

Triggered when new events are written to a chunk. Use for real-time replication or event publishing.

```csharp
public class ReplicationNotification : IStreamDocumentChunkUpdatedNotification
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<ReplicationNotification> _logger;

    public ReplicationNotification(
        IEventBus eventBus,
        ILogger<ReplicationNotification> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public Action StreamDocumentChunkUpdated() => () =>
    {
        // Notify subscribers that new events are available
        _logger.LogInformation("Stream chunk updated, publishing notification");
        _eventBus.Publish(new StreamChunkUpdatedEvent());
    };
}
```

### Chunk Closed Notification

Triggered when a chunk is finalized and no more events will be added to it. Use for archiving or compaction.

```csharp
public class ArchiveNotification : IStreamDocumentChunkClosedNotification
{
    private readonly IArchiveService _archiveService;
    private readonly ILogger<ArchiveNotification> _logger;

    public ArchiveNotification(
        IArchiveService archiveService,
        ILogger<ArchiveNotification> logger)
    {
        _archiveService = archiveService;
        _logger = logger;
    }

    public Func<IEventStream, int, Task> StreamDocumentChunkClosed() =>
        async (eventStream, chunkIndex) =>
        {
            // Archive the closed chunk for long-term storage
            _logger.LogInformation(
                "Chunk {ChunkIndex} closed, archiving...", chunkIndex);

            await _archiveService.ArchiveChunkAsync(
                eventStream.ObjectId,
                chunkIndex);
        };
}
```

## Registration

### Generic Notification Interface

```csharp
// Register notifications via dependency injection
services.AddSingleton<INotification, CacheInvalidationNotification>();
services.AddSingleton<INotification, ReplicationNotification>();
services.AddSingleton<INotification, ArchiveNotification>();
```

### Specific Notification Interfaces

```csharp
// Or register specific notification types
services.AddSingleton<IStreamDocumentUpdatedNotification, CacheInvalidationNotification>();
services.AddSingleton<IStreamDocumentChunkUpdatedNotification, ReplicationNotification>();
services.AddSingleton<IStreamDocumentChunkClosedNotification, ArchiveNotification>();
```

## Use Cases

### Projection Cache Invalidation

```csharp
public class ProjectionCacheNotification : IStreamDocumentChunkUpdatedNotification
{
    private readonly IProjectionCache _projectionCache;
    private readonly string _objectName;

    public ProjectionCacheNotification(
        IProjectionCache projectionCache,
        string objectName)
    {
        _projectionCache = projectionCache;
        _objectName = objectName;
    }

    public Action StreamDocumentChunkUpdated() => () =>
    {
        // Invalidate projection caches when source events change
        _projectionCache.InvalidateForObject(_objectName);
    };
}
```

### Webhook Integration

```csharp
public class WebhookNotification : IStreamDocumentUpdatedNotification
{
    private readonly HttpClient _httpClient;
    private readonly WebhookSettings _settings;

    public WebhookNotification(
        HttpClient httpClient,
        IOptions<WebhookSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public Action DocumentUpdated() => async () =>
    {
        // Send webhook notification to external systems
        var payload = new { EventType = "StreamUpdated", Timestamp = DateTime.UtcNow };
        await _httpClient.PostAsJsonAsync(_settings.WebhookUrl, payload);
    };
}
```

## Best Practices

1. **Keep notifications fast** - They can impact event stream performance
2. **Handle failures gracefully** - Notification failures shouldn't prevent event commits
3. **Use async patterns for I/O** - Especially for external integrations
4. **Consider batching** - Group multiple notifications when possible
5. **Log notification execution** - Aids in debugging and monitoring
