using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

public class BlobDataStoreDocument
{
    public required string ObjectId { get; set; }
    public required string ObjectName { get; set; }

    public required string LastObjectDocumentHash { get; set; } = "*";

    public bool Terminated { get; set; } = false;

    public string SchemaVersion { get; set; } = "1.0.0";

    public List<BlobJsonEvent> Events { get; set; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull )]
[JsonSerializable(typeof(BlobDataStoreDocument))]
internal partial class BlobDataStoreDocumentContext : JsonSerializerContext
{
}
