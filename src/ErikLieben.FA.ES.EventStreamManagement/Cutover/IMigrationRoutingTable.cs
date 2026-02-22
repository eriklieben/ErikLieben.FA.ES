namespace ErikLieben.FA.ES.EventStreamManagement.Cutover;

/// <summary>
/// Manages routing information for ongoing migrations to coordinate distributed read/write operations.
/// </summary>
public interface IMigrationRoutingTable
{
    /// <summary>
    /// Gets the current migration phase for an object.
    /// </summary>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current migration phase, or Normal if no migration is active.</returns>
    Task<MigrationPhase> GetPhaseAsync(string objectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the complete routing information for an object.
    /// </summary>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stream routing configuration.</returns>
    Task<StreamRouting> GetRoutingAsync(string objectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the migration phase and routing for an object.
    /// </summary>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="phase">The migration phase to set.</param>
    /// <param name="oldStream">The old stream identifier.</param>
    /// <param name="newStream">The new stream identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetMigrationPhaseAsync(
        string objectId,
        MigrationPhase phase,
        string oldStream,
        string newStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes routing information when migration completes or is cancelled.
    /// </summary>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveRoutingAsync(string objectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active migrations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of object IDs with active migrations.</returns>
    Task<IReadOnlyList<string>> GetActiveMigrationsAsync(CancellationToken cancellationToken = default);
}
