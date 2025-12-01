namespace ErikLieben.FA.ES.EventStreamManagement.Coordination;

/// <summary>
/// Provides distributed locking capabilities for coordinating migrations across multiple instances.
/// </summary>
public interface IDistributedLockProvider
{
    /// <summary>
    /// Gets the name of this lock provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Attempts to acquire a distributed lock.
    /// </summary>
    /// <param name="lockKey">The unique key identifying the lock.</param>
    /// <param name="timeout">Maximum time to wait for acquiring the lock.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The acquired lock if successful; otherwise null.</returns>
    Task<IDistributedLock?> AcquireLockAsync(
        string lockKey,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a lock with the specified key is currently held.
    /// </summary>
    /// <param name="lockKey">The lock key to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the lock is currently held; otherwise false.</returns>
    Task<bool> IsLockedAsync(string lockKey, CancellationToken cancellationToken = default);
}
