#pragma warning disable CS1591 // Missing XML comment - LoggerMessage source-generated methods are implementation details

namespace ErikLieben.FA.ES.AzureStorage;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging methods for AzureStorage.
/// Using LoggerMessage source generators for zero-allocation logging.
/// </summary>
public static partial class LogMessages
{
    // ===== Backup Provider =====

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Starting backup {BackupId} for {ObjectId}")]
    public static partial void BackupStarting(this ILogger logger, Guid backupId, string objectId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Backup {BackupId} completed ({Size} bytes)")]
    public static partial void BackupCompleted(this ILogger logger, Guid backupId, int size);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "Starting restore from backup {BackupId}")]
    public static partial void RestoreStarting(this ILogger logger, Guid backupId);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Restored backup {BackupId} with {EventCount} events")]
    public static partial void RestoreCompleted(this ILogger logger, Guid backupId, int eventCount);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Warning,
        Message = "Backup {BackupId} blob not found")]
    public static partial void BackupBlobNotFound(this ILogger logger, Guid backupId);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Warning,
        Message = "Backup {BackupId} has invalid structure")]
    public static partial void BackupInvalidStructure(this ILogger logger, Guid backupId);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Debug,
        Message = "Backup {BackupId} validation successful")]
    public static partial void BackupValidationSuccessful(this ILogger logger, Guid backupId);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Error,
        Message = "Error validating backup {BackupId}")]
    public static partial void BackupValidationError(this ILogger logger, Guid backupId, Exception exception);

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Information,
        Message = "Deleted backup {BackupId}")]
    public static partial void BackupDeleted(this ILogger logger, Guid backupId);

    // ===== Distributed Lock =====

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Debug,
        Message = "Renewed blob lease lock {LockKey} (LeaseId: {LeaseId})")]
    public static partial void LockRenewed(this ILogger logger, string lockKey, string leaseId);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Warning,
        Message = "Failed to renew blob lease lock {LockKey} - lock was lost")]
    public static partial void LockRenewalFailed(this ILogger logger, string lockKey, Exception exception);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Error,
        Message = "Error renewing blob lease lock {LockKey}")]
    public static partial void LockRenewalError(this ILogger logger, string lockKey, Exception exception);

    [LoggerMessage(
        EventId = 2104,
        Level = LogLevel.Information,
        Message = "Released blob lease lock {LockKey} (LeaseId: {LeaseId})")]
    public static partial void LockReleased(this ILogger logger, string lockKey, string leaseId);

    [LoggerMessage(
        EventId = 2105,
        Level = LogLevel.Debug,
        Message = "Blob lease lock {LockKey} was already released")]
    public static partial void LockAlreadyReleased(this ILogger logger, string lockKey, Exception exception);

    [LoggerMessage(
        EventId = 2106,
        Level = LogLevel.Error,
        Message = "Error releasing blob lease lock {LockKey}")]
    public static partial void LockReleaseError(this ILogger logger, string lockKey, Exception exception);

    // ===== Distributed Lock Provider =====

    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Information,
        Message = "Acquired distributed lock {LockKey} with lease ID {LeaseId}")]
    public static partial void LockAcquired(this ILogger logger, string lockKey, string leaseId);

    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Debug,
        Message = "Lock {LockKey} is held by another process, waiting... (Elapsed: {Elapsed})")]
    public static partial void LockWaiting(this ILogger logger, string lockKey, TimeSpan elapsed, Exception exception);

    [LoggerMessage(
        EventId = 2203,
        Level = LogLevel.Error,
        Message = "Error acquiring distributed lock {LockKey}")]
    public static partial void LockAcquisitionError(this ILogger logger, string lockKey, Exception exception);

    [LoggerMessage(
        EventId = 2204,
        Level = LogLevel.Warning,
        Message = "Failed to acquire distributed lock {LockKey} within timeout {Timeout}")]
    public static partial void LockAcquisitionTimeout(this ILogger logger, string lockKey, TimeSpan timeout);

    // ===== Migration Routing Table =====

    [LoggerMessage(
        EventId = 2301,
        Level = LogLevel.Warning,
        Message = "Failed to deserialize routing entry for {ObjectId}")]
    public static partial void RoutingDeserializationFailed(this ILogger logger, string objectId);

    [LoggerMessage(
        EventId = 2302,
        Level = LogLevel.Debug,
        Message = "Retrieved routing for {ObjectId}: Phase={Phase}, Old={OldStream}, New={NewStream}")]
    public static partial void RoutingRetrieved(this ILogger logger, string objectId, string phase, string oldStream, string newStream);

    [LoggerMessage(
        EventId = 2303,
        Level = LogLevel.Error,
        Message = "Error retrieving routing for {ObjectId}")]
    public static partial void RoutingRetrievalError(this ILogger logger, string objectId, Exception exception);

    [LoggerMessage(
        EventId = 2304,
        Level = LogLevel.Information,
        Message = "Set routing for {ObjectId}: Phase={Phase}, Old={OldStream}, New={NewStream}")]
    public static partial void RoutingSet(this ILogger logger, string objectId, string phase, string oldStream, string newStream);

    [LoggerMessage(
        EventId = 2305,
        Level = LogLevel.Error,
        Message = "Error setting routing for {ObjectId}")]
    public static partial void RoutingSetError(this ILogger logger, string objectId, Exception exception);

    [LoggerMessage(
        EventId = 2306,
        Level = LogLevel.Information,
        Message = "Removed routing for {ObjectId}")]
    public static partial void RoutingRemoved(this ILogger logger, string objectId);

    [LoggerMessage(
        EventId = 2307,
        Level = LogLevel.Error,
        Message = "Error removing routing for {ObjectId}")]
    public static partial void RoutingRemovalError(this ILogger logger, string objectId, Exception exception);

    [LoggerMessage(
        EventId = 2308,
        Level = LogLevel.Error,
        Message = "Error retrieving active migrations")]
    public static partial void ActiveMigrationsError(this ILogger logger, Exception exception);
}
