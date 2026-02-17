using Azure.Storage.Blobs;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.AppendBlob;

/// <summary>
/// Creates Azure Append Blob Storage-backed event streams for object documents.
/// </summary>
public class AppendBlobEventStreamFactory : IEventStreamFactory
{
    private readonly EventStreamAppendBlobSettings settings;
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly IDocumentTagDocumentFactory documentTagFactory;
    private readonly IObjectDocumentFactory objectDocumentFactory;
    private readonly IAggregateFactory aggregateFactory;

    /// <inheritdoc cref="Blob.BlobEventStreamFactory(EventStreamBlobSettings, IAzureClientFactory{BlobServiceClient}, IDocumentTagDocumentFactory, IObjectDocumentFactory, IAggregateFactory)"/>
    public AppendBlobEventStreamFactory(
        EventStreamAppendBlobSettings settings,
        IAzureClientFactory<BlobServiceClient> clientFactory,
        IDocumentTagDocumentFactory documentTagFactory,
        IObjectDocumentFactory objectDocumentFactory,
        IAggregateFactory aggregateFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(documentTagFactory);
        ArgumentNullException.ThrowIfNull(objectDocumentFactory);
        ArgumentNullException.ThrowIfNull(aggregateFactory);

        this.settings = settings;
        this.clientFactory = clientFactory;
        this.documentTagFactory = documentTagFactory;
        this.objectDocumentFactory = objectDocumentFactory;
        this.aggregateFactory = aggregateFactory;
    }

    /// <inheritdoc />
    public IEventStream Create(IObjectDocument document)
    {
        if (document.Active.StreamType == "default")
        {
            document.Active.StreamType = settings.DefaultDataStore;
        }

        var documentTagStore = documentTagFactory.CreateDocumentTagStore(document);
        var streamTagStore = documentTagFactory.CreateStreamTagStore(document);

        return new AppendBlobEventStream(
            new ObjectDocumentWithTags(document, documentTagStore, streamTagStore),
            new StreamDependencies
            {
                AggregateFactory = aggregateFactory,
                DataStore = new AppendBlobDataStore(clientFactory, settings.AutoCreateContainer),
                SnapshotStore = new AppendBlobSnapShotStore(clientFactory, settings),
                ObjectDocumentFactory = objectDocumentFactory,
            });
    }
}
