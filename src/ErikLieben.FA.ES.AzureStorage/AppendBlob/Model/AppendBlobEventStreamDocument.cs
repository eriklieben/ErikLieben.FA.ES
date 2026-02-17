using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.AppendBlob.Model;

/// <summary>
/// Represents an object document enriched with Azure Append Blob Storage specific metadata used by the event stream.
/// </summary>
public class AppendBlobEventStreamDocument : ObjectDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppendBlobEventStreamDocument"/> class.
    /// </summary>
    /// <param name="objectId">The identifier of the object.</param>
    /// <param name="objectName">The logical name/type of the object.</param>
    /// <param name="active">The active stream information.</param>
    /// <param name="terminatedStreams">The terminated streams for the object.</param>
    /// <param name="schemaVersion">The schema version of the document.</param>
    /// <param name="hash">The current hash used for optimistic concurrency.</param>
    /// <param name="prevHash">The previous hash used for optimistic concurrency.</param>
    /// <param name="documentPath">The blob path of the materialized document, if known.</param>
    public AppendBlobEventStreamDocument(
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

    /// <summary>
    /// Gets or sets the blob path of the materialized document used primarily by storage operations.
    /// </summary>
    [JsonIgnore]
    public string? DocumentPath { get; set; }

    /// <summary>
    /// Creates an <see cref="AppendBlobEventStreamDocument"/> from an existing <see cref="IObjectDocument"/> instance, copying common metadata.
    /// </summary>
    /// <param name="objectDocument">The source object document.</param>
    /// <returns>A new <see cref="AppendBlobEventStreamDocument"/> with values copied from <paramref name="objectDocument"/>.</returns>
    public static AppendBlobEventStreamDocument From(IObjectDocument objectDocument)
    {
        return new AppendBlobEventStreamDocument(
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
[JsonSerializable(typeof(AppendBlobEventStreamDocument))]
internal partial class AppendBlobEventStreamDocumentContext : JsonSerializerContext
{
}
