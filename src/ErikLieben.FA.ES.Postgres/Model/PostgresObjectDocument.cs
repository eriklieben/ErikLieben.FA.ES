using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Postgres.Model;

/// <summary>
/// Concrete <see cref="ObjectDocument"/> for Postgres. The provider stores the entire document
/// content under the (object_name, object_id) primary key; <see cref="ObjectDocument.Hash"/> and
/// <see cref="ObjectDocument.PrevHash"/> hold the row's <c>xmin</c> from the last read so SetAsync
/// can do a CAS update.
/// </summary>
internal sealed class PostgresObjectDocument : ObjectDocument
{
    public PostgresObjectDocument(
        string objectId,
        string objectName,
        StreamInformation active,
        IEnumerable<TerminatedStream> terminatedStreams,
        string? schemaVersion = null,
        string? hash = null,
        string? prevHash = null)
        : base(objectId, objectName, active, terminatedStreams, schemaVersion, hash, prevHash)
    {
    }
}
