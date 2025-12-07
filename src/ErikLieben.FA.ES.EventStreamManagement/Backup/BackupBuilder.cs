namespace ErikLieben.FA.ES.EventStreamManagement.Backup;

/// <summary>
/// Configuration for backup operations.
/// </summary>
public class BackupBuilder : IBackupBuilder
{
    private readonly Core.BackupConfiguration config = new();

    /// <inheritdoc/>
    public IBackupBuilder ToProvider(string providerName)
    {
        config.ProviderName = providerName;
        return this;
    }

    /// <inheritdoc/>
    public IBackupBuilder ToLocation(string location)
    {
        config.Location = location;
        return this;
    }

    /// <inheritdoc/>
    public IBackupBuilder IncludeSnapshots()
    {
        config.IncludeSnapshots = true;
        return this;
    }

    /// <inheritdoc/>
    public IBackupBuilder IncludeObjectDocument()
    {
        config.IncludeObjectDocument = true;
        return this;
    }

    /// <inheritdoc/>
    public IBackupBuilder IncludeTerminatedStreams()
    {
        config.IncludeTerminatedStreams = true;
        return this;
    }

    /// <inheritdoc/>
    public IBackupBuilder WithCompression()
    {
        config.EnableCompression = true;
        return this;
    }

    /// <inheritdoc/>
    public IBackupBuilder WithRetention(TimeSpan retention)
    {
        config.Retention = retention;
        return this;
    }

    /// <summary>
    /// Builds the backup configuration.
    /// </summary>
    internal Core.BackupConfiguration Build() => config;
}
