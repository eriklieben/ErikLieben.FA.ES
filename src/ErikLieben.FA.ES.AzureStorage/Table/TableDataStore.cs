using System.Diagnostics;
using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Table.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Provides an Azure Table Storage-backed implementation of <see cref="IDataStore"/> for reading and appending event streams.
/// </summary>
public class TableDataStore : IDataStore, IDataStoreRecovery
{
    private readonly IAzureClientFactory<TableServiceClient> clientFactory;
    private readonly EventStreamTableSettings settings;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.AzureStorage.Table");

    /// <summary>
    /// Initializes a new instance of the <see cref="TableDataStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="TableServiceClient"/> instances.</param>
    /// <param name="settings">The table storage settings.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clientFactory"/> or <paramref name="settings"/> is null.</exception>
    public TableDataStore(IAzureClientFactory<TableServiceClient> clientFactory, EventStreamTableSettings settings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(settings);

        this.clientFactory = clientFactory;
        this.settings = settings;
    }

    /// <summary>
    /// Reads events for the specified document from Azure Table Storage.
    /// </summary>
    /// <param name="document">The document whose event stream is read.</param>
    /// <param name="startVersion">The zero-based version to start reading from (inclusive).</param>
    /// <param name="untilVersion">The final version to read up to (inclusive); null to read to the end.</param>
    /// <param name="chunk">The chunk identifier to read from when chunking is enabled; null when not chunked.</param>
    /// <returns>A sequence of events ordered by version, or null when the stream does not exist.</returns>
    public async Task<IEnumerable<IEvent>?> ReadAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null)
    {
        using var activity = ActivitySource.StartActivity("TableDataStore.ReadAsync");

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var tableClient = await GetTableClientAsync(document);
        var partitionKey = GetPartitionKey(document, chunk);

        var startRowKey = $"{startVersion:d20}";
        var endRowKey = untilVersion.HasValue ? $"{untilVersion.Value:d20}" : null;

        string filter;
        if (endRowKey != null)
        {
            filter = $"PartitionKey eq '{partitionKey}' and RowKey ge '{startRowKey}' and RowKey le '{endRowKey}'";
        }
        else
        {
            filter = $"PartitionKey eq '{partitionKey}' and RowKey ge '{startRowKey}'";
        }

        var events = new List<IEvent>();
        try
        {
            await foreach (var entity in tableClient.QueryAsync<TableEventEntity>(filter))
            {
                events.Add(TableJsonEvent.FromEntity(entity));
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        if (events.Count == 0)
        {
            return null;
        }

        return events.OrderBy(e => e.EventVersion).ToList();
    }

    /// <summary>
    /// Appends the specified events to the event stream of the given document in Azure Table Storage.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    public Task AppendAsync(IObjectDocument document, params IEvent[] events)
        => AppendAsync(document, preserveTimestamp: false, events);

    /// <summary>
    /// Appends the specified events to the event stream of the given document in Azure Table Storage.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="preserveTimestamp">When true, preserves the original timestamp from TableJsonEvent sources (useful for migrations).</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    public async Task AppendAsync(IObjectDocument document, bool preserveTimestamp, params IEvent[] events)
    {
        using var activity = ActivitySource.StartActivity("TableDataStore.AppendAsync");
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        if (events.Length == 0)
        {
            throw new ArgumentException("No events provided to store.");
        }

        var tableClient = await GetTableClientAsync(document);

        // Check if stream is closed by looking for the last event
        await CheckStreamNotClosedAsync(tableClient, document);

        int? chunkIdentifier = null;
        if (document.Active.ChunkingEnabled())
        {
            var chunks = document.Active.StreamChunks;
            var lastChunk = chunks[chunks.Count - 1];
            chunkIdentifier = lastChunk.ChunkIdentifier;
        }

        var documentHash = document.Hash ?? "*";

        // Convert events and add to table
        var tableEvents = events
            .Select(e => TableJsonEvent.From(e, preserveTimestamp))
            .Where(e => e != null)
            .Cast<TableJsonEvent>()
            .ToList();

        if (tableEvents.Count == 0)
        {
            throw new ArgumentException("No valid events could be converted for storage.");
        }

        // Use batch transaction for atomicity
        var batch = new List<TableTransactionAction>();
        foreach (var tableEvent in tableEvents)
        {
            var entity = tableEvent.ToEntity(
                document.ObjectId,
                document.Active.StreamIdentifier,
                documentHash,
                chunkIdentifier);

            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, entity));
        }

        try
        {
            // Azure Table Storage batch operations are limited to 100 entities
            // and all entities in a batch must have the same partition key
            const int batchSize = 100;
            for (int i = 0; i < batch.Count; i += batchSize)
            {
                var batchSlice = batch.Skip(i).Take(batchSize).ToList();
                await tableClient.SubmitTransactionAsync(batchSlice);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new TableDataStoreProcessingException(
                $"Conflict detected when appending events to stream '{document.Active.StreamIdentifier}'. " +
                "An event with the same version already exists.", ex);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new TableDocumentStoreTableNotFoundException(
                $"The table '{settings.DefaultEventTableName}' was not found. " +
                "Create the table in your deployment or enable AutoCreateTable in settings.", ex);
        }
    }

    private static async Task CheckStreamNotClosedAsync(TableClient tableClient, IObjectDocument document)
    {
        int? chunkIdentifier = null;
        if (document.Active.ChunkingEnabled())
        {
            var chunks = document.Active.StreamChunks;
            var lastChunk = chunks[chunks.Count - 1];
            chunkIdentifier = lastChunk.ChunkIdentifier;
        }

        var partitionKey = GetPartitionKey(document, chunkIdentifier);

        // Query for the last event to check if stream is closed
        var filter = $"PartitionKey eq '{partitionKey}'";

        TableEventEntity? lastEvent = null;
        await foreach (var entity in tableClient.QueryAsync<TableEventEntity>(filter))
        {
            if (lastEvent == null || entity.EventVersion > lastEvent.EventVersion)
            {
                lastEvent = entity;
            }
        }

        if (lastEvent != null && lastEvent.EventType == "EventStream.Closed")
        {
            throw new EventStreamClosedException(
                document.Active.StreamIdentifier,
                $"Cannot append events to closed stream '{document.Active.StreamIdentifier}'. " +
                "The stream was closed and may have a continuation stream. Please retry on the active stream.");
        }
    }

    private async Task<TableClient> GetTableClientAsync(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document.ObjectName);

#pragma warning disable CS0618 // Type or member is obsolete
        var connectionName = !string.IsNullOrWhiteSpace(document.Active.DataStore)
            ? document.Active.DataStore
            : document.Active.StreamConnectionName;
#pragma warning restore CS0618

        var serviceClient = clientFactory.CreateClient(connectionName);
        var tableClient = serviceClient.GetTableClient(settings.DefaultEventTableName);

        if (settings.AutoCreateTable)
        {
            await tableClient.CreateIfNotExistsAsync();
        }

        return tableClient;
    }

    private static string GetPartitionKey(IObjectDocument document, int? chunk)
    {
        // Table name already indicates the object type, so partition key only needs stream identifier
        if (chunk.HasValue)
        {
            return $"{document.Active.StreamIdentifier}_{chunk:d10}";
        }
        return document.Active.StreamIdentifier;
    }

    /// <inheritdoc />
    public async Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion)
    {
        using var activity = ActivitySource.StartActivity("TableDataStore.RemoveEventsForFailedCommitAsync");

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var tableClient = await GetTableClientAsync(document);

        // Determine chunk if chunking is enabled
        int? chunkIdentifier = null;
        if (document.Active.ChunkingEnabled() && document.Active.StreamChunks.Count > 0)
        {
            var chunks = document.Active.StreamChunks;
            var lastChunk = chunks[chunks.Count - 1];
            chunkIdentifier = lastChunk.ChunkIdentifier;
        }

        var partitionKey = GetPartitionKey(document, chunkIdentifier);
        var removed = 0;

        // Delete each event row individually
        for (var version = fromVersion; version <= toVersion; version++)
        {
            var rowKey = $"{version:d20}";
            try
            {
                await tableClient.DeleteEntityAsync(partitionKey, rowKey);
                removed++;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Event doesn't exist (wasn't written) - that's fine, continue
            }
        }

        activity?.SetTag("RemovedCount", removed);
        return removed;
    }
}
