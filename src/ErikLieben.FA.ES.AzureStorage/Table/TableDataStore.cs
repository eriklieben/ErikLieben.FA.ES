using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Table.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Observability;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Provides an Azure Table Storage-backed implementation of <see cref="IDataStore"/> for reading and appending event streams.
/// </summary>
public class TableDataStore : IDataStore, IDataStoreRecovery
{
    private readonly IAzureClientFactory<TableServiceClient> clientFactory;
    private readonly EventStreamTableSettings settings;

    /// <summary>
    /// Maximum size for a single payload chunk (60KB to leave room for other entity properties).
    /// </summary>
    private const int MaxPayloadChunkSizeBytes = 60 * 1024;

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
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A sequence of events ordered by version, or null when the stream does not exist.</returns>
    public async Task<IEnumerable<IEvent>?> ReadAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("TableDataStore.Read");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureTable);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
        }

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
            await foreach (var entity in tableClient.QueryAsync<TableEventEntity>(filter, cancellationToken: cancellationToken))
            {
                // Skip payload chunk rows (they have _p{index} suffix in RowKey)
                if (entity.PayloadChunkIndex.HasValue && entity.PayloadChunkIndex.Value > 0)
                {
                    continue;
                }

                var @event = await ConvertEntityToEventAsync(tableClient, entity, cancellationToken);
                events.Add(@event);
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
    /// Reads events for the specified document as a streaming async enumerable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is optimized for reading large event streams without loading all events into memory.
    /// Events are yielded as they are retrieved from Azure Table Storage, enabling efficient processing
    /// of streams with millions of events while minimizing memory allocation.
    /// </para>
    /// <para>
    /// Uses Azure Table Storage's native async pagination to fetch events in batches.
    /// </para>
    /// </remarks>
    /// <param name="document">The document whose event stream is read.</param>
    /// <param name="startVersion">The zero-based version to start reading from (inclusive).</param>
    /// <param name="untilVersion">The final version to read up to (inclusive); null to read to the end.</param>
    /// <param name="chunk">The chunk identifier to read from when chunking is enabled; null when not chunked.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the streaming operation.</param>
    /// <returns>An async enumerable of events ordered by version.</returns>
    public IAsyncEnumerable<IEvent> ReadAsStreamAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);
        return ReadAsStreamAsyncCore(document, startVersion, untilVersion, chunk, cancellationToken);
    }

    private async IAsyncEnumerable<IEvent> ReadAsStreamAsyncCore(
        IObjectDocument document,
        int startVersion,
        int? untilVersion,
        int? chunk,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("TableDataStore.ReadAsStream");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureTable);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
        }

        TableClient tableClient;
        try
        {
            tableClient = await GetTableClientAsync(document);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            yield break;
        }

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

        // Use await foreach to stream results directly from Azure Table Storage
        IAsyncEnumerator<TableEventEntity>? enumerator = null;
        try
        {
            enumerator = tableClient.QueryAsync<TableEventEntity>(filter, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    yield break;
                }

                if (!hasNext)
                {
                    break;
                }

                var entity = enumerator.Current;

                // Skip payload chunk rows (they have _p{index} suffix in RowKey)
                if (entity.PayloadChunkIndex.HasValue && entity.PayloadChunkIndex.Value > 0)
                {
                    continue;
                }

                var @event = await ConvertEntityToEventAsync(tableClient, entity, cancellationToken);
                yield return @event;
            }
        }
        finally
        {
            if (enumerator != null)
            {
                await enumerator.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Appends the specified events to the event stream of the given document in Azure Table Storage.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    public Task AppendAsync(IObjectDocument document, CancellationToken cancellationToken, params IEvent[] events)
        => AppendAsync(document, preserveTimestamp: false, cancellationToken, events);

    /// <summary>
    /// Appends the specified events to the event stream of the given document in Azure Table Storage.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="preserveTimestamp">When true, preserves the original timestamp from TableJsonEvent sources (useful for migrations).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    public async Task AppendAsync(IObjectDocument document, bool preserveTimestamp, CancellationToken cancellationToken, params IEvent[] events)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("TableDataStore.Append");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureTable);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationWrite);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.EventCount, events?.Length ?? 0);
        }
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        if (events.Length == 0)
        {
            throw new ArgumentException("No events provided to store.");
        }

        var tableClient = await GetTableClientAsync(document);

        // Check if stream is closed by looking for the last event
        await CheckStreamNotClosedAsync(tableClient, document, cancellationToken);

        var chunkIdentifier = GetLastChunkIdentifier(document);
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

        var batch = BuildAppendBatch(tableEvents, document, documentHash, chunkIdentifier);
        await SubmitBatchesAsync(tableClient, batch, document.Active.StreamIdentifier, cancellationToken);
    }

    private static int? GetLastChunkIdentifier(IObjectDocument document)
    {
        if (!document.Active.ChunkingEnabled())
        {
            return null;
        }

        var chunks = document.Active.StreamChunks;
        var lastChunk = chunks[chunks.Count - 1];
        return lastChunk.ChunkIdentifier;
    }

    private List<TableTransactionAction> BuildAppendBatch(
        List<TableJsonEvent> tableEvents,
        IObjectDocument document,
        string documentHash,
        int? chunkIdentifier)
    {
        var batch = new List<TableTransactionAction>();
        foreach (var tableEvent in tableEvents)
        {
            var entity = tableEvent.ToEntity(
                document.ObjectId,
                document.Active.StreamIdentifier,
                documentHash,
                chunkIdentifier);

            if (TryAddLargePayloadActions(entity, batch))
            {
                continue;
            }

            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, entity));
        }

        return batch;
    }

    private bool TryAddLargePayloadActions(TableEventEntity entity, List<TableTransactionAction> batch)
    {
        if (!settings.EnableLargePayloadChunking)
        {
            return false;
        }

        var payloadBytes = Encoding.UTF8.GetBytes(entity.Payload);
        if (payloadBytes.Length <= settings.PayloadChunkThresholdBytes)
        {
            return false;
        }

        var (dataToStore, isCompressed) = PreparePayloadData(entity.Payload, payloadBytes);

        if (dataToStore.Length > MaxPayloadChunkSizeBytes)
        {
            AddChunkedPayloadActions(entity, dataToStore, isCompressed, batch);
        }
        else
        {
            AddSinglePayloadAction(entity, dataToStore, isCompressed, batch);
        }

        return true;
    }

    private (byte[] Data, bool IsCompressed) PreparePayloadData(string payload, byte[] payloadBytes)
    {
        if (settings.CompressLargePayloads)
        {
            return (CompressPayload(payload), true);
        }

        return (payloadBytes, false);
    }

    private static void AddChunkedPayloadActions(
        TableEventEntity entity,
        byte[] dataToStore,
        bool isCompressed,
        List<TableTransactionAction> batch)
    {
        var chunks = ChunkPayloadData(dataToStore);
        var totalChunks = chunks.Count;

        // Main entity stores first chunk
        entity.Payload = "{}";
        entity.PayloadData = chunks[0];
        entity.PayloadChunked = true;
        entity.PayloadTotalChunks = totalChunks;
        entity.PayloadCompressed = isCompressed;

        batch.Add(new TableTransactionAction(TableTransactionActionType.Add, entity));

        for (int i = 1; i < chunks.Count; i++)
        {
            var chunkEntity = CreatePayloadChunkEntity(entity, chunks[i], i, totalChunks);
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, chunkEntity));
        }
    }

    private static void AddSinglePayloadAction(
        TableEventEntity entity,
        byte[] dataToStore,
        bool isCompressed,
        List<TableTransactionAction> batch)
    {
        entity.Payload = "{}";
        entity.PayloadData = dataToStore;
        entity.PayloadChunked = false;
        entity.PayloadTotalChunks = 1;
        entity.PayloadCompressed = isCompressed;

        batch.Add(new TableTransactionAction(TableTransactionActionType.Add, entity));
    }

    private async Task SubmitBatchesAsync(
        TableClient tableClient,
        List<TableTransactionAction> batch,
        string streamIdentifier,
        CancellationToken cancellationToken)
    {
        try
        {
            // Azure Table Storage batch operations are limited to 100 entities
            // and all entities in a batch must have the same partition key
            const int batchSize = 100;
            for (int i = 0; i < batch.Count; i += batchSize)
            {
                var batchSlice = batch.Skip(i).Take(batchSize).ToList();
                await tableClient.SubmitTransactionAsync(batchSlice, cancellationToken);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new TableDataStoreProcessingException(
                $"Conflict detected when appending events to stream '{streamIdentifier}'. " +
                "An event with the same version already exists.", ex);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new TableDocumentStoreTableNotFoundException(
                $"The table '{settings.DefaultEventTableName}' was not found. " +
                "Create the table in your deployment or enable AutoCreateTable in settings.", ex);
        }
    }

    private static async Task CheckStreamNotClosedAsync(TableClient tableClient, IObjectDocument document, CancellationToken cancellationToken = default)
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
        await foreach (var entity in tableClient.QueryAsync<TableEventEntity>(filter, cancellationToken: cancellationToken))
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
        using var activity = FaesInstrumentation.Storage.StartActivity("TableDataStore.RemoveEventsForFailedCommit");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureTable);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationDelete);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
        }

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var tableClient = await GetTableClientAsync(document);

        var chunkIdentifier = GetLastChunkIdentifierIfAvailable(document);
        var partitionKey = GetPartitionKey(document, chunkIdentifier);
        var removed = 0;

        for (var version = fromVersion; version <= toVersion; version++)
        {
            var rowKey = $"{version:d20}";
            if (await TryDeleteEventRowAsync(tableClient, partitionKey, rowKey))
            {
                removed++;
            }
        }

        activity?.SetTag(FaesSemanticConventions.EventCount, removed);
        return removed;
    }

    private static int? GetLastChunkIdentifierIfAvailable(IObjectDocument document)
    {
        if (!document.Active.ChunkingEnabled() || document.Active.StreamChunks.Count == 0)
        {
            return null;
        }

        var chunks = document.Active.StreamChunks;
        var lastChunk = chunks[chunks.Count - 1];
        return lastChunk.ChunkIdentifier;
    }

    private static async Task<bool> TryDeleteEventRowAsync(TableClient tableClient, string partitionKey, string rowKey)
    {
        TableEventEntity? mainEntity;
        try
        {
            var response = await tableClient.GetEntityAsync<TableEventEntity>(partitionKey, rowKey);
            mainEntity = response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }

        await DeletePayloadChunksAsync(tableClient, partitionKey, rowKey, mainEntity);

        try
        {
            await tableClient.DeleteEntityAsync(partitionKey, rowKey);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private static async Task DeletePayloadChunksAsync(TableClient tableClient, string partitionKey, string rowKey, TableEventEntity mainEntity)
    {
        if (mainEntity.PayloadChunked != true || !mainEntity.PayloadTotalChunks.HasValue)
        {
            return;
        }

        for (int i = 1; i < mainEntity.PayloadTotalChunks.Value; i++)
        {
            try
            {
                await tableClient.DeleteEntityAsync(partitionKey, $"{rowKey}_p{i}");
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Chunk doesn't exist, continue
            }
        }
    }

    /// <summary>
    /// Compresses a string payload using GZip compression.
    /// </summary>
    private static byte[] CompressPayload(string payload)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
        {
            gzipStream.Write(payloadBytes, 0, payloadBytes.Length);
        }

        return outputStream.ToArray();
    }

    /// <summary>
    /// Decompresses a GZip compressed payload back to a string.
    /// </summary>
    private static string DecompressPayload(byte[] compressedData)
    {
        using var inputStream = new MemoryStream(compressedData);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        gzipStream.CopyTo(outputStream);
        return Encoding.UTF8.GetString(outputStream.ToArray());
    }

    /// <summary>
    /// Splits payload data into chunks of maximum size.
    /// </summary>
    private static List<byte[]> ChunkPayloadData(byte[] data)
    {
        var chunks = new List<byte[]>();
        var offset = 0;

        while (offset < data.Length)
        {
            var chunkSize = Math.Min(MaxPayloadChunkSizeBytes, data.Length - offset);
            var chunk = new byte[chunkSize];
            Array.Copy(data, offset, chunk, 0, chunkSize);
            chunks.Add(chunk);
            offset += chunkSize;
        }

        return chunks;
    }

    /// <summary>
    /// Creates a payload chunk entity for storing additional payload chunks.
    /// </summary>
    private static TableEventEntity CreatePayloadChunkEntity(
        TableEventEntity mainEntity,
        byte[] chunkData,
        int chunkIndex,
        int totalChunks)
    {
        return new TableEventEntity
        {
            PartitionKey = mainEntity.PartitionKey,
            RowKey = $"{mainEntity.RowKey}_p{chunkIndex}",
            ObjectId = mainEntity.ObjectId,
            StreamIdentifier = mainEntity.StreamIdentifier,
            EventVersion = mainEntity.EventVersion,
            EventType = mainEntity.EventType,
            SchemaVersion = mainEntity.SchemaVersion,
            ChunkIdentifier = mainEntity.ChunkIdentifier,
            LastObjectDocumentHash = mainEntity.LastObjectDocumentHash,
            PayloadChunkIndex = chunkIndex,
            PayloadData = chunkData,
            PayloadTotalChunks = totalChunks
        };
    }

    /// <summary>
    /// Reassembles a chunked payload from the main entity and additional chunk rows.
    /// </summary>
    private static async Task<string> ReassembleChunkedPayloadAsync(
        TableClient tableClient,
        TableEventEntity mainEntity,
        CancellationToken cancellationToken = default)
    {
        if (mainEntity.PayloadChunked != true || mainEntity.PayloadTotalChunks == null)
        {
            return GetNonChunkedPayload(mainEntity);
        }

        var chunks = await FetchAllChunksAsync(tableClient, mainEntity, cancellationToken);
        var combinedData = ConcatenateChunks(chunks);

        return DecodePayloadData(combinedData, mainEntity.PayloadCompressed == true);
    }

    private static string GetNonChunkedPayload(TableEventEntity mainEntity)
    {
        if (mainEntity.PayloadData != null && mainEntity.PayloadData.Length > 0)
        {
            return DecodePayloadData(mainEntity.PayloadData, mainEntity.PayloadCompressed == true);
        }

        return mainEntity.Payload;
    }

    private static string DecodePayloadData(byte[] data, bool isCompressed)
    {
        return isCompressed
            ? DecompressPayload(data)
            : Encoding.UTF8.GetString(data);
    }

    private static async Task<byte[][]> FetchAllChunksAsync(
        TableClient tableClient,
        TableEventEntity mainEntity,
        CancellationToken cancellationToken)
    {
        var totalChunks = mainEntity.PayloadTotalChunks!.Value;
        var chunks = new byte[totalChunks][];

        if (mainEntity.PayloadData != null)
        {
            chunks[0] = mainEntity.PayloadData;
        }

        for (int i = 1; i < totalChunks; i++)
        {
            chunks[i] = await FetchSingleChunkAsync(tableClient, mainEntity, i, totalChunks, cancellationToken);
        }

        return chunks;
    }

    private static async Task<byte[]> FetchSingleChunkAsync(
        TableClient tableClient,
        TableEventEntity mainEntity,
        int chunkIndex,
        int totalChunks,
        CancellationToken cancellationToken)
    {
        var chunkRowKey = $"{mainEntity.RowKey}_p{chunkIndex}";
        try
        {
            var chunkResponse = await tableClient.GetEntityAsync<TableEventEntity>(
                mainEntity.PartitionKey,
                chunkRowKey,
                cancellationToken: cancellationToken);

            if (chunkResponse?.Value?.PayloadData != null)
            {
                return chunkResponse.Value.PayloadData;
            }

            return [];
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException(
                $"Missing payload chunk {chunkIndex} for event version {mainEntity.EventVersion} " +
                $"in stream '{mainEntity.StreamIdentifier}'. Expected {totalChunks} chunks.");
        }
    }

    private static byte[] ConcatenateChunks(byte[][] chunks)
    {
        var nonNullChunks = chunks.Where(chunk => chunk != null).ToList();
        var totalLength = nonNullChunks.Sum(c => c!.Length);
        var combinedData = new byte[totalLength];
        var offset = 0;
        foreach (var chunk in nonNullChunks)
        {
            Array.Copy(chunk!, 0, combinedData, offset, chunk!.Length);
            offset += chunk.Length;
        }

        return combinedData;
    }

    /// <summary>
    /// Converts an entity to a TableJsonEvent, handling payload reassembly if needed.
    /// </summary>
    private static async Task<TableJsonEvent> ConvertEntityToEventAsync(
        TableClient tableClient,
        TableEventEntity entity,
        CancellationToken cancellationToken = default)
    {
        // Check if this is a chunked or compressed payload
        if (entity.PayloadChunked == true || entity.PayloadData != null)
        {
            var payload = await ReassembleChunkedPayloadAsync(tableClient, entity, cancellationToken);
            entity.Payload = payload;
            entity.PayloadData = null; // Clear binary data after reassembly
        }

        return TableJsonEvent.FromEntity(entity);
    }
}
