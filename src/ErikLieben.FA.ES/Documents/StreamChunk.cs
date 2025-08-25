using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Documents;

public class StreamChunk
{
    public StreamChunk()
    {
    }

    public StreamChunk(int chunkIdentifier, int? firstEventVersion, int? lastEventVersion)
    {
        ChunkIdentifier = chunkIdentifier;
        FirstEventVersion = firstEventVersion;
        LastEventVersion = lastEventVersion;
    }

    [   JsonIgnore(Condition = JsonIgnoreCondition.Never), 
        JsonPropertyName("id"), 
        JsonPropertyOrder(0)]
    public int ChunkIdentifier { get; set; } = 0;

    [JsonPropertyName("first"), JsonPropertyOrder(1)]
    public int? FirstEventVersion { get; set; }

    [JsonPropertyName("last"), JsonPropertyOrder(2)]
    public int? LastEventVersion { get; set; }
}
