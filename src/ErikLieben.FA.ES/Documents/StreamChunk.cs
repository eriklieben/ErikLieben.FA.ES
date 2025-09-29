using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Describes a contiguous segment (chunk) of an event stream by identifier and first/last event versions.
/// </summary>
public class StreamChunk
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StreamChunk"/> class.
    /// </summary>
    public StreamChunk()
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified identifiers and bounds.
    /// </summary>
    /// <param name="chunkIdentifier">The unique chunk identifier (zero-based).</param>
    /// <param name="firstEventVersion">The first event version in the chunk; null when unknown.</param>
    /// <param name="lastEventVersion">The last event version in the chunk; null when no events have been added.</param>
    public StreamChunk(int chunkIdentifier, int? firstEventVersion, int? lastEventVersion)
    {
        ChunkIdentifier = chunkIdentifier;
        FirstEventVersion = firstEventVersion;
        LastEventVersion = lastEventVersion;
    }

    /// <summary>
    /// Gets or sets the unique chunk identifier.
    /// </summary>
    [   JsonIgnore(Condition = JsonIgnoreCondition.Never),
        JsonPropertyName("id"),
        JsonPropertyOrder(0)]
    public int ChunkIdentifier { get; set; } = 0;

    /// <summary>
    /// Gets or sets the first event version in the chunk.
    /// </summary>
    [JsonPropertyName("first"), JsonPropertyOrder(1)]
    public int? FirstEventVersion { get; set; }

    /// <summary>
    /// Gets or sets the last event version in the chunk.
    /// </summary>
    [JsonPropertyName("last"), JsonPropertyOrder(2)]
    public int? LastEventVersion { get; set; }
}
