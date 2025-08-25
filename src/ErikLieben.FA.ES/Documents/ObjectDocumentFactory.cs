using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Exceptions;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace ErikLieben.FA.ES.Documents;

public class ObjectDocumentFactory : IObjectDocumentFactory
{
    private readonly IDictionary<string, IObjectDocumentFactory> objectDocumentFactories;
    private readonly EventStreamDefaultTypeSettings settings;
    private readonly IDocumentTagDocumentFactory documentTagDocumentFactory;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES");

    public ObjectDocumentFactory(
        IDictionary<string, IObjectDocumentFactory> objectDocumentFactories,
        IDocumentTagDocumentFactory documentTagDocumentFactory,
        EventStreamDefaultTypeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(objectDocumentFactories);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(documentTagDocumentFactory);

        this.objectDocumentFactories = objectDocumentFactories;
        this.settings = settings;
        this.documentTagDocumentFactory = documentTagDocumentFactory;
    }

    public Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null)
    {
        using var activity = ActivitySource.StartActivity($"ObjectDocument.{nameof(GetAsync)}");
        ArgumentException.ThrowIfNullOrEmpty(objectName);
        ArgumentException.ThrowIfNullOrEmpty(objectId);
        
        store ??= settings.DocumentType.ToLowerInvariant();
        if (objectDocumentFactories.TryGetValue(store, out IObjectDocumentFactory? objectDocumentFactory))
        {
            return objectDocumentFactory.GetAsync(objectName, objectId);
        }

        throw new UnableToFindDocumentFactoryException(
            $"Unable to find store for DocumentType: {store}." +
            " Are you sure it's properly registered in the configuration?");
    }

    public Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null)
    {
        using var activity = ActivitySource.StartActivity($"ObjectDocument.{nameof(GetOrCreateAsync)}");
        activity?.AddTag("ObjectName", objectName);
        activity?.AddTag("ObjectId", objectId);
        ArgumentException.ThrowIfNullOrEmpty(objectName);
        ArgumentException.ThrowIfNullOrEmpty(objectId);

        store ??= settings.DocumentType.ToLowerInvariant();
        if (objectDocumentFactories.TryGetValue(store, out var objectDocumentFactory))
        {
            return objectDocumentFactory.GetOrCreateAsync(objectName, objectId);
        }

        throw new UnableToFindDocumentFactoryException(
            $"Unable to find store for DocumentType: {store}." +
            " Are you sure it's properly registered in the configuration?");
    }

    public async Task<IObjectDocument?> GetFirstByObjectDocumentTag(string objectName, string objectDocumentTag)
    {
        var documentTagStore = this.documentTagDocumentFactory.CreateDocumentTagStore(settings.DocumentTagType);
         var objectIds = (await documentTagStore.GetAsync(objectName, objectDocumentTag)).ToList();

         var objectId = objectIds.FirstOrDefault();
         if (string.IsNullOrWhiteSpace(objectId))
         {
             return null;
         }

         return await this.GetAsync(objectName, objectId);
    }

    public async Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(string objectName, string objectDocumentTag)
    {
        var documentTagStore = this.documentTagDocumentFactory.CreateDocumentTagStore(settings.DocumentTagType);
        var objectIds = (await documentTagStore.GetAsync(objectName, objectDocumentTag)).ToList();

        var documents = new List<IObjectDocument>();
        foreach (var objectId in objectIds.Where(objectId => !string.IsNullOrWhiteSpace(objectId)))
        {
            documents.Add(await this.GetAsync(objectName, objectId));
        }

        return documents;
    }

    public async Task SetAsync(IObjectDocument document, string? store = null)
    {
        using var activity = ActivitySource.StartActivity("ObjectDocument.SetAsync");
        ArgumentNullException.ThrowIfNull(document);

        store ??= settings.DocumentType.ToLowerInvariant();
        if (objectDocumentFactories.TryGetValue(store, out IObjectDocumentFactory? objectDocumentFactory))
        {
            await objectDocumentFactory.SetAsync(document);
        }
    }
}