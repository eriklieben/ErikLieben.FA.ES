using System.Diagnostics;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb;

#pragma warning disable CS8602 // Dereference of possibly null reference - cosmosDbDocumentStore is always initialized in constructors
/// <summary>
/// Provides a CosmosDB-backed implementation of <see cref="IObjectDocumentFactory"/>.
/// </summary>
public class CosmosDbObjectDocumentFactory : IObjectDocumentFactory
{
    private readonly ICosmosDbDocumentStore cosmosDbDocumentStore;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.CosmosDb");

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbObjectDocumentFactory"/> class using Azure services and settings.
    /// </summary>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="documentTagStore">The factory used to access document tag storage.</param>
    /// <param name="settings">The default event stream type settings.</param>
    /// <param name="cosmosDbSettings">The CosmosDB settings used for containers.</param>
    public CosmosDbObjectDocumentFactory(
        CosmosClient cosmosClient,
        IDocumentTagDocumentFactory documentTagStore,
        EventStreamDefaultTypeSettings settings,
        EventStreamCosmosDbSettings cosmosDbSettings)
    {
        this.cosmosDbDocumentStore = new CosmosDbDocumentStore(cosmosClient, documentTagStore, cosmosDbSettings);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbObjectDocumentFactory"/> class using a pre-configured <see cref="ICosmosDbDocumentStore"/>.
    /// </summary>
    /// <param name="cosmosDbDocumentStore">The CosmosDB document store to delegate operations to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cosmosDbDocumentStore"/> is null.</exception>
    public CosmosDbObjectDocumentFactory(ICosmosDbDocumentStore cosmosDbDocumentStore)
    {
        ArgumentNullException.ThrowIfNull(cosmosDbDocumentStore);
        this.cosmosDbDocumentStore = cosmosDbDocumentStore;
    }

    /// <summary>
    /// Retrieves an object document or creates a new one when it does not exist.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the partition key.</param>
    /// <param name="objectId">The identifier of the object to retrieve or create.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <param name="documentType">Ignored for CosmosDbObjectDocumentFactory (already cosmosdb-specific).</param>
    /// <returns>The existing or newly created <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null, string? documentType = null)
    {
        Console.WriteLine($"[COSMOSDB-FACTORY] GetOrCreateAsync: objectName={objectName}, objectId={objectId}, store={store}, documentType={documentType}");
        using var activity = ActivitySource.StartActivity($"CosmosDbObjectDocumentFactory.{nameof(GetOrCreateAsync)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        var objectNameLower = objectName.ToLowerInvariant();
        var result = await this.cosmosDbDocumentStore!.CreateAsync(objectNameLower, objectId, store);

        if (result is null)
        {
            throw new InvalidOperationException("CosmosDbDocumentStore.CreateAsync returned null document.");
        }

        Console.WriteLine($"[COSMOSDB-FACTORY] Created document: StreamIdentifier={result.Active.StreamIdentifier}, DocumentType={result.Active.DocumentType}, StreamType={result.Active.StreamType}");
        return result;
    }

    /// <summary>
    /// Retrieves an existing object document from CosmosDB.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the partition key.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <param name="documentType">Ignored for CosmosDbObjectDocumentFactory (already cosmosdb-specific).</param>
    /// <returns>The loaded <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null, string? documentType = null)
    {
        using var activity = ActivitySource.StartActivity($"CosmosDbObjectDocumentFactory.{nameof(GetAsync)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        var result = await this.cosmosDbDocumentStore!.GetAsync(objectName!.ToLowerInvariant(), objectId!, store);
        if (result is null)
        {
            throw new InvalidOperationException("CosmosDbDocumentStore.GetAsync returned null document.");
        }

        return result;
    }

    /// <summary>
    /// Gets the first object document that has the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the document. If not provided, uses the default document store.</param>
    /// <returns>The first matching document or null when none is found.</returns>
    public Task<IObjectDocument?> GetFirstByObjectDocumentTag(
        string objectName,
        string objectDocumentTag,
        string? documentTagStore = null,
        string? store = null)
    {
        using var activity = ActivitySource.StartActivity($"CosmosDbObjectDocumentFactory.{nameof(GetFirstByObjectDocumentTag)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectDocumentTag);

        return cosmosDbDocumentStore.GetFirstByDocumentByTagAsync(objectName, objectDocumentTag, documentTagStore, store);
    }

    /// <summary>
    /// Gets all object documents that have the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the documents. If not provided, uses the default document store.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    public async Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(
        string objectName,
        string objectDocumentTag,
        string? documentTagStore = null,
        string? store = null)
    {
        using var activity = ActivitySource.StartActivity($"CosmosDbObjectDocumentFactory.{nameof(GetByObjectDocumentTag)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectDocumentTag);

        return (await cosmosDbDocumentStore.GetByDocumentByTagAsync(objectName, objectDocumentTag, documentTagStore, store))
               ?? [];
    }

    /// <summary>
    /// Persists the provided object document to CosmosDB.
    /// </summary>
    /// <param name="document">The object document to save.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <param name="documentType">Ignored for CosmosDbObjectDocumentFactory (already cosmosdb-specific).</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public Task SetAsync(IObjectDocument document, string? store = null, string? documentType = null)
    {
        using var activity = ActivitySource.StartActivity($"CosmosDbObjectDocumentFactory.{nameof(SetAsync)}");
        ArgumentNullException.ThrowIfNull(document);

        return cosmosDbDocumentStore.SetAsync(document);
    }
}
