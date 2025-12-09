#pragma warning disable S1133 // Deprecated code - legacy connection name properties maintained for backwards compatibility

namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Provides concrete stream metadata including identifiers, connection names, and options such as chunking and snapshots.
/// </summary>
public class StreamInformation : IStreamInformation
{
    /// <summary>
    /// Gets or sets the unique stream identifier for the object.
    /// </summary>
    public string StreamIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream provider type (e.g., "blob").
    /// </summary>
    public string StreamType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tag store/provider type to use for document tags.
    /// </summary>
    public string DocumentTagType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current stream version (last appended event version).
    /// </summary>
    public int CurrentStreamVersion { get; set; } = -1;

    /// <summary>
    /// Gets or sets the connection name used to access the event stream backend.
    /// </summary>
    [Obsolete("Use DataStore instead. This property will be removed in a future version.")]
    public string StreamConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection name used for document tagging operations.
    /// </summary>
    [Obsolete("Use DocumentTagStore instead. This property will be removed in a future version.")]
    public string DocumentTagConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection name used for stream tagging operations.
    /// </summary>
    [Obsolete("Use StreamTagStore instead. This property will be removed in a future version.")]
    public string StreamTagConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection name used for snapshot persistence.
    /// </summary>
    [Obsolete("Use SnapShotStore instead. This property will be removed in a future version.")]
    public string SnapShotConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets chunking configuration; null when chunking is disabled.
    /// </summary>
    public StreamChunkSettings? ChunkSettings { get; set; } = new();

    /// <summary>
    /// Gets or sets the chunk descriptors when chunking is enabled.
    /// </summary>
    public List<StreamChunk> StreamChunks { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of snapshots associated with the stream.
    /// </summary>
    public List<StreamSnapShot> SnapShots { get; set; } = [];

    /// <summary>
    /// Gets or sets the document provider type (e.g., "blob", "cosmos").
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event stream tag provider type.
    /// </summary>
    public string EventStreamTagType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the document reference provider type.
    /// </summary>
    public string DocumentRefType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the named connection for event stream data storage.
    /// </summary>
    public string DataStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the named connection for document storage.
    /// </summary>
    public string DocumentStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection name used for document persistence.
    /// </summary>
    [Obsolete("Use DocumentStore instead. This property will be removed in a future version.")]
    public string DocumentConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the named connection for document tag storage.
    /// </summary>
    public string DocumentTagStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the named connection for stream tag storage.
    /// </summary>
    public string StreamTagStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the named connection for snapshot storage.
    /// </summary>
    public string SnapShotStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether chunking is enabled.
    /// </summary>
    public bool ChunkingEnabled()
    {
        return ChunkSettings is { EnableChunks: true };
    }

    /// <summary>
    /// Gets a value indicating whether the stream has snapshots.
    /// </summary>
    public bool HasSnapShots()
    {
        return SnapShots.Count != 0;
    }
}
