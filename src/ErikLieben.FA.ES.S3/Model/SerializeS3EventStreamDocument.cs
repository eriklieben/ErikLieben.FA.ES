using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.S3.Model;

/// <summary>
/// Document model used for serialization that excludes legacy *ConnectionName properties.
/// </summary>
public class SerializeS3EventStreamDocument
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
    public SerializeS3StreamInformation Active { get; set; } = new();

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
    /// Creates a serialization document from an <see cref="IObjectDocument"/>.
    /// </summary>
    /// <param name="source">The source object document to convert.</param>
    /// <returns>A new <see cref="SerializeS3EventStreamDocument"/> populated from the source.</returns>
    public static SerializeS3EventStreamDocument From(IObjectDocument source)
    {
        return new SerializeS3EventStreamDocument
        {
            ObjectId = source.ObjectId,
            ObjectName = source.ObjectName,
            Active = SerializeS3StreamInformation.From(source.Active),
            TerminatedStreams = source.TerminatedStreams.ToList(),
            SchemaVersion = source.SchemaVersion,
            Hash = source.Hash,
            PrevHash = source.PrevHash,
        };
    }
}

/// <summary>
/// Stream information model used for serialization that only includes the new *Store properties.
/// </summary>
public class SerializeS3StreamInformation
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
    /// Creates a serialization stream information model from a <see cref="StreamInformation"/> instance.
    /// </summary>
    /// <param name="source">The source stream information to convert.</param>
    /// <returns>A new <see cref="SerializeS3StreamInformation"/> populated from the source.</returns>
    public static SerializeS3StreamInformation From(StreamInformation source)
    {
        return new SerializeS3StreamInformation
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
            DataStore = !string.IsNullOrEmpty(source.DataStore) ? source.DataStore : string.Empty,
            DocumentStore = !string.IsNullOrEmpty(source.DocumentStore) ? source.DocumentStore : string.Empty,
            DocumentTagStore = !string.IsNullOrEmpty(source.DocumentTagStore) ? source.DocumentTagStore : string.Empty,
            StreamTagStore = !string.IsNullOrEmpty(source.StreamTagStore) ? source.StreamTagStore : string.Empty,
            SnapShotStore = !string.IsNullOrEmpty(source.SnapShotStore) ? source.SnapShotStore : string.Empty,
        };
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SerializeS3EventStreamDocument))]
internal partial class SerializeS3EventStreamDocumentContext : JsonSerializerContext
{
}
