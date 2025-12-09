namespace ErikLieben.FA.ES.EventStreamManagement.Coordination;

/// <summary>
/// Configuration options for distributed locking.
/// </summary>
public class DistributedLockOptions : IDistributedLockOptions
{
    /// <summary>
    /// Gets or sets the maximum time to wait for acquiring the lock. Default is 30 minutes.
    /// </summary>
    public TimeSpan LockTimeoutValue { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the interval at which to send heartbeat/renewal signals. Default is 10 seconds.
    /// </summary>
    public TimeSpan HeartbeatIntervalValue { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the lock location URI.
    /// </summary>
    public string? LockLocation { get; set; }

    /// <summary>
    /// Gets or sets the lock provider name to use.
    /// </summary>
    public string? ProviderName { get; set; } = "blob-lease";

    /// <inheritdoc/>
    public IDistributedLockOptions LockTimeout(TimeSpan timeout)
    {
        LockTimeoutValue = timeout;
        return this;
    }

    /// <inheritdoc/>
    public IDistributedLockOptions HeartbeatInterval(TimeSpan interval)
    {
        HeartbeatIntervalValue = interval;
        return this;
    }

    /// <inheritdoc/>
    public IDistributedLockOptions UseLease(string lockLocation)
    {
        LockLocation = lockLocation;
        return this;
    }

    /// <inheritdoc/>
    public IDistributedLockOptions UseProvider(string providerName)
    {
        ProviderName = providerName;
        return this;
    }
}
