#pragma warning disable S2139 // Exception handling - distributed locks require specific error recovery patterns

namespace ErikLieben.FA.ES.AzureStorage.Migration;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

/// <summary>
/// Provides distributed locking using Azure Blob Storage leases.
/// </summary>
public class BlobLeaseDistributedLockProvider : IDistributedLockProvider
{
    private readonly BlobServiceClient blobServiceClient;
    private readonly ILogger<BlobLeaseDistributedLockProvider> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly string containerName;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobLeaseDistributedLockProvider"/> class.
    /// </summary>
    public BlobLeaseDistributedLockProvider(
        BlobServiceClient blobServiceClient,
        ILoggerFactory loggerFactory,
        string containerName = "migration-locks")
    {
        this.blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        this.logger = loggerFactory.CreateLogger<BlobLeaseDistributedLockProvider>();
        this.containerName = containerName;
    }

    /// <inheritdoc/>
    public string ProviderName => "blob-lease";

    /// <inheritdoc/>
    public async Task<IDistributedLock?> AcquireLockAsync(
        string lockKey,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lockKey);

        var stopwatch = Stopwatch.StartNew();

        // Ensure container exists
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // Create or get lock blob
        var blobName = SanitizeLockKey(lockKey);
        var blobClient = containerClient.GetBlobClient(blobName);

        // Ensure blob exists (empty blob used as lock placeholder)
        try
        {
            await blobClient.UploadAsync(
                new BinaryData(Array.Empty<byte>()),
                overwrite: false,
                cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Blob already exists - this is fine
        }

        // Try to acquire lease with retry
        var leaseClient = blobClient.GetBlobLeaseClient();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                // Azure blob leases are 15-60 seconds or infinite
                // We use 60 seconds and renew via heartbeat
                var leaseResponse = await leaseClient.AcquireAsync(
                    duration: TimeSpan.FromSeconds(60),
                    cancellationToken: cancellationToken);

                logger.LogInformation(
                    "Acquired distributed lock {LockKey} with lease ID {LeaseId}",
                    lockKey,
                    leaseResponse.Value.LeaseId);

                return new BlobLeaseDistributedLock(
                    leaseClient,
                    leaseResponse.Value.LeaseId,
                    lockKey,
                    loggerFactory.CreateLogger<BlobLeaseDistributedLock>());
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // Lease already held by someone else
                logger.LogDebug(
                    ex,
                    "Lock {LockKey} is held by another process, waiting... (Elapsed: {Elapsed})",
                    lockKey,
                    stopwatch.Elapsed);

                // Wait before retry
                var remainingTime = timeout - stopwatch.Elapsed;
                var waitTime = TimeSpan.FromSeconds(Math.Min(2, remainingTime.TotalSeconds));

                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error acquiring distributed lock {LockKey}",
                    lockKey);

                throw;
            }
        }

        logger.LogWarning(
            "Failed to acquire distributed lock {LockKey} within timeout {Timeout}",
            lockKey,
            timeout);

        return null;
    }

    /// <inheritdoc/>
    public async Task<bool> IsLockedAsync(string lockKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lockKey);

        try
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobName = SanitizeLockKey(lockKey);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return false;
            }

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            // Check if blob has an active lease
            return properties.Value.LeaseState == Azure.Storage.Blobs.Models.LeaseState.Leased;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private static string SanitizeLockKey(string lockKey)
    {
        // Replace invalid blob name characters
        return lockKey
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace(":", "-")
            .Replace("?", "-")
            .Replace("#", "-")
            .Replace("[", "-")
            .Replace("]", "-")
            .Replace("@", "-")
            + ".lock";
    }
}
