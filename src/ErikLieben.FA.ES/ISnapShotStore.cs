using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
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
    /// <returns>The snapshot object, or null if not found.</returns>
    Task<T?> GetAsync<T>(JsonTypeInfo<T> jsonTypeInfo, IObjectDocument document, int version, string? name = null) where T : class, IBase;

    /// <summary>
    /// Retrieves a snapshot as an object at the specified version.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type information for deserialization.</param>
    /// <param name="document">The object document identifying the stream.</param>
    /// <param name="version">The version of the snapshot to retrieve.</param>
    /// <param name="name">Optional name for the snapshot.</param>
    /// <returns>The snapshot object, or null if not found.</returns>
    Task<object?> GetAsync(JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null);

    /// <summary>
    /// Stores a snapshot at the specified version.
    /// </summary>
    /// <param name="object">The object to snapshot.</param>
    /// <param name="jsonTypeInfo">The JSON type information for serialization.</param>
    /// <param name="document">The object document identifying the stream.</param>
    /// <param name="version">The version at which to store the snapshot.</param>
    /// <param name="name">Optional name for the snapshot.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetAsync(IBase @object, JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null);
}