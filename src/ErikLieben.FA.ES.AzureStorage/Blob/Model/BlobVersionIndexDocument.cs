using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

/// <summary>
/// Represents an index document that maps object identifiers to their last processed version identifiers.
/// </summary>
public class BlobVersionIndexDocument
{
    /// <summary>
    /// Gets or sets the schema version of this document format.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the version index mapping an <see cref="ObjectIdentifier"/> to its <see cref="VersionIdentifier"/>.
    /// </summary>
    public Dictionary<ObjectIdentifier, VersionIdentifier> VersionIndex { get; set; } = [];

    /// <summary>
    /// Deserializes a <see cref="BlobVersionIndexDocument"/> from its JSON representation.
    /// </summary>
    /// <param name="json">The JSON text containing the version index document.</param>
    /// <returns>The deserialized <see cref="BlobVersionIndexDocument"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when deserialization fails.</exception>
    public static BlobVersionIndexDocument FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);
        return JsonSerializer.Deserialize(json, VersionIndexContext.Default.BlobVersionIndexDocument)
               ?? throw new InvalidOperationException("Could not deserialize JSON of blob version index document.");
    }

    /// <summary>
    /// Serializes the specified version index to JSON.
    /// </summary>
    /// <param name="versionIndex">The version index to serialize.</param>
    /// <returns>The JSON representation of a <see cref="BlobVersionIndexDocument"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="versionIndex"/> is null.</exception>
    public static string ToJson(Dictionary<ObjectIdentifier, VersionIdentifier> versionIndex)
    {
        ArgumentNullException.ThrowIfNull(versionIndex);
        return JsonSerializer.Serialize(new BlobVersionIndexDocument
        {
            VersionIndex = versionIndex
        },
            VersionIndexContext.Default.BlobVersionIndexDocument);
    }
}


[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull )]
[JsonSerializable(typeof(BlobVersionIndexDocument))]
[JsonSerializable(typeof(Dictionary<string, string>))]
/// <summary>
/// Provides the System.Text.Json source-generation context for <see cref="BlobVersionIndexDocument"/> and related types.
/// </summary>
/// <remarks>
/// This context enables fast, reflection-free JSON (de)serialization for the Azure Storage blob version index document.
/// </remarks>
public partial class VersionIndexContext : JsonSerializerContext
{
}
