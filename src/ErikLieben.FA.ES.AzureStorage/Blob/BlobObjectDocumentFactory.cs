using System.Diagnostics;
using System.Linq;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Provides an Azure Blob Storage-backed implementation of <see cref="IObjectDocumentFactory"/>.
/// </summary>
public class BlobObjectDocumentFactory : IObjectDocumentFactory
{
    private readonly IBlobDocumentStore blobDocumentStore;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.AzureStorage");

    /// <summary>
/// Initializes a new instance of the <see cref="BlobObjectDocumentFactory"/> class using Azure services and settings.
/// </summary>
/// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
/// <param name="documentTagStore">The factory used to access document tag storage.</param>
/// <param name="settings">The default event stream type settings.</param>
/// <param name="blobSettings">The blob storage settings used for containers and chunking.</param>
public BlobObjectDocumentFactory(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        IDocumentTagDocumentFactory documentTagStore,
        EventStreamDefaultTypeSettings settings,
        EventStreamBlobSettings blobSettings)
    {
        blobDocumentStore = new BlobDocumentStore(clientFactory, documentTagStore, blobSettings, settings);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobObjectDocumentFactory"/> class using a pre-configured <see cref="IBlobDocumentStore"/>.
    /// </summary>
    /// <param name="blobDocumentStore">The blob document store to delegate operations to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blobDocumentStore"/> is null.</exception>
    public BlobObjectDocumentFactory(IBlobDocumentStore blobDocumentStore)
    {
        ArgumentNullException.ThrowIfNull(blobDocumentStore);
        this.blobDocumentStore = blobDocumentStore;
    }

    /// <summary>
    /// Retrieves an object document or creates a new one when it does not exist.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to retrieve or create.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <returns>The existing or newly created <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(GetOrCreateAsync)}");
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectId);
        var result = await blobDocumentStore.CreateAsync(objectName!.ToLowerInvariant(), objectId!);
        if (result is null)
        {
            throw new InvalidOperationException("BlobDocumentStore.CreateAsync returned null document.");
        }
        return result;
    }

    /// <summary>
    /// Retrieves an existing object document from Azure Blob Storage.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <returns>The loaded <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(GetAsync)}");
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectId);
        var result = await blobDocumentStore.GetAsync(objectName.ToLowerInvariant(), objectId);
        if (result is null)
        {
            throw new InvalidOperationException("BlobDocumentStore.GetAsync returned null document.");
        }
        return result;
    }

    /// <summary>
    /// Retrieves all object documents that have the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    public async Task<IEnumerable<IObjectDocument>> GetByDocumentTagAsync(string objectName, string objectDocumentTag)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(GetByDocumentTagAsync)}");
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectDocumentTag);
        return (await blobDocumentStore.GetByDocumentByTagAsync(objectName, objectDocumentTag))
               ?? Enumerable.Empty<IObjectDocument>();
    }

    /// <summary>
    /// Gets the first object document that has the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <returns>The first matching document or null when none is found.</returns>
    public Task<IObjectDocument?> GetFirstByObjectDocumentTag(string objectName, string objectDocumentTag)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(GetByDocumentTagAsync)}");
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectDocumentTag);
        return blobDocumentStore.GetFirstByDocumentByTagAsync(objectName, objectDocumentTag);
    }

    /// <summary>
    /// Gets all object documents that have the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    public async Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(string objectName, string objectDocumentTag)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(GetByDocumentTagAsync)}");
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectDocumentTag);
        return (await blobDocumentStore.GetByDocumentByTagAsync(objectName, objectDocumentTag))
               ?? Enumerable.Empty<IObjectDocument>();
    }

    /// <summary>
    /// Persists the provided object document to Azure Blob Storage.
    /// </summary>
    /// <param name="document">The object document to save.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public Task SetAsync(IObjectDocument document, string? store = null)
    {
        using var activity = ActivitySource.StartActivity($"BlobObjectDocumentFactory.{nameof(SetAsync)}");
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfNull(document);
        return blobDocumentStore.SetAsync(document);
    }
}
