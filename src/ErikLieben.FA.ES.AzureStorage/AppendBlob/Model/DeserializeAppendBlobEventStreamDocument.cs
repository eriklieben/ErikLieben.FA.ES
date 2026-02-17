using System.Text.Json.Serialization;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.AppendBlob.Model;

/// <summary>
/// Represents a mutable document used exclusively for JSON deserialization.
/// Uses <see cref="DeserializeStreamInformation"/> to read both legacy *ConnectionName
/// and new *Store properties for automatic migration support.
/// </summary>
public class DeserializeAppendBlobEventStreamDocument
{
    /// <summary>
    /// Gets or sets the object identifier of the event stream document.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object name for the event stream document.
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the active stream information (deserialization format with legacy support).
    /// </summary>
    public DeserializeStreamInformation Active { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of terminated streams.
    /// </summary>
    public List<TerminatedStream> TerminatedStreams { get; set; } = [];

    /// <summary>
    /// Gets or sets the schema version used to serialize the document.
    /// </summary>
    public string? SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the integrity hash of the document contents.
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Gets or sets the previous document hash.
    /// </summary>
    public string? PrevHash { get; set; }

    /// <summary>
    /// Gets or sets the blob path of the materialized document (set programmatically after loading, not from JSON).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? DocumentPath { get; set; }

    /// <summary>
    /// Converts this deserialization document to an <see cref="AppendBlobEventStreamDocument"/>,
    /// migrating legacy *ConnectionName values to new *Store properties.
    /// </summary>
    public AppendBlobEventStreamDocument ToAppendBlobEventStreamDocument()
    {
        return new AppendBlobEventStreamDocument(
            ObjectId,
            ObjectName,
            Active.ToStreamInformation(),
            TerminatedStreams,
            SchemaVersion,
            Hash,
            PrevHash);
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
    )]
[JsonSerializable(typeof(DeserializeAppendBlobEventStreamDocument))]
internal partial class DeserializeAppendBlobEventStreamDocumentContext : JsonSerializerContext
{
}
