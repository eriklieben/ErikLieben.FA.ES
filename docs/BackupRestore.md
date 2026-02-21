# Backup and Restore

The `ErikLieben.FA.ES.EventStreamManagement` package provides comprehensive backup and restore capabilities for event streams, independent of migration operations.

## Overview

The backup/restore service supports:

- **Single stream backup** - Backup individual aggregates
- **Bulk backup** - Backup multiple streams concurrently
- **Restore to original** - Restore back to the original location
- **Restore to new location** - Clone streams to new IDs
- **Backup management** - List, validate, and cleanup backups

## Installation

```bash
dotnet add package ErikLieben.FA.ES.EventStreamManagement
```

## Basic Usage

### Setup

```csharp
// Register services
services.AddSingleton<IBackupProvider, AzureBlobBackupProvider>();
services.AddSingleton<IBackupRestoreService, BackupRestoreService>();

// Optionally register a backup registry for listing/querying backups
services.AddSingleton<IBackupRegistry, YourBackupRegistry>();
```

### Single Stream Backup

```csharp
public class BackupService
{
    private readonly IBackupRestoreService _backupService;

    public BackupService(IBackupRestoreService backupService)
    {
        _backupService = backupService;
    }

    public async Task<Guid> BackupOrder(string orderId)
    {
        // Create backup with default options
        var handle = await _backupService.BackupStreamAsync(
            "order",       // Object name (aggregate type)
            orderId,       // Object ID
            BackupOptions.Default);

        return handle.BackupId;
    }

    public async Task<Guid> BackupWithOptions(string orderId)
    {
        // Create backup with custom options
        var options = new BackupOptions
        {
            IncludeSnapshots = true,
            IncludeObjectDocument = true,
            EnableCompression = true,
            Retention = TimeSpan.FromDays(30),
            Description = "Manual backup before migration",
            Tags = new Dictionary<string, string>
            {
                ["reason"] = "pre-migration",
                ["operator"] = "admin"
            }
        };

        var handle = await _backupService.BackupStreamAsync("order", orderId, options);
        return handle.BackupId;
    }
}
```

### Restore from Backup

```csharp
public async Task RestoreOrder(Guid backupId)
{
    // Get the backup handle
    var handle = await _backupService.GetBackupAsync(backupId);
    if (handle == null)
    {
        throw new InvalidOperationException("Backup not found");
    }

    // Restore to original location
    await _backupService.RestoreStreamAsync(handle, RestoreOptions.Default);
}

public async Task RestoreWithOverwrite(Guid backupId)
{
    var handle = await _backupService.GetBackupAsync(backupId);

    // Restore with overwrite option
    await _backupService.RestoreStreamAsync(handle, RestoreOptions.WithOverwrite);
}

public async Task CloneOrder(Guid backupId, string newOrderId)
{
    var handle = await _backupService.GetBackupAsync(backupId);

    // Restore to a new location (clone)
    await _backupService.RestoreToNewStreamAsync(
        handle,
        newOrderId,
        RestoreOptions.Default);
}
```

## Backup Options

### BackupOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IncludeSnapshots` | bool | false | Include snapshots in backup |
| `IncludeObjectDocument` | bool | true | Include object document metadata |
| `IncludeTerminatedStreams` | bool | false | Include terminated/closed streams |
| `EnableCompression` | bool | true | Compress the backup data |
| `Location` | string? | null | Custom backup location/container |
| `Retention` | TimeSpan? | null | Retention period before cleanup |
| `Description` | string? | null | Description or reason for backup |
| `Tags` | Dictionary<string, string> | {} | Custom tags for categorization |

### Preset Options

```csharp
// Default settings
var options = BackupOptions.Default;

// Full backup (all data)
var fullOptions = BackupOptions.Full;

// Custom options
var customOptions = new BackupOptions
{
    IncludeSnapshots = true,
    EnableCompression = true,
    Retention = TimeSpan.FromDays(90),
    Tags = { ["environment"] = "production" }
};
```

## Restore Options

### RestoreOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Overwrite` | bool | false | Overwrite existing stream if it exists |
| `ValidateBeforeRestore` | bool | true | Validate backup integrity before restoring |
| `RestoreObjectDocument` | bool | true | Restore object document metadata |
| `RestoreSnapshots` | bool | true | Restore snapshots (if included in backup) |
| `Description` | string? | null | Description or reason for restore |

### Preset Options

```csharp
// Default - no overwrite
var options = RestoreOptions.Default;

// With overwrite
var overwriteOptions = RestoreOptions.WithOverwrite;
```

## Bulk Operations

### Backup Multiple Streams

```csharp
public async Task<BulkBackupResult> BackupAllOrders(IEnumerable<string> orderIds)
{
    var options = new BulkBackupOptions
    {
        EnableCompression = true,
        MaxConcurrency = 8,        // Process 8 at a time
        ContinueOnError = true,    // Continue if one fails
        OnProgress = progress =>
        {
            Console.WriteLine($"Progress: {progress.ProcessedStreams}/{progress.TotalStreams}");
        }
    };

    var result = await _backupService.BackupManyAsync(
        orderIds,
        "order",
        options);

    Console.WriteLine($"Completed: {result.SuccessCount} succeeded, {result.FailureCount} failed");

    // Handle failures
    foreach (var failure in result.FailedBackups)
    {
        Console.WriteLine($"Failed: {failure.ObjectId} - {failure.ErrorMessage}");
    }

    return result;
}
```

### Restore Multiple Backups

```csharp
public async Task<BulkRestoreResult> RestoreAllBackups(IEnumerable<IBackupHandle> handles)
{
    var options = new BulkRestoreOptions
    {
        Overwrite = true,
        MaxConcurrency = 4,
        ContinueOnError = true,
        OnProgress = progress =>
        {
            Console.WriteLine($"Restoring: {progress.ProcessedBackups}/{progress.TotalBackups}");
        }
    };

    return await _backupService.RestoreManyAsync(handles, options);
}
```

