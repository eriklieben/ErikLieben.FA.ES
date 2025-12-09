# Event Stream Management

The `ErikLieben.FA.ES.EventStreamManagement` package provides zero-downtime event stream migration and transformation capabilities for distributed event-sourced systems.

## Overview

Event Stream Management enables:
- **Zero-downtime migrations** in distributed environments
- **Event transformation (upcasting)** support
- **Distributed coordination** to prevent conflicts
- **Safety mechanisms** (backup, verification, rollback)
- **Fluent API** for easy configuration
- **AOT-compatible** (.NET 9+)

## Package Structure

### Core Package: `ErikLieben.FA.ES.EventStreamManagement`

Contains all interfaces and base implementations for migration orchestration:

```
ErikLieben.FA.ES.EventStreamManagement/
├── Core/
│   ├── IEventStreamMigrationService.cs  # Main service interface
│   ├── IMigrationBuilder.cs             # Fluent builder interface
│   ├── MigrationBuilder.cs              # Builder implementation
│   ├── MigrationContext.cs              # Migration configuration
│   ├── MigrationExecutor.cs             # Saga orchestrator
│   ├── MigrationResult.cs               # Result types
│   ├── MigrationStatus.cs               # Status enum
│   ├── MigrationStrategy.cs             # Strategy enum
│   └── MigrationException.cs            # Custom exception
├── Coordination/
│   ├── IDistributedLock.cs              # Lock interface
│   ├── IDistributedLockProvider.cs      # Lock provider interface
│   └── DistributedLockOptions.cs        # Lock configuration
├── Transformation/
│   ├── IEventTransformer.cs             # Transformer interface
│   ├── ITransformationPipeline.cs       # Pipeline interface
│   ├── TransformationPipeline.cs        # Pipeline implementation
│   ├── TransformationPipelineBuilder.cs # Builder
│   ├── FunctionTransformer.cs           # Function-based transformer
│   └── CompositeTransformer.cs          # Composite transformer
├── Backup/
│   ├── IBackupProvider.cs               # Backup provider interface
│   ├── IBackupBuilder.cs                # Builder interface
│   ├── IBackupHandle.cs                 # Handle interface
│   └── BackupBuilder.cs                 # Builder implementation
├── Progress/
│   ├── IMigrationProgress.cs            # Progress interface
│   ├── IMigrationControl.cs             # Control interface
│   ├── IProgressBuilder.cs              # Builder interface
│   ├── MigrationProgress.cs             # Progress implementation
│   ├── MigrationProgressTracker.cs      # Tracker
│   └── ProgressBuilder.cs               # Builder implementation
├── Cutover/
│   ├── MigrationPhase.cs                # Phase enum
│   ├── IMigrationRoutingTable.cs        # Routing interface
│   ├── MigrationRoutingEntry.cs         # Entry model
│   └── StreamRouting.cs                 # Routing rules
├── BookClosing/
│   ├── IBookClosingBuilder.cs           # Builder interface
│   └── BookClosingBuilder.cs            # Builder implementation
└── Verification/
    ├── IMigrationPlan.cs                # Plan interface
    ├── IVerificationResult.cs           # Result interface
    ├── IVerificationBuilder.cs          # Builder interface
    ├── MigrationPlan.cs                 # Plan implementation
    └── VerificationBuilder.cs           # Builder implementation
```

### Azure Provider Package: `ErikLieben.FA.ES.AzureStorage`

Contains Azure-specific implementations:

```
ErikLieben.FA.ES.AzureStorage/
└── Migration/
    ├── BlobLeaseDistributedLock.cs        # Azure Blob lease lock
    ├── BlobLeaseDistributedLockProvider.cs # Lock provider
    ├── BlobMigrationRoutingTable.cs        # Routing table
    ├── AzureBlobBackupProvider.cs          # Backup provider
    └── MigrationJsonContext.cs             # AOT JSON context
```

## Quick Start

### 1. Basic Migration

```csharp
// Create migration service with Azure providers
var migrationService = new EventStreamMigrationService(
    new BlobLeaseDistributedLockProvider(blobServiceClient, loggerFactory),
    loggerFactory);

// Execute a simple migration
var result = await migrationService
    .ForDocument(objectDocument)
    .WithSourceStream("object-123")
    .WithTargetStream("object-123-v2")
    .ExecuteAsync();

if (result.IsSuccess)
{
    Console.WriteLine($"Migration completed: {result.Statistics.TotalEvents} events migrated");
}
```

### 2. Migration with Transformation

```csharp
// Define a transformer for event upcasting
var transformer = new FunctionTransformer(
    canTransform: (name, version) => name == "OrderCreated" && version == 1,
    transform: async (evt, ct) =>
    {
        // Transform v1 OrderCreated to v2
        return new TransformedEvent(
            evt.EventType,
            version: 2,
            // Add new fields, rename properties, etc.
            TransformPayload(evt.Payload)
        );
    });

var result = await migrationService
    .ForDocument(objectDocument)
    .WithSourceStream("orders-123")
    .WithTargetStream("orders-123-v2")
    .WithTransformer(transformer)
    .ExecuteAsync();
```

### 3. Migration with Backup and Progress

