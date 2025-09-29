using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

/// <summary>
/// Represents a mutable version of <see cref="BlobEventStreamDocument"/> used exclusively for JSON deserialization.
/// </summary>
/// <remarks>
/// The base <see cref="BlobEventStreamDocument"/> exposes init-only or non-nullable members. This type provides
/// settable properties to allow the System.Text.Json source generator to materialize instances from storage.
/// </remarks>
public class DeserializeBlobEventStreamDocument() : BlobEventStreamDocument(string.Empty, string.Empty, new StreamInformation(), [])
{
    /// <summary>
    /// Gets or sets the object identifier of the event stream document.
    /// </summary>
    public new string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object name for the event stream document.
    /// </summary>
    public new string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the active stream information.
    /// </summary>
    public new StreamInformation Active { get; set; } = null!;

    /// <summary>
    /// Gets or sets the list of terminated streams.
    /// </summary>
    public new List<TerminatedStream> TerminatedStreams { get; set; } = new List<TerminatedStream>();

    /// <summary>
    /// Gets or sets the schema version used to serialize the document.
    /// </summary>
    public new string? SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the integrity hash of the document contents.
    /// </summary>
    public new string? Hash { get; set; }

    /// <summary>
    /// Gets or sets the previous document hash.
    /// </summary>
    public new string? PrevHash { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
    )]
[JsonSerializable(typeof(DeserializeBlobEventStreamDocument))]
internal partial class DeserializeBlobEventStreamDocumentContext : JsonSerializerContext
{
}
