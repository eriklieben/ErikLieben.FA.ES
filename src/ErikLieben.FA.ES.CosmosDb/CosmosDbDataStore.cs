using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Exceptions;
using ErikLieben.FA.ES.CosmosDb.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Observability;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Provides a CosmosDB-backed implementation of <see cref="IDataStore"/> for reading and appending event streams.
/// Optimized for RU efficiency using partition key per stream for efficient reads.
/// </summary>
public class CosmosDbDataStore : IDataStore, IDataStoreRecovery
{
    private readonly CosmosClient cosmosClient;
    private readonly EventStreamCosmosDbSettings settings;
    private Container? eventsContainer;

    /// <summary>
    /// Cache of stream IDs that are known to be closed.
    /// Once a stream is closed, it remains closed forever, so we can cache this
    /// to avoid querying CosmosDB on every append (saves 1 RU per append).
    /// </summary>
    /// <remarks>
    /// Uses ConcurrentDictionary for thread-safe access without locks because:
    /// - Closed streams never reopen (immutable property)
    /// - Memory usage is minimal (just stream ID strings)
    /// - Cleared on application restart (safe default)
    /// </remarks>
    private static readonly ConcurrentDictionary<string, byte> ClosedStreamCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbDataStore"/> class.
    /// </summary>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="settings">The CosmosDB settings.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cosmosClient"/> or <paramref name="settings"/> is null.</exception>
    public CosmosDbDataStore(CosmosClient cosmosClient, EventStreamCosmosDbSettings settings)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(settings);

