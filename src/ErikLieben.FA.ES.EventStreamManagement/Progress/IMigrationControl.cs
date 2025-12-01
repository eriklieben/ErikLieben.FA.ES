namespace ErikLieben.FA.ES.EventStreamManagement.Progress;

/// <summary>
/// Provides control operations for an ongoing migration.
/// </summary>
public interface IMigrationControl
{
    /// <summary>
    /// Gets the unique identifier for this migration.
    /// </summary>
    Guid MigrationId { get; }

    /// <summary>
    /// Pauses the migration. Can be resumed later.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused migration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the migration. Cannot be resumed after cancellation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the migration to the original state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current progress of the migration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IMigrationProgress> GetProgressAsync(CancellationToken cancellationToken = default);
}
