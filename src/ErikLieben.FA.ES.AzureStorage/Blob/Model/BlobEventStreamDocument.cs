using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

public class BlobEventStreamDocument : ObjectDocument
{
    public BlobEventStreamDocument(
        string objectId, 
        string objectName, 
        StreamInformation active, 
        IEnumerable<TerminatedStream> terminatedStreams, 
        string? schemaVersion = null,
        string? hash = null,
        string? prevHash = null,
        string? documentPath = null) : base(objectId, objectName, active, terminatedStreams, schemaVersion, hash, prevHash)
    {
        DocumentPath = documentPath;
    }

    [JsonIgnore]
    public string? DocumentPath { get; set; }

    public static BlobEventStreamDocument From(IObjectDocument objectDocument)
    {
        return new BlobEventStreamDocument(
            objectDocument.ObjectId,
            objectDocument.ObjectName,
            objectDocument.Active,
            objectDocument.TerminatedStreams,
            objectDocument.SchemaVersion,
            objectDocument.Hash,
            objectDocument.PrevHash);
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
    )]
[JsonSerializable(typeof(BlobEventStreamDocument))]
internal partial class BlobEventStreamDocumentContext : JsonSerializerContext
{
}


