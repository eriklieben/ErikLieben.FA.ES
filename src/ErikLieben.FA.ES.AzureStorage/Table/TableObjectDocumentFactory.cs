using System.Diagnostics;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Provides an Azure Table Storage-backed implementation of <see cref="IObjectDocumentFactory"/>.
/// </summary>
public class TableObjectDocumentFactory : IObjectDocumentFactory
{
    private readonly ITableDocumentStore tableDocumentStore;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.AzureStorage.Table");

    /// <summary>
    /// Initializes a new instance of the <see cref="TableObjectDocumentFactory"/> class using Azure services and settings.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="TableServiceClient"/> instances.</param>
    /// <param name="documentTagStore">The factory used to access document tag storage.</param>
    /// <param name="settings">The default event stream type settings.</param>
    /// <param name="tableSettings">The table storage settings used for tables and chunking.</param>
    public TableObjectDocumentFactory(
        IAzureClientFactory<TableServiceClient> clientFactory,
        IDocumentTagDocumentFactory documentTagStore,
        EventStreamDefaultTypeSettings settings,
        EventStreamTableSettings tableSettings)
    {
        tableDocumentStore = new TableDocumentStore(clientFactory, documentTagStore, tableSettings, settings);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableObjectDocumentFactory"/> class using a pre-configured <see cref="ITableDocumentStore"/>.
    /// </summary>
    /// <param name="tableDocumentStore">The table document store to delegate operations to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tableDocumentStore"/> is null.</exception>
    public TableObjectDocumentFactory(ITableDocumentStore tableDocumentStore)
    {
        ArgumentNullException.ThrowIfNull(tableDocumentStore);
        this.tableDocumentStore = tableDocumentStore;
    }

    /// <summary>
    /// Retrieves an object document or creates a new one when it does not exist.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the partition key.</param>
    /// <param name="objectId">The identifier of the object to retrieve or create.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <param name="documentType">Ignored for TableObjectDocumentFactory (already table-specific).</param>
    /// <returns>The existing or newly created <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null, string? documentType = null)
    {
        using var activity = ActivitySource.StartActivity($"TableObjectDocumentFactory.{nameof(GetOrCreateAsync)}");
        DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectId);

        var objectNameLower = objectName.ToLowerInvariant();
#pragma warning disable CS8604 // Possible null reference argument - validated above
        var result = await tableDocumentStore.CreateAsync(objectNameLower, objectId, store);
#pragma warning restore CS8604

        if (result is null)
        {
            throw new InvalidOperationException("TableDocumentStore.CreateAsync returned null document.");
        }

        return result;
    }

    /// <summary>
    /// Retrieves an existing object document from Azure Table Storage.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the partition key.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <param name="documentType">Ignored for TableObjectDocumentFactory (already table-specific).</param>
    /// <returns>The loaded <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null, string? documentType = null)
    {
        using var activity = ActivitySource.StartActivity($"TableObjectDocumentFactory.{nameof(GetAsync)}");
        DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectId);

        var result = await tableDocumentStore.GetAsync(objectName!.ToLowerInvariant(), objectId!, store);
        if (result is null)
        {
            throw new InvalidOperationException("TableDocumentStore.GetAsync returned null document.");
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
        using var activity = ActivitySource.StartActivity($"TableObjectDocumentFactory.{nameof(GetFirstByObjectDocumentTag)}");
        DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectDocumentTag);

        return tableDocumentStore.GetFirstByDocumentByTagAsync(objectName, objectDocumentTag, documentTagStore, store);
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
        using var activity = ActivitySource.StartActivity($"TableObjectDocumentFactory.{nameof(GetByObjectDocumentTag)}");
        DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectName);
        DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(objectDocumentTag);

        return (await tableDocumentStore.GetByDocumentByTagAsync(objectName, objectDocumentTag, documentTagStore, store))
               ?? [];
    }

    /// <summary>
    /// Persists the provided object document to Azure Table Storage.
    /// </summary>
    /// <param name="document">The object document to save.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <param name="documentType">Ignored for TableObjectDocumentFactory (already table-specific).</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public Task SetAsync(IObjectDocument document, string? store = null, string? documentType = null)
    {
        using var activity = ActivitySource.StartActivity($"TableObjectDocumentFactory.{nameof(SetAsync)}");
        DocumentConfigurationException.ThrowIfNull(document);

        return tableDocumentStore.SetAsync(document);
    }
}
