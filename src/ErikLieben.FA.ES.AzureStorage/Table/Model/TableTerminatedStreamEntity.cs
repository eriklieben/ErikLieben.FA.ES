using Azure;
using Azure.Data.Tables;

namespace ErikLieben.FA.ES.AzureStorage.Table.Model;

/// <summary>
/// Represents a terminated stream entity stored in Azure Table Storage.
/// </summary>
/// <remarks>
/// PartitionKey: {ObjectId}
/// RowKey: {StreamIdentifier}
/// </remarks>
public class TableTerminatedStreamEntity : ITableEntity
{
    /// <summary>
    /// Gets or sets the partition key (ObjectId).
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row key (StreamIdentifier).
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
    /// Gets or sets the identifier of the terminated stream.
    /// </summary>
    public string? StreamIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the stream provider type (e.g., "blob", "table").
    /// </summary>
    public string? StreamType { get; set; }

    /// <summary>
    /// Gets or sets the data store name for the terminated stream.
    /// </summary>
    public string? DataStore { get; set; }

    /// <summary>
    /// Gets or sets the reason for termination.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the identifier of a continuation stream when the stream continues elsewhere.
    /// </summary>
    public string? ContinuationStreamId { get; set; }

    /// <summary>
    /// Gets or sets the UTC date/time when the stream was terminated.
    /// </summary>
    public DateTimeOffset TerminationDate { get; set; }

    /// <summary>
    /// Gets or sets the stream version at termination, when known.
    /// </summary>
    public int? StreamVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the materialized document has been deleted.
    /// </summary>
    public bool Deleted { get; set; }

    /// <summary>
    /// Gets or sets the UTC date/time when the document was deleted.
    /// </summary>
    public DateTimeOffset DeletionDate { get; set; }
}
