using Azure;
using Azure.Data.Tables;

namespace ErikLieben.FA.ES.AzureStorage.Table.Model;

/// <summary>
/// Represents a document tag entity stored in Azure Table Storage.
/// </summary>
/// <remarks>
/// PartitionKey: {ObjectName}_{SanitizedTag}
/// RowKey: {ObjectId}
/// </remarks>
public class TableDocumentTagEntity : ITableEntity
{
    /// <summary>
    /// Gets or sets the partition key (ObjectName_SanitizedTag).
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row key (ObjectId).
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
    /// Gets or sets the object name this tag belongs to.
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object ID that has this tag.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;
}
