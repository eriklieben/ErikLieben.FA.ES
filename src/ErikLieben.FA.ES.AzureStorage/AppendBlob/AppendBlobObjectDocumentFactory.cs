using System.Diagnostics;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.AppendBlob;

#pragma warning disable CS8602 // Dereference of possibly null reference - appendBlobDocumentStore is always initialized in constructors
/// <summary>
/// Provides an Azure Append Blob Storage-backed implementation of <see cref="IObjectDocumentFactory"/>.
/// </summary>
public class AppendBlobObjectDocumentFactory : IObjectDocumentFactory
{
    private readonly IAppendBlobDocumentStore appendBlobDocumentStore;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.AzureStorage");

    /// <summary>
    /// Initializes a new instance of the <see cref="AppendBlobObjectDocumentFactory"/> class using Azure services and settings.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
    /// <param name="documentTagStore">The factory used to access document tag storage.</param>
    /// <param name="settings">The default event stream type settings.</param>
    /// <param name="appendBlobSettings">The append blob storage settings used for containers.</param>
    public AppendBlobObjectDocumentFactory(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        IDocumentTagDocumentFactory documentTagStore,
        EventStreamDefaultTypeSettings settings,
        EventStreamAppendBlobSettings appendBlobSettings)
    {
        this.appendBlobDocumentStore = new AppendBlobDocumentStore(clientFactory, documentTagStore, appendBlobSettings, settings);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppendBlobObjectDocumentFactory"/> class using a pre-configured <see cref="IAppendBlobDocumentStore"/>.
    /// </summary>
    /// <param name="appendBlobDocumentStore">The append blob document store to delegate operations to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="appendBlobDocumentStore"/> is null.</exception>
    public AppendBlobObjectDocumentFactory(IAppendBlobDocumentStore appendBlobDocumentStore)
    {
        ArgumentNullException.ThrowIfNull(appendBlobDocumentStore);
        this.appendBlobDocumentStore = appendBlobDocumentStore;
    }

    /// <summary>
    /// Retrieves an object document or creates a new one when it does not exist.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to retrieve or create.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <param name="documentType">Ignored for AppendBlobObjectDocumentFactory (already append-blob-specific).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The existing or newly created <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null, string? documentType = null, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"AppendBlobObjectDocumentFactory.{nameof(GetOrCreateAsync)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        var objectNameLower = objectName.ToLowerInvariant();
        var result = await this.appendBlobDocumentStore!.CreateAsync(objectNameLower, objectId, store);
        if (result is null)
        {
            throw new InvalidOperationException("AppendBlobDocumentStore.CreateAsync returned null document.");
        }
        return result;
    }

    /// <summary>
    /// Retrieves an existing object document from Azure Blob Storage.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <param name="documentType">Ignored for AppendBlobObjectDocumentFactory (already append-blob-specific).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loaded <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null, string? documentType = null, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"AppendBlobObjectDocumentFactory.{nameof(GetAsync)}");
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectId);

        var result = await this.appendBlobDocumentStore!.GetAsync(objectName!.ToLowerInvariant(), objectId!, store);
        if (result is null)
        {
            throw new InvalidOperationException("AppendBlobDocumentStore.GetAsync returned null document.");
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
        using var activity = ActivitySource.StartActivity($"AppendBlobObjectDocumentFactory.{nameof(GetByDocumentTagAsync)}");
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectDocumentTag);
        return (await appendBlobDocumentStore.GetByDocumentByTagAsync(objectName, objectDocumentTag))
               ?? [];
    }

    /// <summary>
    /// Gets the first object document that has the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the document. If not provided, uses the default document store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The first matching document or null when none is found.</returns>
    public Task<IObjectDocument?> GetFirstByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"AppendBlobObjectDocumentFactory.{nameof(GetByDocumentTagAsync)}");
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectDocumentTag);
        return appendBlobDocumentStore.GetFirstByDocumentByTagAsync(objectName, objectDocumentTag, documentTagStore, store);
    }

    /// <summary>
    /// Gets all object documents that have the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the documents. If not provided, uses the default document store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    public async Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"AppendBlobObjectDocumentFactory.{nameof(GetByDocumentTagAsync)}");
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectDocumentTag);
        return (await appendBlobDocumentStore.GetByDocumentByTagAsync(objectName, objectDocumentTag, documentTagStore, store))
               ?? [];
    }

    /// <summary>
    /// Persists the provided object document to Azure Blob Storage.
    /// </summary>
    /// <param name="document">The object document to save.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <param name="documentType">Ignored for AppendBlobObjectDocumentFactory (already append-blob-specific).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public Task SetAsync(IObjectDocument document, string? store = null, string? documentType = null, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"AppendBlobObjectDocumentFactory.{nameof(SetAsync)}");
        AzureStorage.Exceptions.DocumentConfigurationException.ThrowIfNull(document);
        return appendBlobDocumentStore.SetAsync(document);
    }
}
