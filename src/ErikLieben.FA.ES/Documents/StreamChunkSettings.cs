namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Provides configuration for chunking an event stream into fixed-size segments.
/// </summary>
public class StreamChunkSettings
{
    /// <summary>
    /// Gets or sets the maximum number of events per chunk.
    /// </summary>
    public int ChunkSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether chunking is enabled.
    /// </summary>
    public bool EnableChunks { get; set; } = false;
}
