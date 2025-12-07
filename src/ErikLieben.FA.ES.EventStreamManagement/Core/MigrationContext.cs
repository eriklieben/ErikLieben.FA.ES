namespace ErikLieben.FA.ES.EventStreamManagement.Core;

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using ErikLieben.FA.ES.EventStreamManagement.Verification;

/// <summary>
/// Contains all context and configuration for a migration operation.
/// </summary>
public class MigrationContext
{
    /// <summary>
    /// Gets or sets the unique identifier for this migration.
    /// </summary>
    public Guid MigrationId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the source object document.
    /// </summary>
    public required IObjectDocument SourceDocument { get; set; }

    /// <summary>
    /// Gets or sets the source stream identifier.
    /// </summary>
    public required string SourceStreamIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the target stream identifier.
    /// </summary>
    public required string TargetStreamIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the migration strategy.
    /// </summary>
    public MigrationStrategy Strategy { get; set; } = MigrationStrategy.CopyAndTransform;

    /// <summary>
    /// Gets or sets the event transformer to use.
    /// </summary>
    public IEventTransformer? Transformer { get; set; }

    /// <summary>
    /// Gets or sets the transformation pipeline.
    /// </summary>
    public ITransformationPipeline? Pipeline { get; set; }

    /// <summary>
    /// Gets or sets distributed lock options.
    /// </summary>
    public DistributedLockOptions? LockOptions { get; set; }

    /// <summary>
    /// Gets or sets backup configuration.
    /// </summary>
    public BackupConfiguration? BackupConfig { get; set; }

    /// <summary>
    /// Gets or sets book closing configuration.
    /// </summary>
    public BookClosingConfiguration? BookClosingConfig { get; set; }

    /// <summary>
    /// Gets or sets verification configuration.
    /// </summary>
    public VerificationConfiguration? VerificationConfig { get; set; }

    /// <summary>
    /// Gets or sets progress reporting configuration.
    /// </summary>
    public ProgressConfiguration? ProgressConfig { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a dry run.
    /// </summary>
    public bool IsDryRun { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether pause support is enabled.
    /// </summary>
    public bool SupportsPause { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether rollback support is enabled.
    /// </summary>
    public bool SupportsRollback { get; set; }

    /// <summary>
    /// Gets or sets the data store to use for reading/writing events.
    /// </summary>
    public IDataStore? DataStore { get; set; }

    /// <summary>
    /// Gets or sets the document store to use for updating object documents during cutover.
    /// </summary>
    public IDocumentStore? DocumentStore { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when migration started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets custom metadata for the migration.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Configuration for backup operations.
/// </summary>
public class BackupConfiguration
{
    /// <summary>
    /// Gets or sets the backup provider name.
    /// </summary>
    public string ProviderName { get; set; } = "azure-blob";

    /// <summary>
    /// Gets or sets the backup location.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include snapshots.
    /// </summary>
    public bool IncludeSnapshots { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include the object document.
    /// </summary>
    public bool IncludeObjectDocument { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include terminated streams.
    /// </summary>
    public bool IncludeTerminatedStreams { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable compression.
    /// </summary>
    public bool EnableCompression { get; set; }

    /// <summary>
    /// Gets or sets the retention period for the backup.
    /// </summary>
    public TimeSpan? Retention { get; set; }
}

/// <summary>
/// Configuration for book closing operations.
/// </summary>
public class BookClosingConfiguration
{
    /// <summary>
    /// Gets or sets the reason for closing the book.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to create a snapshot before closing.
    /// </summary>
    public bool CreateSnapshot { get; set; }

    /// <summary>
    /// Gets or sets the archive storage location.
    /// </summary>
    public string? ArchiveLocation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to mark the stream as deleted.
    /// </summary>
    public bool MarkAsDeleted { get; set; }

    /// <summary>
    /// Gets or sets custom metadata for the terminated stream entry.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Configuration for verification operations.
/// </summary>
public class VerificationConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to compare event counts.
    /// </summary>
    public bool CompareEventCounts { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to compare checksums.
    /// </summary>
    public bool CompareChecksums { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to validate transformations.
    /// </summary>
    public bool ValidateTransformations { get; set; }

    /// <summary>
    /// Gets or sets the sample size for transformation validation.
    /// </summary>
    public int TransformationSampleSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether to verify stream integrity.
    /// </summary>
    public bool VerifyStreamIntegrity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to fail fast on first error.
    /// </summary>
    public bool FailFast { get; set; }

    /// <summary>
    /// Gets or sets custom validation functions.
    /// </summary>
    public List<(string Name, Func<VerificationContext, Task<ValidationResult>> Validator)> CustomValidations { get; set; } = new();
}

/// <summary>
/// Configuration for progress reporting.
/// </summary>
public class ProgressConfiguration
{
    /// <summary>
    /// Gets or sets the interval at which to report progress.
    /// </summary>
    public TimeSpan ReportInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the progress callback.
    /// </summary>
    public Action<IMigrationProgress>? OnProgress { get; set; }

    /// <summary>
    /// Gets or sets the completion callback.
    /// </summary>
    public Action<IMigrationProgress>? OnCompleted { get; set; }

    /// <summary>
    /// Gets or sets the failure callback.
    /// </summary>
    public Action<IMigrationProgress, Exception>? OnFailed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable logging.
    /// </summary>
    public bool EnableLogging { get; set; }

    /// <summary>
    /// Gets or sets custom metrics to track.
    /// </summary>
    public Dictionary<string, Func<object>> CustomMetrics { get; set; } = new();
}
