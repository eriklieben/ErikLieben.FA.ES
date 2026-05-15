using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Postgres.Model;

/// <summary>
/// Wire-format projection of <see cref="StreamInformation"/> for the active jsonb column.
/// Mirrors the public, non-obsolete fields only; legacy *ConnectionName properties are not persisted.
/// CurrentStreamVersion is intentionally excluded — it lives in the dedicated
/// faes_documents.current_stream_version column to keep faes_append on the HOT update path.
/// </summary>
internal sealed record PostgresStreamInformation
{
    [JsonPropertyName("streamIdentifier")] public string StreamIdentifier { get; init; } = string.Empty;
    [JsonPropertyName("streamType")]       public string StreamType { get; init; } = string.Empty;
    [JsonPropertyName("documentType")]     public string DocumentType { get; init; } = string.Empty;
    [JsonPropertyName("documentTagType")]  public string DocumentTagType { get; init; } = string.Empty;
    [JsonPropertyName("eventStreamTagType")] public string EventStreamTagType { get; init; } = string.Empty;
    [JsonPropertyName("documentRefType")]  public string DocumentRefType { get; init; } = string.Empty;
    [JsonPropertyName("dataStore")]        public string DataStore { get; init; } = string.Empty;
    [JsonPropertyName("documentStore")]    public string DocumentStore { get; init; } = string.Empty;
    [JsonPropertyName("documentTagStore")] public string DocumentTagStore { get; init; } = string.Empty;
    [JsonPropertyName("streamTagStore")]   public string StreamTagStore { get; init; } = string.Empty;
    [JsonPropertyName("snapShotStore")]    public string SnapShotStore { get; init; } = string.Empty;
    [JsonPropertyName("isBroken")]         public bool IsBroken { get; init; }

    public static PostgresStreamInformation From(StreamInformation s) => new()
    {
        StreamIdentifier   = s.StreamIdentifier,
        StreamType         = s.StreamType,
        DocumentType       = s.DocumentType,
        DocumentTagType    = s.DocumentTagType,
        EventStreamTagType = s.EventStreamTagType,
        DocumentRefType    = s.DocumentRefType,
        DataStore          = s.DataStore,
        DocumentStore      = s.DocumentStore,
        DocumentTagStore   = s.DocumentTagStore,
        StreamTagStore     = s.StreamTagStore,
        SnapShotStore      = s.SnapShotStore,
        IsBroken           = s.IsBroken,
    };

    public StreamInformation ToStreamInformation(int currentStreamVersion) => new()
    {
        StreamIdentifier   = StreamIdentifier,
        StreamType         = StreamType,
        DocumentType       = DocumentType,
        DocumentTagType    = DocumentTagType,
        EventStreamTagType = EventStreamTagType,
        DocumentRefType    = DocumentRefType,
        DataStore          = DataStore,
        DocumentStore      = DocumentStore,
        DocumentTagStore   = DocumentTagStore,
        StreamTagStore     = StreamTagStore,
        SnapShotStore      = SnapShotStore,
        CurrentStreamVersion = currentStreamVersion,
        IsBroken           = IsBroken,
    };
}

/// <summary>
/// Wire-format projection of <see cref="TerminatedStream"/> for the terminated_streams jsonb column.
/// Mirrors only the fields needed for serialization round-trips; populate as the feature expands.
/// </summary>
internal sealed record PostgresTerminatedStream
{
    [JsonPropertyName("streamIdentifier")] public string StreamIdentifier { get; init; } = string.Empty;
}
