using Azure;
using Azure.Data.Tables;

namespace ErikLieben.FA.ES.AzureStorage.Table.Model;

/// <summary>
/// Represents an object document entity stored in Azure Table Storage.
/// </summary>
/// <remarks>
/// PartitionKey: {ObjectName}
/// RowKey: {ObjectId}
/// </remarks>
public class TableDocumentEntity : ITableEntity
{
    /// <summary>
    /// Gets or sets the partition key (ObjectName).
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
    /// Gets or sets the identifier of the object.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the logical name/type of the object.
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    #region Active Stream Properties (split from ActiveStreamInfo JSON)

    /// <summary>
    /// Gets or sets the unique stream identifier for the active stream.
    /// </summary>
    public string ActiveStreamIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream provider type (e.g., "blob", "table").
    /// </summary>
    public string ActiveStreamType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tag store/provider type for document tags.
    /// </summary>
    public string ActiveDocumentTagType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current stream version (last appended event version).
    /// </summary>
    public int ActiveCurrentStreamVersion { get; set; } = -1;

    /// <summary>
    /// Gets or sets the document provider type (e.g., "blob", "table").
    /// </summary>
    public string ActiveDocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event stream tag provider type.
    /// </summary>
    public string ActiveEventStreamTagType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the document reference provider type.
    /// </summary>
    public string ActiveDocumentRefType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the named connection for event stream data storage.
    /// </summary>
    public string ActiveDataStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the named connection for document storage.
    /// </summary>
    public string ActiveDocumentStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the named connection for document tag storage.
    /// </summary>
    public string ActiveDocumentTagStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the named connection for stream tag storage.
    /// </summary>
    public string ActiveStreamTagStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the named connection for snapshot storage.
    /// </summary>
    public string ActiveSnapShotStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether chunking is enabled.
    /// </summary>
    public bool ActiveChunkingEnabled { get; set; }

    /// <summary>
    /// Gets or sets the chunk size when chunking is enabled.
    /// </summary>
    public int ActiveChunkSize { get; set; }

    #endregion

    /// <summary>
    /// Gets or sets the schema version of this document format.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the hash of the document used for optimistic concurrency.
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Gets or sets the previous hash of the document.
    /// </summary>
    public string? PrevHash { get; set; }
}
