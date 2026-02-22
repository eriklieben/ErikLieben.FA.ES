using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.S3.Model;

/// <summary>
/// Mutable document used for JSON deserialization with migration support.
/// </summary>
public class DeserializeS3EventStreamDocument
{
    /// <summary>
    /// Gets or sets the unique identifier of the aggregate object.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type name of the aggregate object.
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the active stream information for this document.
    /// </summary>
    public DeserializeS3StreamInformation Active { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of previously terminated streams.
    /// </summary>
    public List<TerminatedStream> TerminatedStreams { get; set; } = [];

    /// <summary>
    /// Gets or sets the schema version of this document format.
    /// </summary>
    public string? SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the hash of the current document state for integrity verification.
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Gets or sets the hash of the previous document state for chain verification.
    /// </summary>
    public string? PrevHash { get; set; }

    /// <summary>
    /// Gets or sets the S3 object key path of this document. Excluded from JSON serialization.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? DocumentPath { get; set; }

    /// <summary>
    /// Converts this deserialization model to an immutable <see cref="S3EventStreamDocument"/>.
    /// </summary>
    /// <returns>A new <see cref="S3EventStreamDocument"/> populated from this instance.</returns>
    public S3EventStreamDocument ToS3EventStreamDocument()
    {
        return new S3EventStreamDocument(
            ObjectId,
            ObjectName,
            Active.ToStreamInformation(),
            TerminatedStreams,
            SchemaVersion,
            Hash,
            PrevHash);
    }
}

/// <summary>
/// Stream information model for deserialization with legacy property support.
/// </summary>
public class DeserializeS3StreamInformation
{
    /// <summary>
    /// Gets or sets the unique identifier of the event stream.
    /// </summary>
    public string StreamIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the event stream.
    /// </summary>
    public string StreamType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type used for document tagging.
    /// </summary>
    public string DocumentTagType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current version number of the stream. Defaults to -1 indicating no events.
    /// </summary>
    public int CurrentStreamVersion { get; set; } = -1;

    /// <summary>
    /// Gets or sets the chunk configuration settings for the stream.
    /// </summary>
    public StreamChunkSettings? ChunkSettings { get; set; }

    /// <summary>
    /// Gets or sets the list of stream chunks containing event data.
    /// </summary>
    public List<StreamChunk> StreamChunks { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of stream snapshots for optimized replay.
    /// </summary>
    public List<StreamSnapShot> SnapShots { get; set; } = [];

    /// <summary>
    /// Gets or sets the document type identifier.
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event stream tag type identifier.
    /// </summary>
    public string EventStreamTagType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the document reference type identifier.
    /// </summary>
    public string DocumentRefType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage location name for event data.
    /// </summary>
    public string DataStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage location name for documents.
    /// </summary>
    public string DocumentStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage location name for document tags.
    /// </summary>
    public string DocumentTagStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage location name for stream tags.
    /// </summary>
    public string StreamTagStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage location name for snapshots.
    /// </summary>
    public string SnapShotStore { get; set; } = string.Empty;

    /// <summary>
    /// Converts this deserialization model to a <see cref="StreamInformation"/> instance.
    /// </summary>
    /// <returns>A new <see cref="StreamInformation"/> populated from this instance.</returns>
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
            DataStore = DataStore,
            DocumentStore = DocumentStore,
            DocumentTagStore = DocumentTagStore,
            StreamTagStore = StreamTagStore,
            SnapShotStore = SnapShotStore,
        };
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DeserializeS3EventStreamDocument))]
internal partial class DeserializeS3EventStreamDocumentContext : JsonSerializerContext
{
}
