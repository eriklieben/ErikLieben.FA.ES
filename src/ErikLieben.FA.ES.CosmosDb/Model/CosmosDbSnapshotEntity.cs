using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.CosmosDb.Model;

/// <summary>
/// Represents a snapshot stored in CosmosDB.
/// Partition key: streamId (colocates snapshots with their event stream).
/// </summary>
public class CosmosDbSnapshotEntity
{
    /// <summary>
    /// Unique identifier for this snapshot. Format: {streamId}_{version:D20}_{name}
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The stream identifier. Used as partition key.
    /// </summary>
    [JsonPropertyName("streamId")]
    public string StreamId { get; set; } = string.Empty;

    /// <summary>
    /// The event version at which this snapshot was taken.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; }

    /// <summary>
    /// Optional name for the snapshot (e.g., aggregate type name).
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The serialized snapshot data as JSON.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// The CLR type name of the snapshot data.
    /// </summary>
    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// When the snapshot was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Document type discriminator.
    /// </summary>
    [JsonPropertyName("_type")]
    public string Type { get; set; } = "snapshot";

    /// <summary>
    /// Creates the document ID from stream ID, version, and optional name.
    /// </summary>
    public static string CreateId(string streamId, int version, string? name = null)
        => string.IsNullOrEmpty(name)
            ? $"{streamId}_{version:D20}"
            : $"{streamId}_{version:D20}_{name}";
}
