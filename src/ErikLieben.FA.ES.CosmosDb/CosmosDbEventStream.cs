using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Provides a CosmosDB-backed event stream implementation.
/// </summary>
public class CosmosDbEventStream(
    IObjectDocumentWithMethods document,
    IStreamDependencies streamDependencies) : BaseEventStream(document, streamDependencies)
{
}
