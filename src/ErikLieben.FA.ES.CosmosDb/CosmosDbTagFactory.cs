using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Creates CosmosDB-backed document and stream tag stores.
/// </summary>
public class CosmosDbTagFactory : IDocumentTagDocumentFactory
{
    private readonly EventStreamCosmosDbSettings cosmosDbSettings;
    private readonly CosmosClient cosmosClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbTagFactory"/> class.
    /// </summary>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="cosmosDbSettings">The CosmosDB settings controlling default stores and auto-creation.</param>
    public CosmosDbTagFactory(
        CosmosClient cosmosClient,
        EventStreamCosmosDbSettings cosmosDbSettings)
    {
        this.cosmosDbSettings = cosmosDbSettings;
        this.cosmosClient = cosmosClient;
    }

    /// <summary>
    /// Creates a document tag store for the specified object document using its configured tag type.
    /// </summary>
    /// <param name="document">The document whose tag configuration is used.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by CosmosDB.</returns>
    public IDocumentTagStore CreateDocumentTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.DocumentTagType);

        return new CosmosDbDocumentTagStore(cosmosClient, cosmosDbSettings);
    }

    /// <summary>
    /// Creates a document tag store using the default document tag type from settings.
    /// </summary>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by CosmosDB.</returns>
    public IDocumentTagStore CreateDocumentTagStore()
    {
        return new CosmosDbDocumentTagStore(cosmosClient, cosmosDbSettings);
    }

    /// <summary>
    /// Creates a document tag store for the specified tag provider type.
    /// </summary>
    /// <param name="type">The tag provider type (e.g., "cosmosdb").</param>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by the specified provider.</returns>
    public IDocumentTagStore CreateDocumentTagStore(string type)
    {
        return new CosmosDbDocumentTagStore(cosmosClient, cosmosDbSettings);
    }

    /// <summary>
    /// Creates a stream tag store for the specified document using the configured stream tag provider type.
    /// </summary>
    /// <param name="document">The document whose stream tag store is requested.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> for stream tags.</returns>
    public IDocumentTagStore CreateStreamTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.EventStreamTagType);

        return new CosmosDbStreamTagStore(cosmosClient, cosmosDbSettings);
    }

    /// <summary>
    /// Creates a stream tag store using the default stream tag type from settings.
    /// </summary>
    /// <returns>An <see cref="IDocumentTagStore"/> for stream tags.</returns>
    public IDocumentTagStore CreateStreamTagStore()
    {
        return new CosmosDbStreamTagStore(cosmosClient, cosmosDbSettings);
    }
}
