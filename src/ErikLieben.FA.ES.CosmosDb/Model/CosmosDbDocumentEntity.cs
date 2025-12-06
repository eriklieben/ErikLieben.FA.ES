using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.CosmosDb.Model;

/// <summary>
/// Represents an ObjectDocument stored in CosmosDB.
/// Partition key: objectName (groups documents by type for efficient queries).
/// </summary>
public class CosmosDbDocumentEntity
{
    /// <summary>
    /// Unique identifier for this document. Format: {objectName}_{objectId}
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The object type name. Used as partition key.
    /// </summary>
    [JsonPropertyName("objectName")]
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// The object instance identifier.
    /// </summary>
    [JsonPropertyName("objectId")]
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// The active stream information.
    /// </summary>
    [JsonPropertyName("active")]
    public CosmosDbStreamInfo Active { get; set; } = new();

    /// <summary>
    /// List of terminated (archived) streams.
    /// </summary>
    [JsonPropertyName("terminatedStreams")]
    public List<CosmosDbTerminatedStreamInfo> TerminatedStreams { get; set; } = [];

    /// <summary>
    /// Schema version for the document format.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; set; }

    /// <summary>
    /// Hash for optimistic concurrency control.
    /// </summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    /// <summary>
    /// Previous hash for conflict detection.
    /// </summary>
    [JsonPropertyName("prevHash")]
    public string? PrevHash { get; set; }

    /// <summary>
    /// Document type discriminator.
    /// </summary>
    [JsonPropertyName("_type")]
    public string Type { get; set; } = "document";

    /// <summary>
    /// ETag for CosmosDB optimistic concurrency.
    /// </summary>
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }

    /// <summary>
    /// Creates the document ID from object name and ID.
    /// </summary>
    public static string CreateId(string objectName, string objectId) => $"{objectName.ToLowerInvariant()}_{objectId}";
}

/// <summary>
/// Stream information stored within the document entity.
/// </summary>
public class CosmosDbStreamInfo
{
    [JsonPropertyName("streamIdentifier")]
    public string StreamIdentifier { get; set; } = string.Empty;

    [JsonPropertyName("currentStreamVersion")]
    public int CurrentStreamVersion { get; set; }

    [JsonPropertyName("streamType")]
    public string StreamType { get; set; } = "cosmosdb";

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "cosmosdb";

    [JsonPropertyName("documentTagType")]
    public string DocumentTagType { get; set; } = "cosmosdb";

    [JsonPropertyName("eventStreamTagType")]
    public string EventStreamTagType { get; set; } = "cosmosdb";

    [JsonPropertyName("dataStore")]
    public string DataStore { get; set; } = "cosmosdb";

    [JsonPropertyName("documentStore")]
    public string DocumentStore { get; set; } = "cosmosdb";

    [JsonPropertyName("documentTagStore")]
    public string DocumentTagStore { get; set; } = "cosmosdb";

    [JsonPropertyName("streamTagStore")]
    public string StreamTagStore { get; set; } = "cosmosdb";

    [JsonPropertyName("snapShotStore")]
    public string SnapShotStore { get; set; } = "cosmosdb";

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("lastModifiedAt")]
    public DateTimeOffset LastModifiedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Information about a terminated (archived) stream.
/// </summary>
public class CosmosDbTerminatedStreamInfo
{
    [JsonPropertyName("streamIdentifier")]
    public string? StreamIdentifier { get; set; }

    [JsonPropertyName("finalVersion")]
    public int? FinalVersion { get; set; }

    [JsonPropertyName("terminatedAt")]
    public DateTimeOffset TerminatedAt { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
