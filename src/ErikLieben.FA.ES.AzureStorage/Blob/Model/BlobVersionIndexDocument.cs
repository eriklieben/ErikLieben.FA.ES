using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

public class BlobVersionIndexDocument
{
    public string SchemaVersion { get; set; } = "1.0.0";

    public Dictionary<ObjectIdentifier, VersionIdentifier> VersionIndex { get; set; } = [];

    public static BlobVersionIndexDocument FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);
        return JsonSerializer.Deserialize(json, VersionIndexContext.Default.BlobVersionIndexDocument)
               ?? throw new InvalidOperationException("Could not deserialize JSON of blob version index document.");
    }

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
public partial class VersionIndexContext : JsonSerializerContext
{
    
}
