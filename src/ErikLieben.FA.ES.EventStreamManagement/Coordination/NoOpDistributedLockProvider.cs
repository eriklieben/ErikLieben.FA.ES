namespace ErikLieben.FA.ES.EventStreamManagement.Coordination;

/// <summary>
/// A no-op distributed lock provider for single-instance scenarios.
/// Always succeeds in acquiring locks without actual coordination.
/// </summary>
public class NoOpDistributedLockProvider : IDistributedLockProvider
{
    /// <inheritdoc/>
    public string ProviderName => "NoOp";

    /// <inheritdoc/>
    public Task<IDistributedLock?> AcquireLockAsync(
        string lockKey,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IDistributedLock?>(new NoOpDistributedLock(lockKey));
    }

    /// <inheritdoc/>
    public Task<bool> IsLockedAsync(string lockKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}

/// <summary>
/// A no-op distributed lock that does nothing.
/// </summary>
internal class NoOpDistributedLock : IDistributedLock
{
    public NoOpDistributedLock(string lockKey)
    {
        LockId = Guid.NewGuid().ToString();
        LockKey = lockKey;
    }

    public string LockId { get; }
    public string LockKey { get; }
    public DateTimeOffset AcquiredAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt => DateTimeOffset.MaxValue;
    public bool IsValid => true;

    public Task<bool> RenewAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> IsValidAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task ReleaseAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
