namespace ErikLieben.FA.ES.EventStreamManagement.Coordination;

/// <summary>
/// Represents an acquired distributed lock that can be renewed and released.
/// </summary>
public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for this lock instance.
    /// </summary>
    string LockId { get; }

    /// <summary>
    /// Gets the lock key that was acquired.
    /// </summary>
    string LockKey { get; }

    /// <summary>
    /// Gets the timestamp when the lock was acquired.
    /// </summary>
    DateTimeOffset AcquiredAt { get; }

    /// <summary>
    /// Gets the timestamp when the lock expires if not renewed.
    /// </summary>
    DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets a value indicating whether the lock is still valid.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// Renews the lock lease, extending its expiration time.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if renewal succeeded; false if lock was lost.</returns>
    Task<bool> RenewAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the lock is still held and valid.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the lock is still valid; otherwise false.</returns>
    Task<bool> IsValidAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly releases the lock (also called automatically on disposal).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReleaseAsync(CancellationToken cancellationToken = default);
}
