namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Provides configuration for chunking an event stream into fixed-size segments.
/// </summary>
/// <remarks>
/// <para>
/// Chunking splits an event stream into multiple storage units (e.g., separate blob files
/// or table partitions) to improve performance for high-volume streams.
/// </para>
/// <para>
/// <strong>When to enable chunking:</strong>
/// Most streams have fewer than 1000 events and don't need chunking. Enable chunking
/// when you expect streams to grow beyond several thousand events.
/// </para>
/// <para>
/// <strong>Recommended chunk size:</strong>
/// The default of 1000 events per chunk works well for most scenarios:
/// <list type="bullet">
///   <item>Keeps blob chunks at ~1-5MB (assuming ~1-5KB per event with JSON payload)</item>
///   <item>Provides fast read-modify-write operations for append-heavy workloads</item>
///   <item>Balances chunk management overhead vs individual chunk size</item>
/// </list>
/// </para>
/// </remarks>
public class StreamChunkSettings
{
    /// <summary>
    /// Gets or sets the maximum number of events per chunk.
    /// </summary>
    /// <value>
    /// The number of events per chunk. Default is 0 (uses system default of 1000 when chunking is enabled).
    /// Recommended range: 500-2000 for typical event sizes (1-5KB per event).
    /// </value>
    public int ChunkSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether chunking is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to split the stream into chunks; <c>false</c> to store all events
    /// in a single storage unit. Default is <c>false</c>.
    /// </value>
    public bool EnableChunks { get; set; } = false;
}