## Backup Management

### List Backups

```csharp
public async Task ListBackups()
{
    // List all backups
    var allBackups = await _backupService.ListBackupsAsync();

    // Query with filters
    var query = new BackupQuery
    {
        ObjectName = "order",
        CreatedAfter = DateTimeOffset.UtcNow.AddDays(-7),
        MaxResults = 100,
        Tags = new Dictionary<string, string> { ["environment"] = "production" }
    };

    var filteredBackups = await _backupService.ListBackupsAsync(query);

    foreach (var backup in filteredBackups)
    {
        Console.WriteLine($"{backup.BackupId}: {backup.ObjectName}/{backup.ObjectId} ({backup.EventCount} events)");
    }
}
```

### Validate Backup

```csharp
public async Task<bool> ValidateBackup(Guid backupId)
{
    var handle = await _backupService.GetBackupAsync(backupId);
    if (handle == null) return false;

    return await _backupService.ValidateBackupAsync(handle);
}
```

### Delete Backup

```csharp
public async Task DeleteBackup(Guid backupId)
{
    var handle = await _backupService.GetBackupAsync(backupId);
    if (handle != null)
    {
        await _backupService.DeleteBackupAsync(handle);
    }
}
```

### Cleanup Expired Backups

```csharp
public async Task CleanupOldBackups()
{
    var deletedCount = await _backupService.CleanupExpiredBackupsAsync();
    Console.WriteLine($"Deleted {deletedCount} expired backups");
}
```

## Progress Reporting

### Single Operation Progress

```csharp
public async Task BackupWithProgress(string objectId)
{
    var progress = new Progress<BackupProgress>(p =>
    {
        Console.WriteLine($"Backup: {p.EventsBackedUp}/{p.TotalEvents} events ({p.PercentageComplete:F1}%)");
    });

    await _backupService.BackupStreamAsync(
        "order",
        objectId,
        BackupOptions.Default,
        progress);
}

public async Task RestoreWithProgress(IBackupHandle handle)
{
    var progress = new Progress<RestoreProgress>(p =>
    {
        Console.WriteLine($"Restore: {p.EventsRestored}/{p.TotalEvents} events ({p.PercentageComplete:F1}%)");
    });

    await _backupService.RestoreStreamAsync(
        handle,
        RestoreOptions.Default,
        progress);
}
```

### Bulk Operation Progress

```csharp
var options = new BulkBackupOptions
{
    OnProgress = progress =>
    {
        Console.WriteLine(
            $"Bulk backup: {progress.ProcessedStreams}/{progress.TotalStreams} " +
            $"({progress.SuccessfulBackups} succeeded, {progress.FailedBackups} failed)");
    }
};
```

## Backup Handle

The `IBackupHandle` contains information about a backup:

```csharp
public interface IBackupHandle
{
    /// <summary>Unique identifier for this backup.</summary>
    Guid BackupId { get; }

    /// <summary>The object name (aggregate type).</summary>
    string ObjectName { get; }

    /// <summary>The object identifier.</summary>
    string ObjectId { get; }

    /// <summary>Number of events in the backup.</summary>
    int EventCount { get; }

    /// <summary>When the backup was created.</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>Location/path of the backup.</summary>
    string Location { get; }
}
```

## Azure Blob Backup Provider

The default provider stores backups in Azure Blob Storage:

```csharp
// Configure provider
services.AddSingleton<IBackupProvider, AzureBlobBackupProvider>(sp =>
{
    var blobClient = sp.GetRequiredService<BlobServiceClient>();
    return new AzureBlobBackupProvider(blobClient, "backups");
});
```

Backups are stored as:
- `backups/{objectName}/{objectId}/{backupId}.json` - Backup data
- `backups/{objectName}/{objectId}/{backupId}.meta` - Backup metadata

## Error Handling

### Single Operations

```csharp
try
{
    var handle = await _backupService.BackupStreamAsync("order", orderId);
}
catch (InvalidOperationException ex)
{
    // Stream doesn't exist or other operation error
    Console.WriteLine($"Backup failed: {ex.Message}");
}
```

### Bulk Operations

```csharp
var result = await _backupService.BackupManyAsync(orderIds, "order");

if (result.IsFullySuccessful)
{
    Console.WriteLine("All backups completed successfully");
}
else if (result.IsPartialSuccess)
{
    Console.WriteLine($"Partial success: {result.SuccessCount} of {result.TotalProcessed}");
}
else if (result.IsFullyFailed)
{
    Console.WriteLine("All backups failed");
}

// Process failures
foreach (var failure in result.FailedBackups)
{
    _logger.LogError(
        failure.Exception,
        "Backup failed for {ObjectId}: {Error}",
        failure.ObjectId,
        failure.ErrorMessage);
}
```

## Best Practices

### Do

- Set appropriate retention periods for automated cleanup
- Use tags to categorize backups (environment, reason, operator)
- Validate backups before critical restores
- Use bulk operations for multiple streams
- Monitor backup/restore progress for large operations
- Keep backups in a separate storage account for disaster recovery

### Don't

- Don't store backups indefinitely without retention policies
- Don't restore without validating backup integrity
- Don't set MaxConcurrency too high (can overwhelm storage)
- Don't ignore failed backups in bulk operations
- Don't skip backup validation for production restores

## See Also

- [Event Stream Management](EventStreamManagement.md) - Migration service
- [Live Migration](LiveMigration.md) - Live stream migration
- [Storage Providers](StorageProviders.md) - Storage configuration