```csharp
var result = await migrationService
    .ForDocument(objectDocument)
    .WithSourceStream("account-456")
    .WithTargetStream("account-456-v2")
    .WithBackup(backup => backup
        .WithProvider(new AzureBlobBackupProvider(blobServiceClient, logger))
        .IncludeObjectDocument()
        .WithCompression())
    .WithProgress(progress => progress
        .WithInterval(TimeSpan.FromSeconds(5))
        .OnProgress(p => Console.WriteLine($"Progress: {p.PercentComplete:F1}%"))
        .OnCompleted(p => Console.WriteLine("Migration completed!")))
    .ExecuteAsync();
```

### 4. Distributed Migration with Locking

```csharp
var result = await migrationService
    .ForDocument(objectDocument)
    .WithSourceStream("critical-789")
    .WithTargetStream("critical-789-v2")
    .WithDistributedLock(lock => lock
        .WithTimeout(TimeSpan.FromMinutes(30))
        .WithHeartbeatInterval(TimeSpan.FromSeconds(10)))
    .WithRollbackSupport()
    .ExecuteAsync();
```

## Architecture

### Migration Flow

```
Normal → DualWrite → DualRead → Cutover → BookClosed

1. Normal: All operations go to the original stream
2. DualWrite: Writes go to both streams, reads from original
3. DualRead: Writes to both, reads from new (fallback to original)
4. Cutover: Switch ObjectDocument to point to new stream
5. BookClosed: Original stream is terminated and archived
```

### Saga Pattern

The migration executes as a saga with the following steps:

1. **Acquire Lock** - Distributed lock prevents concurrent migrations
2. **Create Backup** - Optional backup before making changes
3. **Analyze Source** - Count events, calculate size
4. **Copy & Transform** - Copy events with optional transformation
5. **Verify** - Optional integrity verification
6. **Cutover** - Atomic switch to new stream
7. **Close Books** - Optional archival of old stream

### Distributed Coordination

Azure Blob Storage leases are used for distributed locking:

- **60-second leases** with automatic renewal
- **Heartbeat** every 10 seconds (configurable)
- **Routing table** stored in blob storage for phase coordination
- **Strongly consistent** for coordination safety

## Configuration Options

### MigrationStrategy

```csharp
public enum MigrationStrategy
{
    CopyAndTransform,  // Copy events to new stream with transformation
    LazyTransform,     // Transform on read (not implemented)
    InPlaceTransform   // Transform in place (not implemented)
}
```

### MigrationPhase

```csharp
public enum MigrationPhase
{
    Normal,      // No migration in progress
    DualWrite,   // Writing to both streams
    DualRead,    // Reading from new, fallback to old
    Cutover,     // Switching to new stream
    BookClosed   // Migration complete, old stream terminated
}
```

### MigrationStatus

```csharp
public enum MigrationStatus
{
    Pending,
    InProgress,
    Paused,
    Verifying,
    CuttingOver,
    Completed,
    Failed,
    Cancelled,
    RollingBack,
    RolledBack
}
```

## AOT Compatibility

The package is fully AOT-compatible:

- **No reflection** - All types are explicit
- **Source-generated JSON** - Using `JsonSerializerContext`
- **No dynamic code generation**

### User Responsibilities

Users must provide AOT-compatible serializers for their event types:

```csharp
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OrderCreated))]
[JsonSerializable(typeof(OrderShipped))]
public partial class MyEventJsonContext : JsonSerializerContext
{
}
```

## Extension Points

### Custom Lock Provider

```csharp
public class RedisDistributedLockProvider : IDistributedLockProvider
{
    public string ProviderName => "redis";

    public Task<IDistributedLock?> AcquireLockAsync(
        string lockKey, TimeSpan timeout, CancellationToken ct)
    {
        // Redis implementation
    }

    public Task<bool> IsLockedAsync(string lockKey, CancellationToken ct)
    {
        // Redis implementation
    }
}
```

### Custom Backup Provider

```csharp
public class S3BackupProvider : IBackupProvider
{
    public string ProviderName => "s3";

    public Task<IBackupHandle> BackupAsync(
        BackupContext context, IProgress<BackupProgress>? progress, CancellationToken ct)
    {
        // S3 implementation
    }

    // ... other methods
}
```

### Custom Event Transformer

```csharp
public class MyEventTransformer : IEventTransformer
{
    public bool CanTransform(string eventName, int version)
    {
        return eventName == "LegacyEvent" && version < 3;
    }

    public Task<IEvent> TransformAsync(IEvent sourceEvent, CancellationToken ct)
    {
        // Transformation logic
    }
}
```

## Best Practices

1. **Always use dry-run first** to validate the migration plan
2. **Enable backups** for production migrations
3. **Set appropriate lock timeouts** based on expected migration duration
4. **Monitor progress** using callbacks or external monitoring
5. **Test transformers** thoroughly before production use
6. **Use distributed locking** when multiple instances may run migrations

## Troubleshooting

### Lock Acquisition Failed

**Cause:** Another migration is in progress or a stale lock exists.

**Solution:**
- Wait for the lock to expire (60 seconds)
- Check for zombie processes
- Manually delete the lock blob if safe

### Transformation Failures

**Cause:** Transformer cannot handle the event format.

**Solution:**
- Check `CanTransform` predicate
- Verify event schema matches expected format
- Enable `FailFast` to stop on first error

### Progress Not Updating

**Cause:** Progress interval too long or callback not registered.

**Solution:**
- Reduce `ReportInterval`
- Verify `OnProgress` callback is set
- Check for exceptions in callback

## Future Enhancements

- Progressive rollout (canary migrations)
- Bulk migration support
- Additional backup providers (S3, Cosmos DB)
- Additional lock providers (Redis, Table Storage)
- Migration scheduling
- Web UI for monitoring
