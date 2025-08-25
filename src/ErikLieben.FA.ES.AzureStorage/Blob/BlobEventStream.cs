using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

public class BlobEventStream(
    IObjectDocumentWithMethods document,
    IStreamDependencies streamDependencies) : BaseEventStream(document, streamDependencies)
{
}
