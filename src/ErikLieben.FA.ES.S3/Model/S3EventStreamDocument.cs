using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.S3.Model;

/// <summary>
/// Represents an object document enriched with S3 storage specific metadata used by the event stream.
/// </summary>
public class S3EventStreamDocument : ObjectDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="S3EventStreamDocument"/> class.
    /// </summary>
    public S3EventStreamDocument(
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
    /// Gets or sets the S3 key of the materialized document used primarily by storage operations.
    /// </summary>
    [JsonIgnore]
    public string? DocumentPath { get; set; }

    /// <summary>
    /// Creates an <see cref="S3EventStreamDocument"/> from an existing <see cref="IObjectDocument"/> instance.
    /// </summary>
    public static S3EventStreamDocument From(IObjectDocument objectDocument)
    {
        return new S3EventStreamDocument(
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
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(S3EventStreamDocument))]
internal partial class S3EventStreamDocumentContext : JsonSerializerContext
{
}
