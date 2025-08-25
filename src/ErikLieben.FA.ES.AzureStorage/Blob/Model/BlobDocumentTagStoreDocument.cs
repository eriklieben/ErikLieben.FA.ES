using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

public class BlobDocumentTagStoreDocument
{
    public List<string> ObjectIds { get; set; } = [];

    public required string Tag { get; set; }
    
    public string SchemaVersion { get; set; } = "1.0.0";
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BlobDocumentTagStoreDocument))]
internal partial class BlobDocumentTagStoreDocumentContext : JsonSerializerContext
{
}
