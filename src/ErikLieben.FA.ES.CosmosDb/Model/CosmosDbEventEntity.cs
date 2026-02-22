using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.CosmosDb.Model;

/// <summary>
/// Represents an event stored in CosmosDB.
/// Partition key: streamId (keeps all events for a stream together).
/// </summary>
public class CosmosDbEventEntity
{
    /// <summary>
    /// Unique identifier for this event. Format: {streamId}_{version:D20}
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The stream identifier. Used as partition key for efficient stream reads.
    /// </summary>
    [JsonPropertyName("streamId")]
    public string StreamId { get; set; } = string.Empty;

    /// <summary>
    /// The event version within the stream (1-based, monotonically increasing).
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; }

    /// <summary>
    /// The logical event type name (e.g., "OrderCreated").
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// The schema version of the event payload.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// The serialized event payload as JSON.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// When the event occurred.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// External sequencer identifier for events ordered by external systems.
    /// </summary>
    [JsonPropertyName("externalSequencer")]
    public string? ExternalSequencer { get; set; }

    /// <summary>
    /// Optional correlation ID for tracing.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Optional causation ID linking to the causing event.
    /// </summary>
    [JsonPropertyName("causationId")]
    public string? CausationId { get; set; }

    /// <summary>
    /// Document type discriminator for polymorphic queries.
    /// </summary>
    [JsonPropertyName("_type")]
    public string Type { get; set; } = "event";

    /// <summary>
    /// Time-to-live in seconds. -1 for infinite.
    /// </summary>
    [JsonPropertyName("ttl")]
    public int? Ttl { get; set; }

    /// <summary>
    /// Creates the document ID from stream ID and version.
    /// </summary>
    public static string CreateId(string streamId, int version) => $"{streamId}_{version:D20}";
}
