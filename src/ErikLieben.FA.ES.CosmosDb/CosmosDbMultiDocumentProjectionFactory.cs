using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Factory for creating and managing multi-document projections stored in Azure CosmosDB.
/// </summary>
/// <typeparam name="T">The projection type that inherits from <see cref="MultiDocumentProjection"/>.</typeparam>
public abstract class CosmosDbMultiDocumentProjectionFactory<T> : IProjectionFactory<T>, IProjectionFactory where T : MultiDocumentProjection
{
    private readonly CosmosClient _cosmosClient;
    private readonly EventStreamCosmosDbSettings _settings;
    private readonly string _containerName;
    private readonly string _partitionKeyPath;
    private Container? _documentsContainer;
    private Container? _checkpointContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbMultiDocumentProjectionFactory{T}"/> class.
    /// </summary>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="settings">The CosmosDB settings.</param>
    /// <param name="containerName">The container name for storing projection documents.</param>
    /// <param name="partitionKeyPath">The partition key path. Defaults to "/partitionKey".</param>
    protected CosmosDbMultiDocumentProjectionFactory(
        CosmosClient cosmosClient,
        EventStreamCosmosDbSettings settings,
        string containerName,
        string partitionKeyPath = "/partitionKey")
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
        _partitionKeyPath = partitionKeyPath;
    }

    /// <summary>
    /// Creates a new instance of the projection.
    /// </summary>
    /// <returns>A new projection instance.</returns>
    protected abstract T New();

    /// <summary>
    /// Loads a projection from JSON (checkpoint data only) using the generated LoadFromJson method.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <returns>The loaded projection instance, or null if deserialization fails.</returns>
    protected abstract T? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory);

    /// <summary>
    /// Serializes a document to JSON for storage. Override to provide custom serialization.
    /// </summary>
    /// <param name="document">The document to serialize.</param>
    /// <returns>The serialized JSON string.</returns>
    protected virtual string SerializeDocument(object document)
    {
        return JsonSerializer.Serialize(document, document.GetType(), MultiDocumentJsonContext.Default);
    }

    /// <summary>
    /// Gets the documents container, creating it if necessary.
    /// </summary>
    /// <returns>The container.</returns>
    protected async Task<Container> GetDocumentsContainerAsync()
    {
        if (_documentsContainer != null)
        {
            return _documentsContainer;
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

        _documentsContainer = database.GetContainer(_containerName);
        return _documentsContainer;
    }

    /// <summary>
    /// Gets the checkpoint container, creating it if necessary.
    /// </summary>
    /// <returns>The container.</returns>
    protected async Task<Container> GetCheckpointContainerAsync()
    {
        if (_checkpointContainer != null)
        {
            return _checkpointContainer;
        }

        var database = _cosmosClient.GetDatabase(_settings.DatabaseName);
        var checkpointContainerName = $"{_containerName}-checkpoints";

        if (_settings.AutoCreateContainers)
        {
            var containerProperties = new ContainerProperties(checkpointContainerName, "/projectionName");
            await database.CreateContainerIfNotExistsAsync(containerProperties);
        }

        _checkpointContainer = database.GetContainer(checkpointContainerName);
        return _checkpointContainer;
    }

    /// <inheritdoc />
    public virtual async Task<T> GetOrCreateAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionId = blobName ?? typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetCheckpointContainerAsync();

        try
        {
            var response = await container.ReadItemAsync<MultiDocumentCheckpoint>(
                projectionId,
                partitionKey,
                cancellationToken: cancellationToken);

            var projection = LoadFromJson(response.Resource.Data, documentFactory, eventStreamFactory);
            if (projection != null)
            {
                return projection;
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Checkpoint doesn't exist, return new instance
        }

        return New();
    }

    /// <inheritdoc />
    public virtual async Task SaveAsync(
        T projection,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var documentsContainer = await GetDocumentsContainerAsync();
        var pendingDocuments = projection.GetPendingDocuments();

        // Bulk insert all pending documents
        foreach (var document in pendingDocuments)
        {
            var json = SerializeDocument(document);

            // Extract partition key from the document
            var partitionKeyValue = ExtractPartitionKey(document);
            var partitionKey = new PartitionKey(partitionKeyValue);

            // Create a wrapper document with the id and partition key at the root level
            var wrapper = CreateDocumentWrapper(document, json);

            await documentsContainer.CreateItemAsync(
                wrapper,
                partitionKey,
                cancellationToken: cancellationToken);
        }

        projection.ClearPendingDocuments();

        // Save checkpoint metadata
        await SaveCheckpointAsync(projection, blobName, cancellationToken);
    }

    /// <summary>
    /// Extracts the partition key value from a document.
    /// Override this method to provide custom partition key extraction logic.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The partition key value.</returns>
    /// <remarks>
    /// This default implementation uses reflection. For AOT compatibility,
    /// override this method in derived classes to avoid reflection.
    /// </remarks>
    [RequiresUnreferencedCode("Uses reflection to extract PartitionKey property. Override in derived class for AOT compatibility.")]
    protected virtual string ExtractPartitionKey(object document)
    {
        // Try to get PartitionKey property via reflection (fallback)
        var partitionKeyProperty = document.GetType().GetProperty("PartitionKey");
        if (partitionKeyProperty != null)
        {
            var value = partitionKeyProperty.GetValue(document);
            if (value != null)
            {
                return value.ToString() ?? typeof(T).Name;
            }
        }

        // Default to projection type name
        return typeof(T).Name;
    }

    /// <summary>
    /// Creates a wrapper document for CosmosDB storage with required id and partitionKey fields.
    /// </summary>
    /// <param name="document">The original document.</param>
    /// <param name="serializedJson">The serialized JSON of the document.</param>
    /// <returns>A dictionary representing the wrapper document.</returns>
    /// <remarks>
    /// This default implementation uses reflection. For AOT compatibility,
    /// override this method in derived classes to avoid reflection.
    /// </remarks>
    [RequiresUnreferencedCode("Uses reflection and dynamic JSON deserialization. Override in derived class for AOT compatibility.")]
    [RequiresDynamicCode("Uses dynamic JSON deserialization. Override in derived class for AOT compatibility.")]
    protected virtual Dictionary<string, object> CreateDocumentWrapper(object document, string serializedJson)
    {
        var wrapper = new Dictionary<string, object>();

        // Generate unique ID
        var id = Guid.NewGuid().ToString();

        // Try to get id from document if it exists
        var idProperty = document.GetType().GetProperty("Id");
        if (idProperty != null)
        {
            var idValue = idProperty.GetValue(document);
            if (idValue != null)
            {
                id = idValue.ToString() ?? id;
            }
        }

        wrapper["id"] = id;
        wrapper["partitionKey"] = ExtractPartitionKey(document);

        // Deserialize the document and merge properties
        var docDict = JsonSerializer.Deserialize<Dictionary<string, object>>(serializedJson);
        if (docDict != null)
        {
            foreach (var kvp in docDict)
            {
                if (kvp.Key != "id" && kvp.Key != "partitionKey")
                {
                    wrapper[kvp.Key] = kvp.Value;
                }
            }
        }

        return wrapper;
    }

    /// <summary>
    /// Saves the checkpoint metadata for the projection.
    /// </summary>
    /// <param name="projection">The projection to save checkpoint for.</param>
    /// <param name="projectionId">Optional projection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected virtual async Task SaveCheckpointAsync(
        T projection,
        string? projectionId = null,
        CancellationToken cancellationToken = default)
    {
        projectionId ??= typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetCheckpointContainerAsync();

        var json = projection.ToJson();
        var checkpoint = new MultiDocumentCheckpoint
        {
            Id = projectionId,
            ProjectionName = projectionName,
            Data = json,
            CheckpointFingerprint = projection.CheckpointFingerprint ?? string.Empty,
            LastUpdated = DateTimeOffset.UtcNow
        };

        await container.UpsertItemAsync(
            checkpoint,
            partitionKey,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionId = blobName ?? typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetCheckpointContainerAsync();

        try
        {
            await container.ReadItemAsync<MultiDocumentCheckpoint>(
                projectionId,
                partitionKey,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<DateTimeOffset?> GetLastModifiedAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionId = blobName ?? typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetCheckpointContainerAsync();

        try
        {
            var response = await container.ReadItemAsync<MultiDocumentCheckpoint>(
                projectionId,
                partitionKey,
                cancellationToken: cancellationToken);
            return response.Resource.LastUpdated;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes the checkpoint from CosmosDB. Note: This does not delete the individual documents.
    /// </summary>
    /// <param name="blobName">Optional projection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task DeleteAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionId = blobName ?? typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetCheckpointContainerAsync();

        try
        {
            await container.DeleteItemAsync<MultiDocumentCheckpoint>(
                projectionId,
                partitionKey,
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already deleted
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

    /// <inheritdoc />
    public virtual async Task SetStatusAsync(
        ProjectionStatus status,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionId = blobName ?? typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetCheckpointContainerAsync();

        try
        {
            // Try to read existing checkpoint
            var response = await container.ReadItemAsync<MultiDocumentCheckpoint>(
                projectionId,
                partitionKey,
                cancellationToken: cancellationToken);

            // Update status
            var checkpoint = response.Resource;
            checkpoint.Status = (int)status;
            checkpoint.StatusUpdatedAt = DateTimeOffset.UtcNow;

            await container.ReplaceItemAsync(
                checkpoint,
                projectionId,
                partitionKey,
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Create minimal checkpoint with just status
            var checkpoint = new MultiDocumentCheckpoint
            {
                Id = projectionId,
                ProjectionName = projectionName,
                Data = "{}",
                CheckpointFingerprint = string.Empty,
                LastUpdated = DateTimeOffset.UtcNow,
                Status = (int)status,
                StatusUpdatedAt = DateTimeOffset.UtcNow
            };

            await container.CreateItemAsync(
                checkpoint,
                partitionKey,
                cancellationToken: cancellationToken);
        }
    }

    /// <inheritdoc />
    public virtual async Task<ProjectionStatus> GetStatusAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionId = blobName ?? typeof(T).Name;
        var projectionName = typeof(T).Name;
        var partitionKey = new PartitionKey(projectionName);

        var container = await GetCheckpointContainerAsync();

        try
        {
            var response = await container.ReadItemAsync<MultiDocumentCheckpoint>(
                projectionId,
                partitionKey,
                cancellationToken: cancellationToken);

            if (response.Resource.Status.HasValue)
            {
                return (ProjectionStatus)response.Resource.Status.Value;
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Checkpoint doesn't exist
        }

        return ProjectionStatus.Active;
    }
}

/// <summary>
/// Checkpoint document for multi-document projections.
/// </summary>
internal class MultiDocumentCheckpoint
{
    /// <summary>
    /// Gets or sets the document ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the projection name (partition key).
    /// </summary>
    [JsonPropertyName("projectionName")]
    public string ProjectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the checkpoint data (serialized projection state).
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the checkpoint fingerprint.
    /// </summary>
    [JsonPropertyName("checkpointFingerprint")]
    public string CheckpointFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last updated timestamp.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; set; }

    /// <summary>
    /// Gets or sets the operational status of the projection (as integer for AOT compatibility).
    /// </summary>
    [JsonPropertyName("status")]
    public int? Status { get; set; }

    /// <summary>
    /// Gets or sets when the status was last updated.
    /// </summary>
    [JsonPropertyName("statusUpdatedAt")]
    public DateTimeOffset? StatusUpdatedAt { get; set; }
}

/// <summary>
/// JSON serialization context for multi-document projections.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(MultiDocumentCheckpoint))]
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class MultiDocumentJsonContext : JsonSerializerContext
{
}
