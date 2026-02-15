using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Observability;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Snapshots;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Provides a CosmosDB-backed implementation of <see cref="ISnapShotStore"/> for persisting and retrieving aggregate snapshots.
/// Optimized for RU efficiency using streamId as partition key to colocate snapshots with their events.
/// </summary>
public class CosmosDbSnapShotStore : ISnapShotStore
{
    private readonly CosmosClient cosmosClient;
    private readonly EventStreamCosmosDbSettings settings;
    private Container? snapshotsContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbSnapShotStore"/> class.
    /// </summary>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="settings">The CosmosDB settings.</param>
    public CosmosDbSnapShotStore(CosmosClient cosmosClient, EventStreamCosmosDbSettings settings)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(settings);

        this.cosmosClient = cosmosClient;
        this.settings = settings;
    }

    /// <summary>
    /// Persists a snapshot of the aggregate to CosmosDB using the supplied JSON type info.
    /// </summary>
    /// <param name="object">The aggregate instance to snapshot.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type info describing the aggregate type.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot is taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SetAsync(IBase @object, JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var activity = FaesInstrumentation.Storage.StartActivity("CosmosDbSnapShotStore.Set");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemCosmosDb);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationWrite);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var container = await GetSnapshotsContainerAsync();

        var streamId = document.Active.StreamIdentifier;
        var data = JsonSerializer.Serialize(@object, jsonTypeInfo);

        var entity = new CosmosDbSnapshotEntity
        {
            Id = CosmosDbSnapshotEntity.CreateId(streamId, version, name),
            StreamId = streamId,
            Version = version,
            Name = name,
            Data = data,
            DataType = @object.GetType().FullName ?? @object.GetType().Name,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var partitionKey = new PartitionKey(streamId);

        // Upsert to handle updates to existing snapshots
        await container.UpsertItemAsync(entity, partitionKey, cancellationToken: cancellationToken);

        // Record snapshot metrics
        FaesMetrics.RecordSnapshotCreated(document.ObjectName ?? "unknown");
    }

    /// <summary>
    /// Retrieves a snapshot of the aggregate at the specified version using the supplied JSON type info.
    /// Uses point read for optimal RU efficiency (1 RU).
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="jsonTypeInfo">The source-generated JSON type info for <typeparamref name="T"/>.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot was taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The deserialized snapshot instance when found; otherwise null.</returns>
    public async Task<T?> GetAsync<T>(JsonTypeInfo<T> jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default) where T : class, IBase
    {
        ArgumentNullException.ThrowIfNull(document);

        using var activity = FaesInstrumentation.Storage.StartActivity("CosmosDbSnapShotStore.Get");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemCosmosDb);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var container = await GetSnapshotsContainerAsync();

        var streamId = document.Active.StreamIdentifier;
        var documentId = CosmosDbSnapshotEntity.CreateId(streamId, version, name);
        var partitionKey = new PartitionKey(streamId);

        try
        {
            // Point read = 1 RU
            var response = await container.ReadItemAsync<CosmosDbSnapshotEntity>(documentId, partitionKey, cancellationToken: cancellationToken);
            return JsonSerializer.Deserialize(response.Resource.Data, jsonTypeInfo);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves a snapshot as an untyped object at the specified version using the supplied JSON type info.
    /// Uses point read for optimal RU efficiency (1 RU).
    /// </summary>
    /// <param name="jsonTypeInfo">The source-generated JSON type info representing the runtime type of the snapshot.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot was taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The deserialized snapshot instance when found; otherwise null.</returns>
    public async Task<object?> GetAsync(JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var activity = FaesInstrumentation.Storage.StartActivity("CosmosDbSnapShotStore.Get");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemCosmosDb);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var container = await GetSnapshotsContainerAsync();

        var streamId = document.Active.StreamIdentifier;
        var documentId = CosmosDbSnapshotEntity.CreateId(streamId, version, name);
        var partitionKey = new PartitionKey(streamId);

        try
        {
            // Point read = 1 RU
            var response = await container.ReadItemAsync<CosmosDbSnapshotEntity>(documentId, partitionKey, cancellationToken: cancellationToken);
            return JsonSerializer.Deserialize(response.Resource.Data, jsonTypeInfo);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SnapshotMetadata>> ListSnapshotsAsync(
        IObjectDocument document,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("CosmosDbSnapShotStore.List");
        ArgumentNullException.ThrowIfNull(document);

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemCosmosDb);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationQuery);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
        }

        var container = await GetSnapshotsContainerAsync();
        var streamId = document.Active.StreamIdentifier;
        var partitionKey = new PartitionKey(streamId);

        var snapshots = new List<SnapshotMetadata>();

        // Query for all snapshots in this partition
        var query = new QueryDefinition("SELECT * FROM c WHERE c.streamId = @streamId")
            .WithParameter("@streamId", streamId);

        using var iterator = container.GetItemQueryIterator<CosmosDbSnapshotEntity>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = partitionKey });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            foreach (var entity in response)
            {
                var metadata = new SnapshotMetadata(
                    entity.Version,
                    entity.CreatedAt,
                    entity.Name,
                    entity.Data?.Length);
                snapshots.Add(metadata);
            }
        }

        activity?.SetTag("faes.snapshot.count", snapshots.Count);

        return snapshots.OrderByDescending(s => s.Version).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        IObjectDocument document,
        int version,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("CosmosDbSnapShotStore.Delete");
        ArgumentNullException.ThrowIfNull(document);

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemCosmosDb);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationDelete);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var container = await GetSnapshotsContainerAsync();
        var streamId = document.Active.StreamIdentifier;
        var documentId = CosmosDbSnapshotEntity.CreateId(streamId, version, name);
        var partitionKey = new PartitionKey(streamId);

        try
        {
            await container.DeleteItemAsync<CosmosDbSnapshotEntity>(
                documentId,
                partitionKey,
                cancellationToken: cancellationToken);
            activity?.SetTag(FaesSemanticConventions.Success, true);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            activity?.SetTag(FaesSemanticConventions.Success, false);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteManyAsync(
        IObjectDocument document,
        IEnumerable<int> versions,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("CosmosDbSnapShotStore.DeleteMany");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemCosmosDb);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationDelete);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
        }

        var deleted = 0;
        foreach (var version in versions)
        {
            if (await DeleteAsync(document, version, cancellationToken: cancellationToken))
            {
                deleted++;
            }
        }

        activity?.SetTag("faes.snapshot.deleted_count", deleted);

        return deleted;
    }

    private async Task<Container> GetSnapshotsContainerAsync()
    {
        if (snapshotsContainer != null)
        {
            return snapshotsContainer;
        }

        Database database;
        if (settings.AutoCreateContainers)
        {
            // Create database if it doesn't exist
            var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(
                settings.DatabaseName,
                settings.DatabaseThroughput != null
                    ? GetThroughputProperties(settings.DatabaseThroughput)
                    : null);
            database = databaseResponse.Database;
        }
        else
        {
            database = cosmosClient.GetDatabase(settings.DatabaseName);
        }

        if (settings.AutoCreateContainers)
        {
            var throughput = GetThroughputProperties(settings.SnapshotsThroughput);
            var containerProperties = new ContainerProperties(settings.SnapshotsContainerName, "/streamId");

            if (throughput != null)
            {
                await database.CreateContainerIfNotExistsAsync(containerProperties, throughput);
            }
            else
            {
                await database.CreateContainerIfNotExistsAsync(containerProperties);
            }
        }

        snapshotsContainer = database.GetContainer(settings.SnapshotsContainerName);
        return snapshotsContainer;
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
}
