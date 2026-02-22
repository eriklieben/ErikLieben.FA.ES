using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Creates Table Storage-backed document and stream tag stores.
/// </summary>
public class TableTagFactory : IDocumentTagDocumentFactory
{
    private readonly EventStreamTableSettings tableSettings;
    private readonly IAzureClientFactory<TableServiceClient> clientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableTagFactory"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="TableServiceClient"/> instances.</param>
    /// <param name="tableSettings">The Table storage settings controlling default stores and auto-creation.</param>
    public TableTagFactory(
        IAzureClientFactory<TableServiceClient> clientFactory,
        EventStreamTableSettings tableSettings)
    {
        this.tableSettings = tableSettings;
        this.clientFactory = clientFactory;
    }

    /// <summary>
    /// Creates a document tag store for the specified object document using its configured tag type.
    /// </summary>
    /// <param name="document">The document whose tag configuration is used.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by Table Storage.</returns>
    public IDocumentTagStore CreateDocumentTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.DocumentTagType);

        return new TableDocumentTagStore(clientFactory, tableSettings, tableSettings.DefaultDocumentTagStore);
    }

    /// <summary>
    /// Creates a document tag store using the default document tag type from settings.
    /// </summary>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by Table Storage.</returns>
    public IDocumentTagStore CreateDocumentTagStore()
    {
        return new TableDocumentTagStore(clientFactory, tableSettings, tableSettings.DefaultDocumentTagStore);
    }

    /// <summary>
    /// Creates a document tag store for the specified tag provider type.
    /// </summary>
    /// <param name="type">The tag provider type (e.g., "table").</param>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by the specified provider.</returns>
    public IDocumentTagStore CreateDocumentTagStore(string type)
    {
        return new TableDocumentTagStore(clientFactory, tableSettings, type);
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

        return new TableStreamTagStore(clientFactory, tableSettings);
    }

    /// <summary>
    /// Creates a stream tag store using the default stream tag type from settings.
    /// </summary>
    /// <returns>An <see cref="IDocumentTagStore"/> for stream tags.</returns>
    public IDocumentTagStore CreateStreamTagStore()
    {
        return new TableStreamTagStore(clientFactory, tableSettings);
    }
}
