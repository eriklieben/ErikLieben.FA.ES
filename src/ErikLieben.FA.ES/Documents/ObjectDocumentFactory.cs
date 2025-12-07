using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Exceptions;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Resolves and delegates object document operations to the appropriate provider based on configured defaults or explicit store keys.
/// </summary>
public class ObjectDocumentFactory : IObjectDocumentFactory
{
    private readonly IDictionary<string, IObjectDocumentFactory> objectDocumentFactories;
    private readonly EventStreamDefaultTypeSettings settings;
    private readonly IDocumentTagDocumentFactory documentTagDocumentFactory;
    private readonly Configuration.IAggregateStorageRegistry? aggregateStorageRegistry;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES");

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectDocumentFactory"/> class.
    /// </summary>
    /// <param name="objectDocumentFactories">A keyed collection of underlying factories by store type.</param>
    /// <param name="documentTagDocumentFactory">The factory used to create document tag stores.</param>
    /// <param name="settings">Default type settings used when resolving factories.</param>
    /// <param name="aggregateStorageRegistry">Optional registry mapping aggregate names to storage connections for cross-storage projections.</param>
    public ObjectDocumentFactory(
        IDictionary<string, IObjectDocumentFactory> objectDocumentFactories,
        IDocumentTagDocumentFactory documentTagDocumentFactory,
        EventStreamDefaultTypeSettings settings,
        Configuration.IAggregateStorageRegistry? aggregateStorageRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(objectDocumentFactories);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(documentTagDocumentFactory);

        this.objectDocumentFactories = objectDocumentFactories;
        this.settings = settings;
        this.documentTagDocumentFactory = documentTagDocumentFactory;
        this.aggregateStorageRegistry = aggregateStorageRegistry;
    }

    /// <summary>
    /// Retrieves an existing object document using the appropriate provider.
    /// </summary>
    /// <param name="objectName">The object type/name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="store">An optional named connection (e.g., "Store2") to pass through to the underlying provider.</param>
    /// <param name="documentType">An optional document type to override the default factory selection (e.g., "table", "blob").</param>
    public Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null, string? documentType = null)
    {
        using var activity = ActivitySource.StartActivity($"ObjectDocument.{nameof(GetAsync)}");
        ArgumentException.ThrowIfNullOrEmpty(objectName);
        ArgumentException.ThrowIfNullOrEmpty(objectId);

        // Resolve storage account from registry if not explicitly provided
        var targetStore = store ?? aggregateStorageRegistry?.GetStorageForAggregate(objectName);

        var factoryType = (documentType ?? settings.DocumentType).ToLowerInvariant();
        if (objectDocumentFactories.TryGetValue(factoryType, out IObjectDocumentFactory? objectDocumentFactory))
        {
            return objectDocumentFactory.GetAsync(objectName, objectId, targetStore);
        }

        throw new UnableToFindDocumentFactoryException(
            $"Unable to find store for DocumentType: {factoryType}." +
            " Are you sure it's properly registered in the configuration?");
    }

    /// <summary>
    /// Retrieves an object document, or creates a new one when missing, using the appropriate provider.
    /// </summary>
    /// <param name="objectName">The object type/name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="store">An optional named connection (e.g., "Store2") to pass through to the underlying provider.</param>
    /// <param name="documentType">An optional document type to override the default factory selection (e.g., "table", "blob").</param>
    public Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null, string? documentType = null)
    {
        using var activity = ActivitySource.StartActivity($"ObjectDocument.{nameof(GetOrCreateAsync)}");
        activity?.AddTag("ObjectName", objectName);
        activity?.AddTag("ObjectId", objectId);
        ArgumentException.ThrowIfNullOrEmpty(objectName);
        ArgumentException.ThrowIfNullOrEmpty(objectId);

        // Resolve storage account from registry if not explicitly provided
        var targetStore = store ?? aggregateStorageRegistry?.GetStorageForAggregate(objectName);

        var factoryType = (documentType ?? settings.DocumentType).ToLowerInvariant();
        if (objectDocumentFactories.TryGetValue(factoryType, out var objectDocumentFactory))
        {
            return objectDocumentFactory.GetOrCreateAsync(objectName, objectId, targetStore);
        }

        throw new UnableToFindDocumentFactoryException(
            $"Unable to find store for DocumentType: {factoryType}." +
            " Are you sure it's properly registered in the configuration?");
    }

    /// <summary>
    /// Gets the first object document that has the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the document. If not provided, uses the default document store.</param>
    /// <returns>The first matching document or null when none is found.</returns>
    public async Task<IObjectDocument?> GetFirstByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null)
    {
        var targetDocumentTagStore = documentTagStore ?? settings.DocumentTagType;
        var documentTagStoreInstance = this.documentTagDocumentFactory.CreateDocumentTagStore(targetDocumentTagStore);
         var objectIds = (await documentTagStoreInstance.GetAsync(objectName, objectDocumentTag)).ToList();

         var objectId = objectIds.FirstOrDefault();
         if (string.IsNullOrWhiteSpace(objectId))
         {
             return null;
         }

         return await this.GetAsync(objectName, objectId, store);
    }

    /// <summary>
    /// Gets all object documents that have the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the documents. If not provided, uses the default document store.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    public async Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null)
    {
        var targetDocumentTagStore = documentTagStore ?? settings.DocumentTagType;
        var documentTagStoreInstance = this.documentTagDocumentFactory.CreateDocumentTagStore(targetDocumentTagStore);
        var objectIds = (await documentTagStoreInstance.GetAsync(objectName, objectDocumentTag)).ToList();

        var documents = new List<IObjectDocument>();
        foreach (var objectId in objectIds.Where(objectId => !string.IsNullOrWhiteSpace(objectId)))
        {
            documents.Add(await this.GetAsync(objectName, objectId, store));
        }

        return documents;
    }

    /// <summary>
    /// Persists the provided object document using the appropriate provider.
    /// </summary>
    /// <param name="document">The object document to persist.</param>
    /// <param name="store">An optional named connection (e.g., "Store2") to pass through to the underlying provider.</param>
    /// <param name="documentType">An optional document type to override the default factory selection (e.g., "table", "blob").</param>
    public async Task SetAsync(IObjectDocument document, string? store = null, string? documentType = null)
    {
        using var activity = ActivitySource.StartActivity("ObjectDocument.SetAsync");
        ArgumentNullException.ThrowIfNull(document);

        // Use document's own DocumentType if set, otherwise use parameter or default
        var factoryType = (documentType ?? document.Active.DocumentType ?? settings.DocumentType).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(factoryType))
        {
            factoryType = settings.DocumentType.ToLowerInvariant();
        }

        if (objectDocumentFactories.TryGetValue(factoryType, out IObjectDocumentFactory? objectDocumentFactory))
        {
            await objectDocumentFactory.SetAsync(document, store);
        }
        else
        {
            throw new UnableToFindDocumentFactoryException(
                $"Unable to find store for DocumentType: {factoryType}." +
                " Are you sure it's properly registered in the configuration?");
        }
    }
}
