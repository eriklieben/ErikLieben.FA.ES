using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.AzureStorage.AppendBlob.Model;

/// <summary>
/// Represents a commit marker appended to the NDJSON event stream after each batch of events.
/// Used to validate cross-blob integrity between the object document and the append blob.
/// </summary>
public record AppendBlobCommitMarker
{
    /// <summary>
    /// Marker type discriminator. Always "c" for commit markers.
    /// Used to distinguish markers from event lines in NDJSON.
    /// </summary>
    [JsonPropertyName("$m")]
    public string MarkerType { get; init; } = "c";

    /// <summary>
    /// The object document hash after Phase 1 save (current hash).
    /// </summary>
    [JsonPropertyName("h")]
    public required string Hash { get; init; }

    /// <summary>
    /// The object document hash before Phase 1 save (previous hash).
    /// </summary>
    [JsonPropertyName("ph")]
    public required string PrevHash { get; init; }

    /// <summary>
    /// The new stream version after this commit.
    /// </summary>
    [JsonPropertyName("v")]
    public required int Version { get; init; }

    /// <summary>
    /// The byte offset in the append blob where this batch of events starts.
    /// Used for incremental reads: when reading from a specific version, the reader
    /// can range-read from this offset instead of downloading the entire blob.
    /// Null for markers written before this feature was added (backwards compatible).
    /// </summary>
    [JsonPropertyName("o")]
    public long? Offset { get; init; }

    /// <summary>
    /// Indicates whether this commit includes a stream-closed event.
    /// When true, the stream is closed and no further events can be appended.
    /// Null for markers written before this feature was added (backwards compatible);
    /// the reader falls back to string matching for those markers.
    /// </summary>
    [JsonPropertyName("cl")]
    public bool? Closed { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppendBlobCommitMarker))]
internal partial class AppendBlobCommitMarkerContext : JsonSerializerContext
{
}
