using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Snapshots;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES;

/// <summary>
/// Provides storage and retrieval operations for aggregate snapshots.
/// </summary>
public interface ISnapShotStore
{
    /// <summary>
    /// Retrieves a strongly-typed snapshot at the specified version.
    /// </summary>
    /// <typeparam name="T">The type of the snapshot.</typeparam>
    /// <param name="jsonTypeInfo">The JSON type information for deserialization.</param>
    /// <param name="document">The object document identifying the stream.</param>
    /// <param name="version">The version of the snapshot to retrieve.</param>
    /// <param name="name">Optional name for the snapshot.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The snapshot object, or null if not found.</returns>
    Task<T?> GetAsync<T>(JsonTypeInfo<T> jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default) where T : class, IBase;

    /// <summary>
    /// Retrieves a snapshot as an object at the specified version.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type information for deserialization.</param>
    /// <param name="document">The object document identifying the stream.</param>
    /// <param name="version">The version of the snapshot to retrieve.</param>
    /// <param name="name">Optional name for the snapshot.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The snapshot object, or null if not found.</returns>
    Task<object?> GetAsync(JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a snapshot at the specified version.
    /// </summary>
    /// <param name="object">The object to snapshot.</param>
    /// <param name="jsonTypeInfo">The JSON type information for serialization.</param>
    /// <param name="document">The object document identifying the stream.</param>
    /// <param name="version">The version at which to store the snapshot.</param>
    /// <param name="name">Optional name for the snapshot.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetAsync(IBase @object, JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all snapshots for a stream, ordered by version descending.
    /// </summary>
    /// <param name="document">The object document identifying the stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of snapshot metadata.</returns>
    Task<IReadOnlyList<SnapshotMetadata>> ListSnapshotsAsync(
        IObjectDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a snapshot at the specified version.
    /// </summary>
    /// <param name="document">The object document identifying the stream.</param>
    /// <param name="version">The version of the snapshot to delete.</param>
    /// <param name="name">Optional name of the snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the snapshot was deleted, false if it didn't exist.</returns>
    Task<bool> DeleteAsync(
        IObjectDocument document,
        int version,
        string? name = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes multiple snapshots by version.
    /// </summary>
    /// <param name="document">The object document identifying the stream.</param>
    /// <param name="versions">The versions of snapshots to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of snapshots deleted.</returns>
    Task<int> DeleteManyAsync(
        IObjectDocument document,
        IEnumerable<int> versions,
        CancellationToken cancellationToken = default);
}