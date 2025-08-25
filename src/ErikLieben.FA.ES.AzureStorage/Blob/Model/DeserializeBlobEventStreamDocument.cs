using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

public class DeserializeBlobEventStreamDocument() : BlobEventStreamDocument(string.Empty, string.Empty, new StreamInformation(), [])
{
    public new string ObjectId { get; set; } = string.Empty;

    public new string ObjectName { get; set; } = string.Empty;

    public new StreamInformation Active { get; set; } = null!;

    public new List<TerminatedStream> TerminatedStreams { get; set; } = new List<TerminatedStream>();

    public new string? SchemaVersion { get; set; }

    public new string? Hash { get; set; }

    public new string? PrevHash { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
    )]
[JsonSerializable(typeof(DeserializeBlobEventStreamDocument))]
internal partial class DeserializeBlobEventStreamDocumentContext : JsonSerializerContext
{
}
