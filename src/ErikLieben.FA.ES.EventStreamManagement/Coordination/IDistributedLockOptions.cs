namespace ErikLieben.FA.ES.EventStreamManagement.Coordination;

/// <summary>
/// Configuration options for distributed locking during migration.
/// </summary>
public interface IDistributedLockOptions
{
    /// <summary>
    /// Sets the maximum time to wait for acquiring the lock.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>This options object for fluent chaining.</returns>
    IDistributedLockOptions LockTimeout(TimeSpan timeout);

    /// <summary>
    /// Sets the interval at which to send heartbeat/renewal signals.
    /// </summary>
    /// <param name="interval">The heartbeat interval.</param>
    /// <returns>This options object for fluent chaining.</returns>
    IDistributedLockOptions HeartbeatInterval(TimeSpan interval);

    /// <summary>
    /// Sets the lock provider and location to use for the distributed lock.
    /// </summary>
    /// <param name="lockLocation">The lock location URI (e.g., "blob://locks/migration-{objectId}").</param>
    /// <returns>This options object for fluent chaining.</returns>
    IDistributedLockOptions UseLease(string lockLocation);

    /// <summary>
    /// Sets a custom lock provider.
    /// </summary>
    /// <param name="providerName">The name of the lock provider to use.</param>
    /// <returns>This options object for fluent chaining.</returns>
    IDistributedLockOptions UseProvider(string providerName);
}
