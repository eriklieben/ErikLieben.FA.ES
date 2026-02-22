using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Represents an Azure Table Storage-backed event stream for an object document.
/// </summary>
/// <param name="document">The object document with tagging capabilities associated with the stream.</param>
/// <param name="streamDependencies">The dependencies used by the stream (data store, snapshot store, factories).</param>
public class TableEventStream(
    IObjectDocumentWithMethods document,
    IStreamDependencies streamDependencies) : BaseEventStream(document, streamDependencies)
{
}
