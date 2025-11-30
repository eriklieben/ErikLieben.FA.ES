using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

/// <summary>
/// Represents the persisted event stream document stored in Azure Blob Storage for a single object.
/// </summary>
/// <remarks>
/// The document contains the sequence of events, termination state, schema version, and a hash used for
/// optimistic concurrency against the corresponding object document.
/// </remarks>
public class BlobDataStoreDocument
{
    /// <summary>
    /// Gets or sets the identifier of the object to which the event stream belongs.
    /// </summary>
    public required string ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the logical name/type of the object.
    /// </summary>
    public required string ObjectName { get; set; }

    /// <summary>
    /// Gets or sets the last known hash of the associated object document used for optimistic concurrency.
    /// </summary>
    public required string LastObjectDocumentHash { get; set; } = "*";

    /// <summary>
    /// Gets or sets a value indicating whether the stream has been terminated and should no longer accept events.
    /// </summary>
    public bool Terminated { get; set; } = false;

    /// <summary>
    /// Gets or sets the schema version of this document format.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the list of events in the stream in ascending version order.
    /// </summary>
    public List<BlobJsonEvent> Events { get; set; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull )]
[JsonSerializable(typeof(BlobDataStoreDocument))]
[JsonSerializable(typeof(BlobJsonEvent))]
internal partial class BlobDataStoreDocumentContext : JsonSerializerContext
{
}
