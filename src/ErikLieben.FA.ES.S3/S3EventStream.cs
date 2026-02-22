using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// Represents an S3-backed event stream for an object document.
/// </summary>
/// <param name="document">The object document with tagging capabilities associated with the stream.</param>
/// <param name="streamDependencies">The dependencies used by the stream (data store, snapshot store, factories).</param>
public class S3EventStream(
    IObjectDocumentWithMethods document,
    IStreamDependencies streamDependencies) : BaseEventStream(document, streamDependencies)
{
}
