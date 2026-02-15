using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.S3.Model;

/// <summary>
/// Represents an index document that maps object identifiers to their last processed version identifiers.
/// </summary>
public class S3VersionIndexDocument
{
    /// <summary>
    /// Gets or sets the schema version of this document format.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the version index mapping an <see cref="ObjectIdentifier"/> to its <see cref="VersionIdentifier"/>.
    /// </summary>
    public Dictionary<ObjectIdentifier, VersionIdentifier> VersionIndex { get; set; } = [];

    public static S3VersionIndexDocument FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);
        return JsonSerializer.Deserialize(json, S3VersionIndexContext.Default.S3VersionIndexDocument)
               ?? throw new InvalidOperationException("Could not deserialize JSON of S3 version index document.");
    }

    public static string ToJson(Dictionary<ObjectIdentifier, VersionIdentifier> versionIndex)
    {
        ArgumentNullException.ThrowIfNull(versionIndex);
        return JsonSerializer.Serialize(new S3VersionIndexDocument
        {
            VersionIndex = versionIndex
        },
            S3VersionIndexContext.Default.S3VersionIndexDocument);
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(S3VersionIndexDocument))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class S3VersionIndexContext : JsonSerializerContext
{
}
