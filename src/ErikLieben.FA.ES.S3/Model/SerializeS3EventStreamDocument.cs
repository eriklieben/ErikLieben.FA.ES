using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.S3.Model;

/// <summary>
/// Document model used for serialization that excludes legacy *ConnectionName properties.
/// </summary>
public class SerializeS3EventStreamDocument
{
    public string ObjectId { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public SerializeS3StreamInformation Active { get; set; } = new();
    public List<TerminatedStream> TerminatedStreams { get; set; } = [];
    public string? SchemaVersion { get; set; }
    public string? Hash { get; set; }
    public string? PrevHash { get; set; }

    /// <summary>
    /// Creates a serialization document from an <see cref="IObjectDocument"/>.
    /// </summary>
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
    public string StreamIdentifier { get; set; } = string.Empty;
    public string StreamType { get; set; } = string.Empty;
    public string DocumentTagType { get; set; } = string.Empty;
    public int CurrentStreamVersion { get; set; } = -1;
    public StreamChunkSettings? ChunkSettings { get; set; }
    public List<StreamChunk> StreamChunks { get; set; } = [];
    public List<StreamSnapShot> SnapShots { get; set; } = [];
    public string DocumentType { get; set; } = string.Empty;
    public string EventStreamTagType { get; set; } = string.Empty;
    public string DocumentRefType { get; set; } = string.Empty;
    public string DataStore { get; set; } = string.Empty;
    public string DocumentStore { get; set; } = string.Empty;
    public string DocumentTagStore { get; set; } = string.Empty;
    public string StreamTagStore { get; set; } = string.Empty;
    public string SnapShotStore { get; set; } = string.Empty;

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
