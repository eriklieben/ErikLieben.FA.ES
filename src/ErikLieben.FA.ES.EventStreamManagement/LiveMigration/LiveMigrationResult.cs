namespace ErikLieben.FA.ES.EventStreamManagement.LiveMigration;

/// <summary>
/// Result of a live migration operation.
/// </summary>
public sealed record LiveMigrationResult
{
    /// <summary>
    /// Gets a value indicating whether the migration completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the migration identifier.
    /// </summary>
    public required Guid MigrationId { get; init; }

    /// <summary>
    /// Gets the source stream identifier.
    /// </summary>
    public required string SourceStreamId { get; init; }

    /// <summary>
    /// Gets the target stream identifier.
    /// </summary>
    public required string TargetStreamId { get; init; }

    /// <summary>
    /// Gets the total number of events copied during the migration.
    /// </summary>
    public required long TotalEventsCopied { get; init; }

    /// <summary>
    /// Gets the number of catch-up iterations performed.
    /// </summary>
    public required int Iterations { get; init; }

    /// <summary>
    /// Gets the total elapsed time for the migration.
    /// </summary>
    public required TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// Gets the error message if the migration failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the exception if the migration failed.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets a value indicating whether the migration failed.
    /// </summary>
    public bool IsFailure => !Success;
}
