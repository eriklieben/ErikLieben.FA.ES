namespace ErikLieben.FA.ES.EventStreamManagement.Core;

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStreamManagement.Progress;

/// <summary>
/// Main service for orchestrating event stream migrations.
/// </summary>
public interface IEventStreamMigrationService
{
    /// <summary>
    /// Starts a new migration for the specified document.
    /// </summary>
    /// <param name="document">The object document to migrate.</param>
    /// <returns>A fluent builder for configuring the migration.</returns>
    IMigrationBuilder ForDocument(IObjectDocument document);

    /// <summary>
    /// Starts a new bulk migration for multiple documents.
    /// </summary>
    /// <param name="documents">The documents to migrate.</param>
    /// <returns>A fluent builder for configuring the bulk migration.</returns>
    IMigrationBuilder ForDocuments(IEnumerable<IObjectDocument> documents);

    /// <summary>
    /// Gets all currently active migrations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of migration status information.</returns>
    Task<IEnumerable<IMigrationProgress>> GetActiveMigrationsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a specific migration.
    /// </summary>
    /// <param name="migrationId">The migration identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Migration status if found; otherwise null.</returns>
    Task<IMigrationProgress?> GetMigrationStatusAsync(
        Guid migrationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses an active migration.
    /// </summary>
    /// <param name="migrationId">The migration identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PauseMigrationAsync(
        Guid migrationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused migration.
    /// </summary>
    /// <param name="migrationId">The migration identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResumeMigrationAsync(
        Guid migrationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an active or paused migration.
    /// </summary>
    /// <param name="migrationId">The migration identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelMigrationAsync(
        Guid migrationId,
        CancellationToken cancellationToken = default);
}