        this.cosmosClient = cosmosClient;
        this.settings = settings;
    }

    /// <summary>
    /// Reads events for the specified document from CosmosDB.
    /// Uses partition key queries for optimal RU efficiency (single partition read).
    /// </summary>
    /// <param name="document">The document whose event stream is read.</param>
    /// <param name="startVersion">The zero-based version to start reading from (inclusive).</param>
    /// <param name="untilVersion">The final version to read up to (inclusive); null to read to the end.</param>
    /// <param name="chunk">The chunk identifier (not used for CosmosDB, kept for interface compatibility).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A sequence of events ordered by version, or null when the stream does not exist.</returns>
    public async Task<IEnumerable<IEvent>?> ReadAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("CosmosDbDataStore.Read");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemCosmosDb);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
        }

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var container = await GetEventsContainerAsync();
        var streamId = document.Active.StreamIdentifier;

        // Build query with partition key for optimal RU usage
        // Note: Using 'c._type' because our custom System.Text.Json serializer respects
        // the [JsonPropertyName("_type")] attribute on CosmosDbEventEntity.Type
        var queryText = untilVersion.HasValue
            ? "SELECT * FROM c WHERE c.streamId = @streamId AND c.version >= @startVersion AND c.version <= @untilVersion AND c._type = 'event' ORDER BY c.version"
            : "SELECT * FROM c WHERE c.streamId = @streamId AND c.version >= @startVersion AND c._type = 'event' ORDER BY c.version";

        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@streamId", streamId)
            .WithParameter("@startVersion", startVersion);

        if (untilVersion.HasValue)
        {
            queryDefinition = queryDefinition.WithParameter("@untilVersion", untilVersion.Value);
        }

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(streamId),
            MaxItemCount = -1 // Let CosmosDB optimize page size
        };

        var events = new List<IEvent>();
        try
        {
            using var iterator = container.GetItemQueryIterator<CosmosDbEventEntity>(queryDefinition, requestOptions: queryOptions);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                foreach (var entity in response)
                {
                    events.Add(CosmosDbJsonEvent.FromEntity(entity));
                }
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (events.Count == 0)
        {
            return null;
        }

        return events;
    }

    /// <summary>
    /// Reads events for the specified document as a streaming async enumerable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is optimized for reading large event streams without loading all events into memory.
    /// Events are yielded as they are retrieved from CosmosDB, enabling efficient processing of
    /// streams with millions of events while minimizing memory allocation.
    /// </para>
    /// <para>
    /// Uses CosmosDB's native pagination to fetch events in batches, yielding each event as soon
    /// as it's available rather than waiting for the entire result set.
    /// </para>
    /// </remarks>
    /// <param name="document">The document whose event stream is read.</param>
    /// <param name="startVersion">The zero-based version to start reading from (inclusive).</param>
    /// <param name="untilVersion">The final version to read up to (inclusive); null to read to the end.</param>
    /// <param name="chunk">The chunk identifier (not used for CosmosDB, kept for interface compatibility).</param>
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
        using var activity = FaesInstrumentation.Storage.StartActivity("CosmosDbDataStore.ReadAsStream");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemCosmosDb);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
        }

        Container container;
        try
        {
            container = await GetEventsContainerAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            yield break;
        }

        var streamId = document.Active.StreamIdentifier;

        // Build query with partition key for optimal RU usage
        var queryText = untilVersion.HasValue
            ? "SELECT * FROM c WHERE c.streamId = @streamId AND c.version >= @startVersion AND c.version <= @untilVersion AND c._type = 'event' ORDER BY c.version"
            : "SELECT * FROM c WHERE c.streamId = @streamId AND c.version >= @startVersion AND c._type = 'event' ORDER BY c.version";

        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@streamId", streamId)
            .WithParameter("@startVersion", startVersion);

        if (untilVersion.HasValue)
        {
            queryDefinition = queryDefinition.WithParameter("@untilVersion", untilVersion.Value);
        }

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(streamId),
            MaxItemCount = settings.StreamingPageSize > 0 ? settings.StreamingPageSize : 100
        };

        FeedIterator<CosmosDbEventEntity>? iterator = null;
        try
        {
            iterator = container.GetItemQueryIterator<CosmosDbEventEntity>(queryDefinition, requestOptions: queryOptions);
            while (iterator.HasMoreResults)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FeedResponse<CosmosDbEventEntity> response;
                try
                {
                    response = await iterator.ReadNextAsync(cancellationToken);
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    yield break;
                }

                foreach (var entity in response)
                {
                    yield return CosmosDbJsonEvent.FromEntity(entity);
                }
            }
        }
        finally
        {
            iterator?.Dispose();
        }
    }

    /// <summary>
    /// Appends the specified events to the event stream of the given document in CosmosDB.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    public Task AppendAsync(IObjectDocument document, CancellationToken cancellationToken, params IEvent[] events)
        => AppendAsync(document, preserveTimestamp: false, cancellationToken, events);

    /// <summary>
    /// Appends the specified events to the event stream of the given document in CosmosDB.
    /// Uses transactional batches for atomicity when multiple events are appended.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="preserveTimestamp">When true, preserves the original timestamp from source events (useful for migrations).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    public async Task AppendAsync(IObjectDocument document, bool preserveTimestamp, CancellationToken cancellationToken, params IEvent[] events)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("CosmosDbDataStore.Append");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemCosmosDb);
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

        var container = await GetEventsContainerAsync();
        var streamId = document.Active.StreamIdentifier;

        // Check if stream is closed
        await CheckStreamNotClosedAsync(container, streamId, cancellationToken);

        // Convert events to CosmosDB entities
        var cosmosEvents = events
            .Select(e => CosmosDbJsonEvent.From(e, preserveTimestamp))
            .Where(e => e != null)
            .Cast<CosmosDbJsonEvent>()
            .Select(e => e.ToEntity(streamId, preserveTimestamp))
            .ToList();

        if (cosmosEvents.Count == 0)
        {
            throw new ArgumentException("No valid events could be converted for storage.");
        }

        // Set TTL if configured
        if (settings.DefaultTimeToLiveSeconds > 0)
        {
            foreach (var evt in cosmosEvents)
            {
                evt.Ttl = settings.DefaultTimeToLiveSeconds;
            }
        }

        try
        {
            var partitionKey = new PartitionKey(streamId);
            await WriteEventsAsync(container, cosmosEvents, partitionKey, streamId, cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new CosmosDbProcessingException(
                $"Conflict detected when appending events to stream '{streamId}'. " +
                "An event with the same version already exists.", ex);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new CosmosDbContainerNotFoundException(
                $"The container '{settings.EventsContainerName}' was not found. " +
                "Create the container in your deployment or enable AutoCreateContainers in settings.", ex);
        }
    }

    private async Task WriteEventsAsync(
        Container container,
        List<CosmosDbEventEntity> cosmosEvents,
        PartitionKey partitionKey,
        string streamId,
        CancellationToken cancellationToken)
    {
        if (cosmosEvents.Count == 1)
        {
            await container.CreateItemAsync(cosmosEvents[0], partitionKey, cancellationToken: cancellationToken);
            return;
        }

        if (cosmosEvents.Count <= settings.MaxBatchSize)
        {
            await ExecuteBatchAsync(container, cosmosEvents, partitionKey, streamId, cancellationToken);
            return;
        }

        // More than max batch size - split into multiple batches
        for (int i = 0; i < cosmosEvents.Count; i += settings.MaxBatchSize)
        {
            var batchEvents = cosmosEvents.Skip(i).Take(settings.MaxBatchSize).ToList();
            await ExecuteBatchAsync(container, batchEvents, partitionKey, streamId, cancellationToken);
        }
    }

    private static async Task ExecuteBatchAsync(
        Container container,
        List<CosmosDbEventEntity> events,
        PartitionKey partitionKey,
        string streamId,
        CancellationToken cancellationToken)
    {
        var batch = container.CreateTransactionalBatch(partitionKey);
        foreach (var evt in events)
        {
            batch.CreateItem(evt);
        }

        var batchResponse = await batch.ExecuteAsync(cancellationToken);
        if (!batchResponse.IsSuccessStatusCode)
        {
            throw new CosmosDbProcessingException(
                $"Batch operation failed with status {batchResponse.StatusCode} when appending events to stream '{streamId}'.");
        }
    }

    private static async Task CheckStreamNotClosedAsync(Container container, string streamId, CancellationToken cancellationToken)
    {
        // Check cache first to avoid unnecessary CosmosDB query (saves 1 RU per append)
        if (ClosedStreamCache.ContainsKey(streamId))
        {
            throw new EventStreamClosedException(
                streamId,
                $"Cannot append events to closed stream '{streamId}'. " +
                "The stream was closed and may have a continuation stream. Please retry on the active stream.");
        }

        // Query for the stream closed event
        // Note: Using 'c._type' because our custom serializer respects [JsonPropertyName("_type")]
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.streamId = @streamId AND c.eventType = 'EventStream.Closed' AND c._type = 'event'")
            .WithParameter("@streamId", streamId);

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(streamId),
            MaxItemCount = 1
        };

        using var iterator = container.GetItemQueryIterator<CosmosDbEventEntity>(query, requestOptions: queryOptions);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            if (response.Count > 0)
            {
                // Cache this closed status - streams never reopen
                ClosedStreamCache.TryAdd(streamId, 0);

                throw new EventStreamClosedException(
                    streamId,
                    $"Cannot append events to closed stream '{streamId}'. " +
                    "The stream was closed and may have a continuation stream. Please retry on the active stream.");
            }
        }
    }

    /// <summary>
    /// Clears the closed stream cache. Primarily intended for testing scenarios.
    /// </summary>
    /// <remarks>
    /// In production, this should rarely be needed as closed streams remain closed.
    /// However, it can be useful after database resets in test environments.
    /// </remarks>
    public static void ClearClosedStreamCache()
    {
        ClosedStreamCache.Clear();
    }

    private async Task<Container> GetEventsContainerAsync()
    {
        if (eventsContainer != null)
        {
            return eventsContainer;
        }

        var database = await GetOrCreateDatabaseAsync();

        if (settings.AutoCreateContainers)
        {
            await CreateContainerIfNotExistsAsync(database);
        }

        eventsContainer = database.GetContainer(settings.EventsContainerName);
        return eventsContainer;
    }

    private async Task<Database> GetOrCreateDatabaseAsync()
    {
        if (!settings.AutoCreateContainers)
        {
            return cosmosClient.GetDatabase(settings.DatabaseName);
        }

        var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(
            settings.DatabaseName,
            GetThroughputProperties(settings.DatabaseThroughput));

        return databaseResponse.Database;
    }

    private async Task CreateContainerIfNotExistsAsync(Database database)
    {
        var throughput = GetThroughputProperties(settings.EventsThroughput);
        var containerProperties = new ContainerProperties(settings.EventsContainerName, "/streamId")
        {
            DefaultTimeToLive = settings.DefaultTimeToLiveSeconds > 0 ? settings.DefaultTimeToLiveSeconds : -1
        };

        if (throughput != null)
        {
            await database.CreateContainerIfNotExistsAsync(containerProperties, throughput);
        }
        else
        {
            await database.CreateContainerIfNotExistsAsync(containerProperties);
        }
    }

    private static ThroughputProperties? GetThroughputProperties(ThroughputSettings? settings)
    {
        if (settings == null)
        {
            return null;
        }

        if (settings.AutoscaleMaxThroughput.HasValue)
        {
            return ThroughputProperties.CreateAutoscaleThroughput(settings.AutoscaleMaxThroughput.Value);
        }

        if (settings.ManualThroughput.HasValue)
        {
            return ThroughputProperties.CreateManualThroughput(settings.ManualThroughput.Value);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("CosmosDbDataStore.RemoveEventsForFailedCommit");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemCosmosDb);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationDelete);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
        }

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var container = await GetEventsContainerAsync();
        var streamId = document.Active.StreamIdentifier;
        var removed = 0;

        // Delete each event individually
        for (var version = fromVersion; version <= toVersion; version++)
        {
            var id = CosmosDbEventEntity.CreateId(streamId, version);
            try
            {
                await container.DeleteItemAsync<CosmosDbEventEntity>(
                    id,
                    new PartitionKey(streamId));
                removed++;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Event doesn't exist (wasn't written) - that's fine, continue
            }
        }

        activity?.SetTag(FaesSemanticConventions.EventCount, removed);
        return removed;
    }
}
