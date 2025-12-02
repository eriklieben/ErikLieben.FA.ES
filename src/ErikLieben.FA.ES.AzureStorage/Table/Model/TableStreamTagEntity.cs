using Azure;
using Azure.Data.Tables;

namespace ErikLieben.FA.ES.AzureStorage.Table.Model;

/// <summary>
/// Represents a stream tag entity stored in Azure Table Storage.
/// </summary>
/// <remarks>
/// PartitionKey: {ObjectName}_{StreamIdentifier}
/// RowKey: {SanitizedTag}
/// </remarks>
public class TableStreamTagEntity : ITableEntity
{
    /// <summary>
    /// Gets or sets the partition key (ObjectName_StreamIdentifier).
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row key (SanitizedTag).
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
    /// Gets or sets the original tag value.
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream identifier.
    /// </summary>
    public string StreamIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object name.
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object ID.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;
}
