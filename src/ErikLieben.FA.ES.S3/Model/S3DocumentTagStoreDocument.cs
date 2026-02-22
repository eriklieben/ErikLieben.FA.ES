using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.S3.Model;

/// <summary>
/// Represents the persisted tag document that maps a tag value to a list of object identifiers in S3.
/// </summary>
public class S3DocumentTagStoreDocument
{
    /// <summary>
    /// Gets or sets the identifiers of documents associated with the tag.
    /// </summary>
    public List<string> ObjectIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the tag value used as the key for the association.
    /// </summary>
    public required string Tag { get; set; }

    /// <summary>
    /// Gets or sets the schema version of this document format.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0.0";
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(S3DocumentTagStoreDocument))]
internal partial class S3DocumentTagStoreDocumentContext : JsonSerializerContext
{
}
