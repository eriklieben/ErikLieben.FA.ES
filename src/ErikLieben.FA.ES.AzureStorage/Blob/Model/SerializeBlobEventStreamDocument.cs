using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

/// <summary>
/// Document model used for serialization that uses <see cref="SerializeStreamInformation"/>
/// to exclude legacy *ConnectionName properties from the output.
/// </summary>
public class SerializeBlobEventStreamDocument
{
    /// <summary>
    /// Gets or sets the object identifier.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object name/type.
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the active stream information (serialization format).
    /// </summary>
    public SerializeStreamInformation Active { get; set; } = new();

    /// <summary>
    /// Gets or sets the terminated streams.
    /// </summary>
    public List<TerminatedStream> TerminatedStreams { get; set; } = [];

    /// <summary>
    /// Gets or sets the schema version.
    /// </summary>
    public string? SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the integrity hash.
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Gets or sets the previous hash.
    /// </summary>
    public string? PrevHash { get; set; }

    /// <summary>
    /// Creates a serialization document from an <see cref="IObjectDocument"/>.
    /// Converts <see cref="StreamInformation"/> to <see cref="SerializeStreamInformation"/>
    /// to exclude legacy properties.
    /// </summary>
    public static SerializeBlobEventStreamDocument From(IObjectDocument source)
    {
        return new SerializeBlobEventStreamDocument
        {
            ObjectId = source.ObjectId,
            ObjectName = source.ObjectName,
            Active = SerializeStreamInformation.From(source.Active),
            TerminatedStreams = source.TerminatedStreams.ToList(),
            SchemaVersion = source.SchemaVersion,
            Hash = source.Hash,
            PrevHash = source.PrevHash,
        };
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
    )]
[JsonSerializable(typeof(SerializeBlobEventStreamDocument))]
internal partial class SerializeBlobEventStreamDocumentContext : JsonSerializerContext
{
}
