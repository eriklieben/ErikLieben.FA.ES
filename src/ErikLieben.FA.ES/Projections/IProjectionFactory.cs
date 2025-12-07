using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Defines the contract for factories that create and manage projection instances.
/// </summary>
/// <typeparam name="T">The projection type.</typeparam>
public interface IProjectionFactory<T> where T : Projection
{
    /// <summary>
    /// Loads the projection from storage, or creates a new instance if it doesn't exist.
    /// </summary>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <param name="blobName">Optional blob name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded or newly created projection instance.</returns>
    Task<T> GetOrCreateAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the projection to storage.
    /// </summary>
    /// <param name="projection">The projection to save.</param>
    /// <param name="blobName">Optional blob name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(
        T projection,
        string? blobName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the projection exists in storage.
    /// </summary>
    /// <param name="blobName">Optional blob name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the projection exists; otherwise, false.</returns>
    Task<bool> ExistsAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last modified timestamp of the projection.
    /// </summary>
    /// <param name="blobName">Optional blob name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The last modified timestamp, or null if the projection doesn't exist.</returns>
    Task<DateTimeOffset?> GetLastModifiedAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Non-generic interface for projection factories to enable runtime type resolution.
/// </summary>
public interface IProjectionFactory
{
    /// <summary>
    /// Gets the projection type this factory creates.
    /// </summary>
    Type ProjectionType { get; }

    /// <summary>
    /// Loads the projection from storage, or creates a new instance if it doesn't exist.
    /// </summary>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <param name="blobName">Optional blob name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded or newly created projection instance.</returns>
    Task<Projection> GetOrCreateProjectionAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the projection to storage.
    /// </summary>
    /// <param name="projection">The projection to save.</param>
    /// <param name="blobName">Optional blob name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveProjectionAsync(
        Projection projection,
        string? blobName = null,
        CancellationToken cancellationToken = default);
}
