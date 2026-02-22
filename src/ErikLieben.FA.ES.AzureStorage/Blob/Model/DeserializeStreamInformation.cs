using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

/// <summary>
/// Stream information model used for deserialization that includes both old *ConnectionName
/// and new *Store properties to support automatic migration of legacy documents.
/// </summary>
public class DeserializeStreamInformation
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

    // New *Store properties

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

    // Old *ConnectionName properties (for reading legacy documents)

    /// <summary>
    /// Gets or sets the legacy connection name for event stream (migrates to DataStore).
    /// </summary>
    public string StreamConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the legacy connection name for document tags (migrates to DocumentTagStore).
    /// </summary>
    public string DocumentTagConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the legacy connection name for stream tags (migrates to StreamTagStore).
    /// </summary>
    public string StreamTagConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the legacy connection name for snapshots (migrates to SnapShotStore).
    /// </summary>
    public string SnapShotConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the legacy connection name for documents (migrates to DocumentStore).
    /// </summary>
    public string DocumentConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Converts this deserialization model to a <see cref="StreamInformation"/>,
    /// migrating old *ConnectionName values to new *Store properties.
    /// </summary>
    public StreamInformation ToStreamInformation()
    {
        return new StreamInformation
        {
            StreamIdentifier = StreamIdentifier,
            StreamType = StreamType,
            DocumentTagType = DocumentTagType,
            CurrentStreamVersion = CurrentStreamVersion,
            ChunkSettings = ChunkSettings,
            StreamChunks = StreamChunks,
            SnapShots = SnapShots,
            DocumentType = DocumentType,
            EventStreamTagType = EventStreamTagType,
            DocumentRefType = DocumentRefType,
            // Prefer new *Store, fall back to old *ConnectionName
            DataStore = !string.IsNullOrEmpty(DataStore) ? DataStore : StreamConnectionName,
            DocumentStore = !string.IsNullOrEmpty(DocumentStore) ? DocumentStore : DocumentConnectionName,
            DocumentTagStore = !string.IsNullOrEmpty(DocumentTagStore) ? DocumentTagStore : DocumentTagConnectionName,
            StreamTagStore = !string.IsNullOrEmpty(StreamTagStore) ? StreamTagStore : StreamTagConnectionName,
            SnapShotStore = !string.IsNullOrEmpty(SnapShotStore) ? SnapShotStore : SnapShotConnectionName,
        };
    }

    /// <summary>
    /// Creates a <see cref="DeserializeStreamInformation"/> from a <see cref="StreamInformation"/>.
    /// Useful for testing scenarios.
    /// </summary>
    public static DeserializeStreamInformation From(StreamInformation source)
    {
        return new DeserializeStreamInformation
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
            DataStore = source.DataStore,
            DocumentStore = source.DocumentStore,
            DocumentTagStore = source.DocumentTagStore,
            StreamTagStore = source.StreamTagStore,
            SnapShotStore = source.SnapShotStore,
#pragma warning disable CS0618 // Type or member is obsolete
            StreamConnectionName = source.StreamConnectionName,
            DocumentConnectionName = source.DocumentConnectionName,
            DocumentTagConnectionName = source.DocumentTagConnectionName,
            StreamTagConnectionName = source.StreamTagConnectionName,
            SnapShotConnectionName = source.SnapShotConnectionName,
#pragma warning restore CS0618
        };
    }
}
