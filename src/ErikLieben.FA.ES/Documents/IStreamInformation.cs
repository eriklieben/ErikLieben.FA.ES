using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Defines metadata describing where and how to persist/read an event stream for an object.
/// </summary>
/// <remarks>
/// Includes connection names for different backends, stream identifiers, chunking configuration,
/// and snapshot metadata.
/// </remarks>
[JsonDerivedType(typeof(StreamInformation), typeDiscriminator: nameof(StreamInformation))]
public interface IStreamInformation
{
    /// <summary>
    /// Gets or sets chunking configuration; null when chunking is disabled.
    /// </summary>
    StreamChunkSettings? ChunkSettings { get; set; }

    /// <summary>
    /// Gets or sets the connection name used for document tagging operations.
    /// </summary>
    [Obsolete("Use DocumentTagStore instead. This property will be removed in a future version.")]
    string DocumentTagConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the tag store/provider type to use for document tags (e.g., "blob").
    /// </summary>
    string DocumentTagType { get; set; }

    /// <summary>
    /// Gets or sets the connection name used for snapshot persistence.
    /// </summary>
    [Obsolete("Use SnapShotStore instead. This property will be removed in a future version.")]
    string SnapShotConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the current stream version (last appended event version).
    /// </summary>
    int CurrentStreamVersion { get; set; }

    /// <summary>
    /// Gets or sets the collection of snapshots associated with the stream.
    /// </summary>
    List<StreamSnapShot> SnapShots { get; set; }

    /// <summary>
    /// Gets or sets the chunk descriptors when chunking is enabled.
    /// </summary>
    List<StreamChunk> StreamChunks { get; set; }

    /// <summary>
    /// Gets or sets the connection name used to access the event stream backend.
    /// </summary>
    [Obsolete("Use DataStore instead. This property will be removed in a future version.")]
    string StreamConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the unique stream identifier for the object.
    /// </summary>
    string StreamIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the connection name used for stream tags.
    /// </summary>
    [Obsolete("Use StreamTagStore instead. This property will be removed in a future version.")]
    string StreamTagConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the stream provider type (e.g., "blob").
    /// </summary>
    string StreamType { get; set; }

    /// <summary>
    /// Gets or sets the document provider type (e.g., "blob", "cosmos").
    /// </summary>
    string DocumentType { get; set; }

    /// <summary>
    /// Gets or sets the event stream tag provider type.
    /// </summary>
    string EventStreamTagType { get; set; }

    /// <summary>
    /// Gets or sets the document reference provider type.
    /// </summary>
    string DocumentRefType { get; set; }

    /// <summary>
    /// Gets or sets the named connection for event stream data storage.
    /// </summary>
    string DataStore { get; set; }

    /// <summary>
    /// Gets or sets the named connection for document storage.
    /// </summary>
    string DocumentStore { get; set; }

    /// <summary>
    /// Gets or sets the connection name used for document persistence.
    /// </summary>
    [Obsolete("Use DocumentStore instead. This property will be removed in a future version.")]
    string DocumentConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the named connection for document tag storage.
    /// </summary>
    string DocumentTagStore { get; set; }

    /// <summary>
    /// Gets or sets the named connection for stream tag storage.
    /// </summary>
    string StreamTagStore { get; set; }

    /// <summary>
    /// Gets or sets the named connection for snapshot storage.
    /// </summary>
    string SnapShotStore { get; set; }

    /// <summary>
    /// Gets a value indicating whether chunking is enabled.
    /// </summary>
    bool ChunkingEnabled();

    /// <summary>
    /// Gets a value indicating whether the stream has snapshots.
    /// </summary>
    bool HasSnapShots();
}
