using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.S3.Model;

/// <summary>
/// Represents the persisted event stream document stored in S3 for a single object.
/// </summary>
public class S3DataStoreDocument
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
    public List<S3JsonEvent> Events { get; set; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(S3DataStoreDocument))]
[JsonSerializable(typeof(S3JsonEvent))]
internal partial class S3DataStoreDocumentContext : JsonSerializerContext
{
}
