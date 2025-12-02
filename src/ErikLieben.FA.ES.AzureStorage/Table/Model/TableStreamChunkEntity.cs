using Azure;
using Azure.Data.Tables;

namespace ErikLieben.FA.ES.AzureStorage.Table.Model;

/// <summary>
/// Represents a stream chunk entity stored in Azure Table Storage.
/// </summary>
/// <remarks>
/// PartitionKey: {ObjectId}
/// RowKey: {ChunkIdentifier:d10} (zero-padded for proper sorting)
/// </remarks>
public class TableStreamChunkEntity : ITableEntity
{
    /// <summary>
    /// Gets or sets the partition key (ObjectId).
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row key (zero-padded ChunkIdentifier).
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the entity.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the ETag for optimistic concurrency.
    /// </summary>
    public ETag ETag { get; set; }

    /// <summary>
    /// Gets or sets the unique chunk identifier (zero-based).
    /// </summary>
    public int ChunkIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the first event version in the chunk.
    /// </summary>
    public int? FirstEventVersion { get; set; }

    /// <summary>
    /// Gets or sets the last event version in the chunk.
    /// </summary>
    public int? LastEventVersion { get; set; }
}
