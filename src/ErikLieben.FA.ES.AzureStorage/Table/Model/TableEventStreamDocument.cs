using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.Table.Model;

/// <summary>
/// Represents an object document for Azure Table Storage that wraps stream metadata.
/// </summary>
public class TableEventStreamDocument : ObjectDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableEventStreamDocument"/> class.
    /// </summary>
    /// <param name="objectId">The identifier of the object.</param>
    /// <param name="objectName">The logical name/type of the object.</param>
    /// <param name="active">The active stream information.</param>
    /// <param name="terminatedStreams">The terminated streams for the object.</param>
    /// <param name="schemaVersion">The schema version of the document.</param>
    /// <param name="hash">The current hash used for optimistic concurrency.</param>
    /// <param name="prevHash">The previous hash used for optimistic concurrency.</param>
    public TableEventStreamDocument(
        string objectId,
        string objectName,
        StreamInformation active,
        IEnumerable<TerminatedStream> terminatedStreams,
        string? schemaVersion = null,
        string? hash = null,
        string? prevHash = null) : base(objectId, objectName, active, terminatedStreams, schemaVersion, hash, prevHash)
    {
    }

    /// <summary>
    /// Creates a <see cref="TableEventStreamDocument"/> from an existing <see cref="IObjectDocument"/> instance, copying common metadata.
    /// </summary>
    /// <param name="objectDocument">The source object document.</param>
    /// <returns>A new <see cref="TableEventStreamDocument"/> with values copied from <paramref name="objectDocument"/>.</returns>
    public static TableEventStreamDocument From(IObjectDocument objectDocument)
    {
        return new TableEventStreamDocument(
            objectDocument.ObjectId,
            objectDocument.ObjectName,
            objectDocument.Active,
            objectDocument.TerminatedStreams,
            objectDocument.SchemaVersion,
            objectDocument.Hash,
            objectDocument.PrevHash);
    }
}
