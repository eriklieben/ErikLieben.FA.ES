using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.VersionTokenParts;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Base factory class for creating and managing projections stored in Azure CosmosDB.
/// </summary>
/// <typeparam name="T">The projection type that inherits from <see cref="Projection"/>.</typeparam>
public abstract class CosmosDbProjectionFactory<T> : IProjectionFactory<T>, IProjectionFactory where T : Projection
{
    private readonly CosmosClient _cosmosClient;
    private readonly EventStreamCosmosDbSettings _settings;
    private readonly string _containerName;
    private readonly string _partitionKeyPath;
    private Container? _projectionsContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbProjectionFactory{T}"/> class.
    /// </summary>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="settings">The CosmosDB settings.</param>
    /// <param name="containerName">Optional container name override. If not provided, uses settings default.</param>
    /// <param name="partitionKeyPath">The partition key path. Defaults to "/projectionName".</param>
    protected CosmosDbProjectionFactory(
        CosmosClient cosmosClient,
        EventStreamCosmosDbSettings settings,
        string? containerName = null,
        string partitionKeyPath = "/projectionName")
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _containerName = containerName ?? settings.ProjectionsContainerName;
        _partitionKeyPath = partitionKeyPath;
    }

    /// <summary>
    /// Gets a value indicating whether the projection uses an external checkpoint.
    /// </summary>
    protected abstract bool HasExternalCheckpoint { get; }

    /// <summary>
    /// Creates a new instance of the projection.
    /// </summary>
    /// <returns>A new projection instance.</returns>
    protected abstract T New();

    /// <summary>
    /// Loads a projection from JSON using the generated LoadFromJson method.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <returns>The loaded projection instance, or null if deserialization fails.</returns>
    protected abstract T? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory);

    /// <summary>
    /// Gets the projections container, creating it if necessary.
    /// </summary>
    /// <returns>The container.</returns>
    protected async Task<Container> GetProjectionsContainerAsync()
    {
        if (_projectionsContainer != null)
        {
            return _projectionsContainer;
        }

        var database = _cosmosClient.GetDatabase(_settings.DatabaseName);

        if (_settings.AutoCreateContainers)
        {
            var throughput = GetThroughputProperties(_settings.ProjectionsThroughput);
            var containerProperties = new ContainerProperties(_containerName, _partitionKeyPath);

            if (throughput != null)
            {
                await database.CreateContainerIfNotExistsAsync(containerProperties, throughput);
            }
            else
            {
                await database.CreateContainerIfNotExistsAsync(containerProperties);
            }
        }

        _projectionsContainer = database.GetContainer(_containerName);
        return _projectionsContainer;
    }

    /// <summary>
    /// Loads the projection from CosmosDB, or creates a new instance if it doesn't exist.
    /// </summary>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <param name="blobName">Optional document ID. If not provided, uses a default ID based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded or newly created projection instance.</returns>
    public virtual async Task<T> GetOrCreateAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetProjectionsContainerAsync();

        try
        {
            var response = await container.ReadItemAsync<ProjectionDocument>(
                blobName,
                partitionKey,
                cancellationToken: cancellationToken);

            var projection = LoadFromJson(response.Resource.Data, documentFactory, eventStreamFactory);
            if (projection != null)
            {
                // If external checkpoint is enabled, load it separately using the CheckpointFingerprint
                if (HasExternalCheckpoint && !string.IsNullOrEmpty(projection.CheckpointFingerprint))
                {
                    var checkpoint = await LoadCheckpointAsync(projection.CheckpointFingerprint, cancellationToken);
                    if (checkpoint != null)
                    {
                        projection.Checkpoint = checkpoint;
                    }
                }

                return projection;
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Document doesn't exist, return new instance
        }

        return New();
    }

    /// <summary>
    /// Saves the projection to CosmosDB.
    /// </summary>
    /// <param name="projection">The projection to save.</param>
    /// <param name="blobName">Optional document ID. If not provided, uses a default ID based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task SaveAsync(
        T projection,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        blobName ??= typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetProjectionsContainerAsync();

        var json = projection.ToJson();
        var document = new ProjectionDocument
        {
            Id = blobName,
            ProjectionName = projectionName,
            Data = json,
            LastModified = DateTimeOffset.UtcNow
        };

        await container.UpsertItemAsync(
            document,
            partitionKey,
            cancellationToken: cancellationToken);

        // If external checkpoint is enabled and fingerprint is set, save it separately
        if (HasExternalCheckpoint && !string.IsNullOrEmpty(projection.CheckpointFingerprint))
        {
            await SaveCheckpointAsync(projection, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes the projection from CosmosDB.
    /// </summary>
    /// <param name="blobName">Optional document ID. If not provided, uses a default ID based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task DeleteAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetProjectionsContainerAsync();

        try
        {
            await container.DeleteItemAsync<ProjectionDocument>(
                blobName,
                partitionKey,
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already deleted or doesn't exist
        }
    }

    /// <summary>
    /// Checks if the projection exists in CosmosDB.
    /// </summary>
    /// <param name="blobName">Optional document ID. If not provided, uses a default ID based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the projection exists; otherwise, false.</returns>
    public virtual async Task<bool> ExistsAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetProjectionsContainerAsync();

        try
        {
            await container.ReadItemAsync<ProjectionDocument>(
                blobName,
                partitionKey,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the last modified timestamp of the projection.
    /// </summary>
    /// <param name="blobName">Optional document ID. If not provided, uses a default ID based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The last modified timestamp, or null if the projection doesn't exist.</returns>
    public virtual async Task<DateTimeOffset?> GetLastModifiedAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetProjectionsContainerAsync();

        try
        {
            var response = await container.ReadItemAsync<ProjectionDocument>(
                blobName,
                partitionKey,
                cancellationToken: cancellationToken);
            return response.Resource.LastModified;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves a checkpoint to external CosmosDB storage using the projection's CheckpointFingerprint.
    /// </summary>
    /// <param name="projection">The projection containing the checkpoint to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected virtual async Task SaveCheckpointAsync(
        T projection,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(projection.CheckpointFingerprint))
        {
            return;
        }

        var checkpointId = $"checkpoint-{typeof(T).Name}-{projection.CheckpointFingerprint}";
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetProjectionsContainerAsync();

        // Check if it already exists (checkpoints are immutable)
        try
        {
            await container.ReadItemAsync<CheckpointDocument>(
                checkpointId,
                partitionKey,
                cancellationToken: cancellationToken);
            // Already exists, don't overwrite
            return;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Doesn't exist, create it
        }

        var json = JsonSerializer.Serialize(projection.Checkpoint, CosmosDbCheckpointJsonContext.Default.Checkpoint);
        var checkpointDoc = new CheckpointDocument
        {
            Id = checkpointId,
            ProjectionName = projectionName,
            Fingerprint = projection.CheckpointFingerprint,
            Data = json,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await container.CreateItemAsync(
            checkpointDoc,
            partitionKey,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Loads a checkpoint from external CosmosDB storage using the projection's CheckpointFingerprint.
    /// </summary>
    /// <param name="checkpointFingerprint">The checkpoint fingerprint to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checkpoint, or null if it doesn't exist.</returns>
    protected virtual async Task<Checkpoint?> LoadCheckpointAsync(
        string checkpointFingerprint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(checkpointFingerprint))
        {
            return null;
        }

        var checkpointId = $"checkpoint-{typeof(T).Name}-{checkpointFingerprint}";
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetProjectionsContainerAsync();

        try
        {
            var response = await container.ReadItemAsync<CheckpointDocument>(
                checkpointId,
                partitionKey,
                cancellationToken: cancellationToken);

            return JsonSerializer.Deserialize(response.Resource.Data, CosmosDbCheckpointJsonContext.Default.Checkpoint);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
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
    public Type ProjectionType => typeof(T);

    /// <inheritdoc />
    public async Task<Projection> GetOrCreateProjectionAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        return await GetOrCreateAsync(documentFactory, eventStreamFactory, blobName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveProjectionAsync(
        Projection projection,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        if (projection is not T typedProjection)
        {
            throw new ArgumentException($"Projection must be of type {typeof(T).Name}", nameof(projection));
        }

        await SaveAsync(typedProjection, blobName, cancellationToken);
    }
}

/// <summary>
/// CosmosDB document wrapper for storing projection data.
/// </summary>
internal class ProjectionDocument
{
    /// <summary>
    /// The unique document ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The projection name (partition key).
    /// </summary>
    [JsonPropertyName("projectionName")]
    public string ProjectionName { get; set; } = string.Empty;

    /// <summary>
    /// The serialized projection JSON data.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// The last modified timestamp.
    /// </summary>
    [JsonPropertyName("lastModified")]
    public DateTimeOffset LastModified { get; set; }
}

/// <summary>
/// CosmosDB document wrapper for storing checkpoint data.
/// </summary>
internal class CheckpointDocument
{
    /// <summary>
    /// The unique document ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The projection name (partition key).
    /// </summary>
    [JsonPropertyName("projectionName")]
    public string ProjectionName { get; set; } = string.Empty;

    /// <summary>
    /// The checkpoint fingerprint.
    /// </summary>
    [JsonPropertyName("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>
    /// The serialized checkpoint JSON data.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// The creation timestamp.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// AOT-compatible JSON serializer context for Checkpoint serialization in CosmosDB.
/// </summary>
[JsonSerializable(typeof(Checkpoint))]
[JsonSerializable(typeof(ObjectIdentifier))]
[JsonSerializable(typeof(VersionIdentifier))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class CosmosDbCheckpointJsonContext : JsonSerializerContext
{
}
