using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.Testing.InMemory;

public class InMemoryEventStreamFactory : IEventStreamFactory
{
    private readonly IDocumentTagDocumentFactory documentTagFactory;
    private readonly IObjectDocumentFactory objectDocumentFactory;
    private readonly InMemoryDataStore dataSource;
    private readonly IAggregateFactory aggregateFactory;

    public InMemoryEventStreamFactory(
        IDocumentTagDocumentFactory documentTagFactory,
        IObjectDocumentFactory objectDocumentFactory,
        InMemoryDataStore dataSource,
        IAggregateFactory aggregateFactory)
    {
        ArgumentNullException.ThrowIfNull(documentTagFactory);
        ArgumentNullException.ThrowIfNull(objectDocumentFactory);
        ArgumentNullException.ThrowIfNull(aggregateFactory);
        ArgumentNullException.ThrowIfNull(dataSource);

        this.documentTagFactory = documentTagFactory;
        this.objectDocumentFactory = objectDocumentFactory;
        this.dataSource = dataSource;
        this.aggregateFactory = aggregateFactory;
    }

    public IEventStream Create(IObjectDocument document)
    {
        if (document.Active.StreamType == "default")
        {
            document.Active.StreamType = "inMemory";
        }

        var documentTagStore = documentTagFactory.CreateDocumentTagStore(document);

        return new InMemoryStream(
            new ObjectDocumentWithTags(document, documentTagStore),
            dataSource,
            new InMemorySnapShotStore(),
            objectDocumentFactory,
            aggregateFactory);
    }
}
