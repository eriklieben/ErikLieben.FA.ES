using Azure;
using Azure.Data.Tables;

namespace ErikLieben.FA.ES.AzureStorage.Table.Model;

/// <summary>
/// Represents an event entity stored in Azure Table Storage.
/// </summary>
/// <remarks>
/// PartitionKey: {StreamIdentifier} (or with chunk suffix for chunked streams)
/// RowKey: {EventVersion:d20} (zero-padded to ensure proper sorting)
/// </remarks>
public class TableEventEntity : ITableEntity
{
    /// <summary>
    /// Gets or sets the partition key (StreamIdentifier).
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row key (zero-padded event version).
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
    /// Gets or sets the identifier of the object to which the event belongs.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream identifier for the event stream.
    /// </summary>
    public string StreamIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version number of the event.
    /// </summary>
    public int EventVersion { get; set; }

    /// <summary>
    /// Gets or sets the type name of the event.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schema version of the event.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the event payload as raw JSON.
    /// </summary>
    public string Payload { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the action metadata as JSON (optional).
    /// </summary>
    public string? ActionMetadata { get; set; }

    /// <summary>
    /// Gets or sets the event metadata as JSON (optional).
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the chunk identifier when chunking is enabled.
    /// </summary>
    public int? ChunkIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the last known hash of the associated object document used for optimistic concurrency.
    /// </summary>
    public string LastObjectDocumentHash { get; set; } = "*";
}
