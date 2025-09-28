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
    public string StreamConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection name used for document tagging operations.
    /// </summary>
    public string DocumentTagConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection name used for stream tagging operations.
    /// </summary>
    public string StreamTagConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection name used for snapshot persistence.
    /// </summary>
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
