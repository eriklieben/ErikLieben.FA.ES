using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Testing.InMemory.Model;


public class InMemoryEventStreamDocument : ObjectDocument
{
    public InMemoryEventStreamDocument(
        string objectId, 
        string objectName,
        StreamInformation active,
        IEnumerable<TerminatedStream> terminatedStream, 
        string? schemaVersion = null,
        string? hash = null,
        string? prevHash = null,
        string? documentPath = null) : base(objectId, objectName, active, terminatedStream, schemaVersion, hash, prevHash)
    {
        DocumentPath = documentPath;
    }

    [JsonIgnore]
    public string? DocumentPath { get; set; }

    public static InMemoryEventStreamDocument From(IObjectDocument objectDocument)
    {
        return new InMemoryEventStreamDocument(
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
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase )]
[JsonSerializable(typeof(InMemoryEventStreamDocument))]
internal partial class InMemoryEventStreamDocumenContext : JsonSerializerContext
{
}
