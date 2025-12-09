#pragma warning disable S2139 // Exception handling - distributed locks require specific error recovery patterns

namespace ErikLieben.FA.ES.AzureStorage.Migration;

using Azure;
using Azure.Storage.Blobs.Specialized;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using Microsoft.Extensions.Logging;

/// <summary>
/// Distributed lock implementation using Azure Blob Storage leases.
/// </summary>
public class BlobLeaseDistributedLock : IDistributedLock
{
    private readonly BlobLeaseClient leaseClient;
    private readonly ILogger<BlobLeaseDistributedLock> logger;
    private readonly string leaseId;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobLeaseDistributedLock"/> class.
    /// </summary>
    public BlobLeaseDistributedLock(
        BlobLeaseClient leaseClient,
        string leaseId,
        string lockKey,
        ILogger<BlobLeaseDistributedLock> logger)
    {
        this.leaseClient = leaseClient ?? throw new ArgumentNullException(nameof(leaseClient));
        this.leaseId = leaseId ?? throw new ArgumentNullException(nameof(leaseId));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LockKey = lockKey;
        AcquiredAt = DateTimeOffset.UtcNow;
        ExpiresAt = AcquiredAt.AddSeconds(60); // Blob leases are 15-60 seconds
    }

    /// <inheritdoc/>
    public string LockId => leaseId;

    /// <inheritdoc/>
    public string LockKey { get; }

    /// <inheritdoc/>
    public DateTimeOffset AcquiredAt { get; }

    /// <inheritdoc/>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <inheritdoc/>
    public bool IsValid => !isDisposed && DateTimeOffset.UtcNow < ExpiresAt;

    /// <inheritdoc/>
    public async Task<bool> RenewAsync(CancellationToken cancellationToken = default)
    {
        if (isDisposed)
        {
            return false;
        }

        try
        {
            await leaseClient.RenewAsync(cancellationToken: cancellationToken);
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(60);

            logger.LockRenewed(LockKey, leaseId);

            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 404)
        {
            logger.LockRenewalFailed(LockKey, ex);

            return false;
        }
        catch (Exception ex)
        {
            logger.LockRenewalError(LockKey, ex);

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsValidAsync(CancellationToken cancellationToken = default)
    {
        if (isDisposed || DateTimeOffset.UtcNow >= ExpiresAt)
        {
            return false;
        }

        try
        {
            // Try to renew to check if we still hold the lease
            await leaseClient.RenewAsync(cancellationToken: cancellationToken);
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(60);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 404)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task ReleaseAsync(CancellationToken cancellationToken = default)
    {
        if (isDisposed)
        {
            return;
        }

        try
        {
            await leaseClient.ReleaseAsync(cancellationToken: cancellationToken);

            logger.LockReleased(LockKey, leaseId);
        }
        catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 404)
        {
            // Lock already released or doesn't exist - this is fine
            logger.LockAlreadyReleased(LockKey, ex);
        }
        catch (Exception ex)
        {
            logger.LockReleaseError(LockKey, ex);

            throw;
        }
        finally
        {
            isDisposed = true;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!isDisposed)
        {
            await ReleaseAsync();
        }

        GC.SuppressFinalize(this);
    }
}
