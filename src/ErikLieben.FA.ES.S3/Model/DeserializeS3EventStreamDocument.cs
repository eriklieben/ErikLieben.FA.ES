using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.S3.Model;

/// <summary>
/// Mutable document used for JSON deserialization with migration support.
/// </summary>
public class DeserializeS3EventStreamDocument
{
    public string ObjectId { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public DeserializeS3StreamInformation Active { get; set; } = new();
    public List<TerminatedStream> TerminatedStreams { get; set; } = [];
    public string? SchemaVersion { get; set; }
    public string? Hash { get; set; }
    public string? PrevHash { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? DocumentPath { get; set; }

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
