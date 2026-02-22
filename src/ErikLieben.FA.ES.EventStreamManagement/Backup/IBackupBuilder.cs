namespace ErikLieben.FA.ES.EventStreamManagement.Backup;

/// <summary>
/// Builder for configuring backup options.
/// </summary>
public interface IBackupBuilder
{
    /// <summary>
    /// Specifies the backup provider to use.
    /// </summary>
    /// <param name="providerName">The name of the backup provider (e.g., "azure-blob", "filesystem").</param>
    /// <returns>This builder for fluent chaining.</returns>
    IBackupBuilder ToProvider(string providerName);

    /// <summary>
    /// Specifies the backup location.
    /// </summary>
    /// <param name="location">The backup location URI or path.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IBackupBuilder ToLocation(string location);

    /// <summary>
    /// Includes snapshots in the backup.
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IBackupBuilder IncludeSnapshots();

    /// <summary>
    /// Includes the object document metadata in the backup.
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IBackupBuilder IncludeObjectDocument();

    /// <summary>
    /// Includes terminated streams in the backup.
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IBackupBuilder IncludeTerminatedStreams();

    /// <summary>
    /// Enables compression of the backup.
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IBackupBuilder WithCompression();

    /// <summary>
    /// Sets the retention period for the backup.
    /// </summary>
    /// <param name="retention">How long to keep the backup.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IBackupBuilder WithRetention(TimeSpan retention);
}
