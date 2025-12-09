using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.CosmosDb.Model;

/// <summary>
/// Represents a tag entry stored in CosmosDB.
/// Partition key: tagKey (combination of objectName and tag for efficient lookups).
/// </summary>
public class CosmosDbTagEntity
{
    /// <summary>
    /// Unique identifier for this tag entry. Format: {tagType}_{objectName}_{tag}_{objectId}
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The tag key used as partition key. Format: {objectName}_{tag}
    /// </summary>
    [JsonPropertyName("tagKey")]
    public string TagKey { get; set; } = string.Empty;

    /// <summary>
    /// The type of tag (document or stream).
    /// </summary>
    [JsonPropertyName("tagType")]
    public string TagType { get; set; } = string.Empty;

    /// <summary>
    /// The object type name.
    /// </summary>
    [JsonPropertyName("objectName")]
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// The tag value.
    /// </summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// The object instance identifier.
    /// </summary>
    [JsonPropertyName("objectId")]
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// When the tag was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Document type discriminator.
    /// </summary>
    [JsonPropertyName("_type")]
    public string Type { get; set; } = "tag";

    /// <summary>
    /// Creates the document ID.
    /// </summary>
    public static string CreateId(string tagType, string objectName, string tag, string objectId)
        => $"{tagType}_{objectName}_{tag}_{objectId}";

    /// <summary>
    /// Creates the tag key (partition key).
    /// </summary>
    public static string CreateTagKey(string objectName, string tag)
        => $"{objectName.ToLowerInvariant()}_{tag}";
}
