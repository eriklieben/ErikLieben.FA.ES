using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

/// <summary>
/// Stream information model used for serialization that only includes the new *Store properties.
/// Old *ConnectionName properties are excluded to facilitate automatic migration.
/// </summary>
public class SerializeStreamInformation
{
    /// <summary>
    /// Gets or sets the unique stream identifier for the object.
    /// </summary>
    public string StreamIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream provider type (e.g., "blob", "table").
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
    /// Gets or sets chunking configuration; null when chunking is disabled.
    /// </summary>
    public StreamChunkSettings? ChunkSettings { get; set; }

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
    /// Creates a <see cref="SerializeStreamInformation"/> from a <see cref="StreamInformation"/>.
    /// Migrates old *ConnectionName values to new *Store properties if not already set.
    /// </summary>
    public static SerializeStreamInformation From(StreamInformation source)
    {
        return new SerializeStreamInformation
        {
            StreamIdentifier = source.StreamIdentifier,
            StreamType = source.StreamType,
            DocumentTagType = source.DocumentTagType,
            CurrentStreamVersion = source.CurrentStreamVersion,
            ChunkSettings = source.ChunkSettings,
            StreamChunks = source.StreamChunks,
            SnapShots = source.SnapShots,
            DocumentType = source.DocumentType,
            EventStreamTagType = source.EventStreamTagType,
            DocumentRefType = source.DocumentRefType,
            // Use new *Store properties, falling back to old *ConnectionName if not set
#pragma warning disable CS0618 // Type or member is obsolete
            DataStore = !string.IsNullOrEmpty(source.DataStore) ? source.DataStore : source.StreamConnectionName,
            DocumentStore = !string.IsNullOrEmpty(source.DocumentStore) ? source.DocumentStore : source.DocumentConnectionName,
            DocumentTagStore = !string.IsNullOrEmpty(source.DocumentTagStore) ? source.DocumentTagStore : source.DocumentTagConnectionName,
            StreamTagStore = !string.IsNullOrEmpty(source.StreamTagStore) ? source.StreamTagStore : source.StreamTagConnectionName,
            SnapShotStore = !string.IsNullOrEmpty(source.SnapShotStore) ? source.SnapShotStore : source.SnapShotConnectionName,
#pragma warning restore CS0618
        };
    }
}
