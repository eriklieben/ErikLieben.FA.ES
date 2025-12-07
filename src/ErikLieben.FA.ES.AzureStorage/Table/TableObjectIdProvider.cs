using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Table.Model;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Provides Azure Table Storage-backed implementation of <see cref="IObjectIdProvider"/>.
/// Uses continuation tokens for efficient pagination through large object collections.
/// </summary>
public class TableObjectIdProvider : IObjectIdProvider
{
    private readonly IAzureClientFactory<TableServiceClient> clientFactory;
    private readonly EventStreamTableSettings tableSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableObjectIdProvider"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="TableServiceClient"/> instances.</param>
    /// <param name="tableSettings">The table storage settings used for tables.</param>
    public TableObjectIdProvider(
        IAzureClientFactory<TableServiceClient> clientFactory,
        EventStreamTableSettings tableSettings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(tableSettings);

        this.clientFactory = clientFactory;
        this.tableSettings = tableSettings;
    }

    /// <summary>
    /// Gets a page of object IDs for the specified object type using continuation tokens.
    /// </summary>
    /// <param name="objectName">The object type name (e.g., "project", "workItem").</param>
    /// <param name="continuationToken">Optional continuation token from previous page. Pass null for first page.</param>
    /// <param name="pageSize">Number of items to return per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged result with object IDs and continuation token for the next page.</returns>
    public async Task<PagedResult<string>> GetObjectIdsAsync(
        string objectName,
        string? continuationToken,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var objectNameLower = objectName.ToLowerInvariant();
        var items = new HashSet<string>();
        string? nextContinuationToken = null;

        var serviceClient = clientFactory.CreateClient(tableSettings.DefaultDocumentStore);
        var tableClient = serviceClient.GetTableClient(tableSettings.DefaultDocumentTableName);

        // Filter by partition key (objectName)
        var filter = $"PartitionKey eq '{objectNameLower}'";

        try
        {
            // Use Azure Table Storage's native pagination
            var query = tableClient.QueryAsync<TableDocumentEntity>(
                filter: filter,
                maxPerPage: pageSize,
                select: new[] { "RowKey" }, // Only fetch the object ID (row key)
                cancellationToken: cancellationToken);

            var pages = query.AsPages(continuationToken, pageSize);

            await foreach (var page in pages.WithCancellation(cancellationToken))
            {
                foreach (var entity in page.Values)
                {
                    items.Add(entity.RowKey); // RowKey is the ObjectId
                }

                nextContinuationToken = page.ContinuationToken;
                break; // Only process first page
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Table doesn't exist, return empty result
            return new PagedResult<string>
            {
                Items = [],
                PageSize = pageSize,
                ContinuationToken = null
            };
        }

        return new PagedResult<string>
        {
            Items = items.ToList(),
            PageSize = pageSize,
            ContinuationToken = nextContinuationToken
        };
    }

    /// <summary>
    /// Checks if an object document exists for the given ID.
    /// </summary>
    /// <param name="objectName">The object type name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the object exists, false otherwise.</returns>
    public async Task<bool> ExistsAsync(
        string objectName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        var objectNameLower = objectName.ToLowerInvariant();

        var serviceClient = clientFactory.CreateClient(tableSettings.DefaultDocumentStore);
        var tableClient = serviceClient.GetTableClient(tableSettings.DefaultDocumentTableName);

        try
        {
            var response = await tableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                objectNameLower,
                objectId,
                select: new[] { "PartitionKey" }, // Minimal data fetch
                cancellationToken: cancellationToken);

            return response.HasValue;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the total count of objects for the given type.
    /// Warning: This may be expensive for large datasets as it requires enumerating all items.
    /// </summary>
    /// <param name="objectName">The object type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total count of unique object IDs.</returns>
    public async Task<long> CountAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        var objectNameLower = objectName.ToLowerInvariant();
        long count = 0;

        var serviceClient = clientFactory.CreateClient(tableSettings.DefaultDocumentStore);
        var tableClient = serviceClient.GetTableClient(tableSettings.DefaultDocumentTableName);

        var filter = $"PartitionKey eq '{objectNameLower}'";

        try
        {
            await foreach (var _ in tableClient.QueryAsync<TableDocumentEntity>(
                filter: filter,
                select: new[] { "PartitionKey" }, // Minimal data fetch
                cancellationToken: cancellationToken))
            {
                count++;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return 0;
        }

        return count;
    }
}
