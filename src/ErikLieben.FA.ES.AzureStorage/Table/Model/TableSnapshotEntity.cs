using Azure;
using Azure.Data.Tables;

namespace ErikLieben.FA.ES.AzureStorage.Table.Model;

/// <summary>
/// Represents a snapshot entity stored in Azure Table Storage containing serialized aggregate data.
/// </summary>
/// <remarks>
/// PartitionKey: {ObjectName}_{StreamIdentifier}
/// RowKey: {Version:d20} or {Version:d20}_{Name}
/// </remarks>
public class TableSnapshotEntity : ITableEntity
{
    /// <summary>
    /// Gets or sets the partition key.
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row key.
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
    /// Gets or sets the stream identifier.
    /// </summary>
    public string StreamIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version at which the snapshot was taken.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets an optional name or version discriminator for the snapshot format.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the serialized aggregate data as JSON.
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type name of the aggregate.
    /// </summary>
    public string AggregateType { get; set; } = string.Empty;
}
