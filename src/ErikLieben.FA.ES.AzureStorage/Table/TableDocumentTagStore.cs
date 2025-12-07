using System.Text.RegularExpressions;
using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Table.Model;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Provides an Azure Table Storage-backed implementation of <see cref="IDocumentTagStore"/> for associating and querying document tags.
/// </summary>
public partial class TableDocumentTagStore : IDocumentTagStore
{
    private readonly IAzureClientFactory<TableServiceClient> clientFactory;
    private readonly EventStreamTableSettings settings;
    private readonly string defaultConnectionName;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableDocumentTagStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="TableServiceClient"/> instances.</param>
    /// <param name="settings">The table storage settings.</param>
    /// <param name="defaultConnectionName">The default connection name used when building table clients.</param>
    public TableDocumentTagStore(
        IAzureClientFactory<TableServiceClient> clientFactory,
        EventStreamTableSettings settings,
        string defaultConnectionName)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(settings);

        this.clientFactory = clientFactory;
        this.settings = settings;
        this.defaultConnectionName = defaultConnectionName;
    }

    /// <summary>
    /// Associates the specified tag with the given document by storing a tag entity in Table Storage.
    /// </summary>
    /// <param name="document">The document to tag.</param>
    /// <param name="tag">The tag value to associate with the document.</param>
    /// <returns>A task that represents the asynchronous tagging operation.</returns>
    public async Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);

        var tableClient = await GetTableClientAsync(document);

        var sanitizedTag = SanitizeForTableKey(tag);
        var partitionKey = $"{document.ObjectName.ToLowerInvariant()}_{sanitizedTag}";
        var rowKey = document.ObjectId;

        var entity = new TableDocumentTagEntity
        {
            PartitionKey = partitionKey,
            RowKey = rowKey,
            Tag = tag,
            ObjectName = document.ObjectName,
            ObjectId = document.ObjectId
        };

        try
        {
            // Upsert to handle both insert and update
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new TableDocumentStoreTableNotFoundException(
                $"The table '{settings.DefaultDocumentTagTableName}' was not found. " +
                "Create the table in your deployment or enable AutoCreateTable in settings.", ex);
        }
    }

    /// <summary>
    /// Gets the identifiers of documents that have the specified tag within the given object scope.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <returns>An enumerable of document identifiers; empty when no documents have the tag.</returns>
    public async Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        var serviceClient = clientFactory.CreateClient(defaultConnectionName);
        var tableClient = serviceClient.GetTableClient(settings.DefaultDocumentTagTableName);

        var sanitizedTag = SanitizeForTableKey(tag);
        var partitionKey = $"{objectName.ToLowerInvariant()}_{sanitizedTag}";

        var filter = $"PartitionKey eq '{partitionKey}'";
        var objectIds = new List<string>();

        try
        {
            await foreach (var entity in tableClient.QueryAsync<TableDocumentTagEntity>(filter))
            {
                objectIds.Add(entity.ObjectId);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Table doesn't exist, return empty
            return [];
        }

        return objectIds;
    }

    private async Task<TableClient> GetTableClientAsync(IObjectDocument objectDocument)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(objectDocument.ObjectName);

#pragma warning disable CS0618 // Type or member is obsolete
        var connectionName = !string.IsNullOrWhiteSpace(objectDocument.Active.DocumentTagStore)
            ? objectDocument.Active.DocumentTagStore
            : objectDocument.Active.DocumentTagConnectionName;
#pragma warning restore CS0618

        if (string.IsNullOrWhiteSpace(connectionName))
        {
            connectionName = defaultConnectionName;
        }

        var serviceClient = clientFactory.CreateClient(connectionName);
        var tableClient = serviceClient.GetTableClient(settings.DefaultDocumentTagTableName);

        if (settings.AutoCreateTable)
        {
            await tableClient.CreateIfNotExistsAsync();
        }

        return tableClient;
    }

    /// <summary>
    /// Sanitizes a string for use as a Table Storage partition or row key.
    /// Removes characters not allowed in keys: /, \, #, ?, and control characters.
    /// </summary>
    private static string SanitizeForTableKey(string input)
    {
        return InvalidTableKeyCharsRegex().Replace(input.ToLowerInvariant(), string.Empty);
    }

    [GeneratedRegex(@"[/\\#?\u0000-\u001F\u007F-\u009F]")]
    private static partial Regex InvalidTableKeyCharsRegex();
}
