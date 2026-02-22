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
/// Provides a Table Storage-backed store for associating tags with event streams.
/// </summary>
public partial class TableStreamTagStore : IDocumentTagStore
{
    private readonly IAzureClientFactory<TableServiceClient> clientFactory;
    private readonly EventStreamTableSettings settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableStreamTagStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="TableServiceClient"/> instances.</param>
    /// <param name="settings">The table storage settings.</param>
    public TableStreamTagStore(
        IAzureClientFactory<TableServiceClient> clientFactory,
        EventStreamTableSettings settings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(settings);

        this.clientFactory = clientFactory;
        this.settings = settings;
    }

    /// <summary>
    /// Associates the specified tag with the stream of the given document.
    /// </summary>
    /// <param name="document">The document whose stream is tagged.</param>
    /// <param name="tag">The tag value to associate.</param>
    /// <returns>A task that represents the asynchronous tagging operation.</returns>
    public async Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var tableClient = await GetTableClientAsync(document);

        var partitionKey = $"{document.ObjectName.ToLowerInvariant()}_{document.Active.StreamIdentifier}";
        var sanitizedTag = SanitizeForTableKey(tag);

        var entity = new TableStreamTagEntity
        {
            PartitionKey = partitionKey,
            RowKey = sanitizedTag,
            Tag = tag,
            StreamIdentifier = document.Active.StreamIdentifier,
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
                $"The table '{settings.DefaultStreamTagTableName}' was not found. " +
                "Create the table in your deployment or enable AutoCreateTable in settings.", ex);
        }
    }

    /// <summary>
    /// Gets the identifiers of streams that have the specified tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <returns>An enumerable of object identifiers that have streams with the specified tag.</returns>
    public async Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        var serviceClient = clientFactory.CreateClient(settings.DefaultDocumentTagStore);
        var tableClient = serviceClient.GetTableClient(settings.DefaultStreamTagTableName);

        var sanitizedTag = SanitizeForTableKey(tag);

        // Query all entities where the row key matches the sanitized tag
        // and the partition key starts with the object name
        var filter = $"RowKey eq '{sanitizedTag}' and ObjectName eq '{objectName}'";
        var objectIds = new List<string>();

        try
        {
            await foreach (var entity in tableClient.QueryAsync<TableStreamTagEntity>(filter))
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

    /// <summary>
    /// Removes the specified tag from the stream of the given document by deleting the tag entity from Table Storage.
    /// </summary>
    /// <param name="document">The document whose stream tag should be removed.</param>
    /// <param name="tag">The tag value to remove.</param>
    /// <returns>A task that represents the asynchronous removal operation.</returns>
    public async Task RemoveAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var tableClient = await GetTableClientAsync(document);

        var partitionKey = $"{document.ObjectName.ToLowerInvariant()}_{document.Active.StreamIdentifier}";
        var sanitizedTag = SanitizeForTableKey(tag);

        try
        {
            await tableClient.DeleteEntityAsync(partitionKey, sanitizedTag);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Tag doesn't exist, which is fine (idempotent operation)
        }
    }

    private async Task<TableClient> GetTableClientAsync(IObjectDocument objectDocument)
    {
        ArgumentNullException.ThrowIfNull(objectDocument.ObjectName);

#pragma warning disable CS0618 // Type or member is obsolete
        var connectionName = !string.IsNullOrWhiteSpace(objectDocument.Active.StreamTagStore)
            ? objectDocument.Active.StreamTagStore
            : objectDocument.Active.StreamTagConnectionName;
#pragma warning restore CS0618

        if (string.IsNullOrWhiteSpace(connectionName))
        {
            connectionName = settings.DefaultDocumentTagStore;
        }

        var serviceClient = clientFactory.CreateClient(connectionName);
        var tableClient = serviceClient.GetTableClient(settings.DefaultStreamTagTableName);

        if (settings.AutoCreateTable)
        {
            await tableClient.CreateIfNotExistsAsync();
        }

        return tableClient;
    }

    /// <summary>
    /// Sanitizes a string for use as a Table Storage partition or row key.
    /// </summary>
    private static string SanitizeForTableKey(string input)
    {
        return InvalidTableKeyCharsRegex().Replace(input.ToLowerInvariant(), string.Empty);
    }

    [GeneratedRegex(@"[/\\#?\u0000-\u001F\u007F-\u009F]")]
    private static partial Regex InvalidTableKeyCharsRegex();
}
