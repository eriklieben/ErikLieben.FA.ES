using Azure;
using Azure.Data.Tables;

namespace ErikLieben.FA.ES.AzureStorage.Table.Model;

/// <summary>
/// Represents document snapshot metadata stored in Azure Table Storage.
/// This tracks when snapshots were taken, separate from the actual snapshot data.
/// </summary>
/// <remarks>
/// PartitionKey: {ObjectId}
/// RowKey: {UntilVersion:d10} (zero-padded for proper sorting)
/// </remarks>
public class TableDocumentSnapShotEntity : ITableEntity
{
    /// <summary>
    /// Gets or sets the partition key (ObjectId).
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row key (zero-padded UntilVersion).
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
    /// Gets or sets the version up to which the snapshot was taken.
    /// </summary>
    public int UntilVersion { get; set; }

    /// <summary>
    /// Gets or sets an optional name or version of the snapshot type.
    /// </summary>
    public string? Name { get; set; }
}
