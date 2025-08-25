using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Testing.InMemory.Model;

public class InMemoryDataStoreDocument
{
    public required string ObjectId { get; set; }
    public required string ObjectName { get; set; }

    public required string LastObjectDocumentHash { get; set; } = "*";

    public bool Terminated { get; set; } = false;

    public string SchemaVersion { get; set; } = "1.0.0";

    public List<JsonEvent> Events { get; set; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull )]
[JsonSerializable(typeof(InMemoryDataStoreDocument))]
internal partial class BlobDataStoreDocumenContext : JsonSerializerContext
{
}
