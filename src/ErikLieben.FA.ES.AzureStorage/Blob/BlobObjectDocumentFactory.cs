using System.Diagnostics;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

public class BlobObjectDocumentFactory : IObjectDocumentFactory
{
    private readonly IBlobDocumentStore blobDocumentStore;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.AzureStorage");

    public BlobObjectDocumentFactory(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        IDocumentTagDocumentFactory documentTagStore,
        EventStreamDefaultTypeSettings settings,
        EventStreamBlobSettings blobSettings)
    {
        blobDocumentStore = new BlobDocumentStore(clientFactory, documentTagStore, blobSettings, settings);
    }

    public BlobObjectDocumentFactory(IBlobDocumentStore blobDocumentStore)
    {
        this.blobDocumentStore = blobDocumentStore;
    }

    public Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(GetOrCreateAsync)}");
        return blobDocumentStore.CreateAsync(objectName.ToLowerInvariant(), objectId);
    }

    public Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(GetAsync)}");
        return blobDocumentStore.GetAsync(objectName.ToLowerInvariant(), objectId);
    }

    public async Task<IEnumerable<IObjectDocument>> GetByDocumentTagAsync(string objectName, string objectDocumentTag)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(GetByDocumentTagAsync)}");
        return await blobDocumentStore.GetByDocumentByTagAsync(objectName, objectDocumentTag);
    }

    public Task<IObjectDocument?> GetFirstByObjectDocumentTag(string objectName, string objectDocumentTag)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(GetByDocumentTagAsync)}");
        return blobDocumentStore.GetFirstByDocumentByTagAsync(objectName, objectDocumentTag);
    }

    public Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(string objectName, string objectDocumentTag)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(GetByDocumentTagAsync)}");
        return blobDocumentStore.GetByDocumentByTagAsync(objectName, objectDocumentTag);
    }

    public Task SetAsync(IObjectDocument document, string? store = null!)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(SetAsync)}");
        return blobDocumentStore.SetAsync(document);
    }
}
