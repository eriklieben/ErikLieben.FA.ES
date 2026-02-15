using Azure.Storage.Blobs;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Observability;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.Azure;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Base factory class for creating and managing projections stored in Azure Blob Storage.
/// </summary>
/// <typeparam name="T">The projection type that inherits from <see cref="Projection"/>.</typeparam>
public abstract class BlobProjectionFactory<T> : IProjectionFactory<T>, IProjectionFactory where T : Projection
{
    private const string StatusPropertyName = "$status";

    private readonly IAzureClientFactory<BlobServiceClient> _blobServiceClientFactory;
    private readonly string _connectionName;
    private readonly string _containerOrPath;
    private readonly bool _autoCreateContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobProjectionFactory{T}"/> class.
    /// </summary>
    /// <param name="blobServiceClientFactory">The factory for creating Azure Blob Service clients.</param>
    /// <param name="connectionName">The name of the Azure client connection.</param>
    /// <param name="containerOrPath">The container name or blob path where the projection is stored.</param>
    /// <param name="autoCreateContainer">A value indicating whether the target blob container is created automatically when missing. Defaults to true.</param>
    protected BlobProjectionFactory(
        IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
        string connectionName,
        string containerOrPath,
        bool autoCreateContainer = true)
    {
        ArgumentNullException.ThrowIfNull(blobServiceClientFactory);
        ArgumentNullException.ThrowIfNull(connectionName);
        ArgumentNullException.ThrowIfNull(containerOrPath);

        _blobServiceClientFactory = blobServiceClientFactory;
        _connectionName = connectionName;
        _containerOrPath = containerOrPath;
        _autoCreateContainer = autoCreateContainer;
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
    /// Gets the blob service client for the configured connection.
    /// </summary>
    /// <returns>The <see cref="BlobServiceClient"/>.</returns>
    protected BlobServiceClient GetBlobServiceClient()
    {
        return _blobServiceClientFactory.CreateClient(_connectionName);
    }

    /// <summary>
    /// Gets the container client for the configured container.
    /// </summary>
    /// <returns>The container client.</returns>
    protected async Task<Azure.Storage.Blobs.BlobContainerClient> GetContainerClientAsync()
    {
        var blobServiceClient = GetBlobServiceClient();
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerOrPath);

        if (_autoCreateContainer)
        {
            await containerClient.CreateIfNotExistsAsync();
        }

        return containerClient;
    }

    /// <summary>
    /// Loads the projection from blob storage, or creates a new instance if it doesn't exist.
    /// </summary>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <param name="blobName">Optional blob name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded or newly created projection instance.</returns>
    public virtual async Task<T> GetOrCreateAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("BlobProjectionFactory.GetOrCreate");

        blobName ??= $"{typeof(T).Name}.json";

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.ProjectionType, typeof(T).Name);
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureBlob);
            activity.SetTag(FaesSemanticConventions.DbName, _containerOrPath);
        }

        var projection = await TryLoadExistingProjectionAsync(documentFactory, eventStreamFactory, blobName, cancellationToken);
        if (projection != null)
        {
            await TryLoadExternalCheckpointAsync(projection, cancellationToken);

            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag(FaesSemanticConventions.LoadedFromCache, true);
            }

            return projection;
        }

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.LoadedFromCache, false);
        }

        return New();
    }

    private async Task<T?> TryLoadExistingProjectionAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string blobName,
        CancellationToken cancellationToken)
    {
        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
        var json = downloadResult.Value.Content.ToString();

        return LoadFromJson(json, documentFactory, eventStreamFactory);
    }

    private async Task TryLoadExternalCheckpointAsync(T projection, CancellationToken cancellationToken)
    {
        if (!HasExternalCheckpoint || string.IsNullOrEmpty(projection.CheckpointFingerprint))
        {
            return;
        }

        var checkpoint = await LoadCheckpointAsync(projection.CheckpointFingerprint, cancellationToken);
        if (checkpoint != null)
        {
            projection.Checkpoint = checkpoint;
        }
    }

    /// <summary>
    /// Saves the projection to blob storage.
    /// </summary>
    /// <param name="projection">The projection to save.</param>
    /// <param name="blobName">Optional blob name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task SaveAsync(
        T projection,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("BlobProjectionFactory.Save");

        ArgumentNullException.ThrowIfNull(projection);

        blobName ??= $"{typeof(T).Name}.json";

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.ProjectionType, typeof(T).Name);
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureBlob);
            activity.SetTag(FaesSemanticConventions.DbName, _containerOrPath);
            activity.SetTag(FaesSemanticConventions.ProjectionStatus, projection.Status.ToString());
        }

        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(blobName);

        var json = projection.ToJson();
        var bytes = Encoding.UTF8.GetBytes(json);

        await blobClient.UploadAsync(
            new BinaryData(bytes),
            overwrite: true,
            cancellationToken);

        // If external checkpoint is enabled and fingerprint is set, save it separately
        // (fingerprint is only set after events have been processed)
        if (HasExternalCheckpoint && !string.IsNullOrEmpty(projection.CheckpointFingerprint))
        {
            await SaveCheckpointAsync(projection, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes the projection from blob storage.
    /// </summary>
    /// <param name="blobName">Optional blob name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task DeleteAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= $"{typeof(T).Name}.json";

        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Checks if the projection exists in blob storage.
    /// </summary>
    /// <param name="blobName">Optional blob name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the projection exists; otherwise, false.</returns>
    public virtual async Task<bool> ExistsAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= $"{typeof(T).Name}.json";

        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(blobName);

        return await blobClient.ExistsAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the last modified timestamp of the projection blob.
    /// </summary>
    /// <param name="blobName">Optional blob name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The last modified timestamp, or null if the blob doesn't exist.</returns>
    public virtual async Task<DateTimeOffset?> GetLastModifiedAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= $"{typeof(T).Name}.json";

        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        return properties.Value.LastModified;
    }

    /// <summary>
    /// Saves a checkpoint to external blob storage using the projection's CheckpointFingerprint.
    /// </summary>
    /// <param name="projection">The projection containing the checkpoint to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected virtual async Task SaveCheckpointAsync(
        T projection,
        CancellationToken cancellationToken = default)
    {
        // Skip saving if no checkpoint fingerprint (no events processed yet)
        if (string.IsNullOrEmpty(projection.CheckpointFingerprint))
        {
            return;
        }

        var checkpointBlobName = $"checkpoints/{typeof(T).Name}/{projection.CheckpointFingerprint}.json";

        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(checkpointBlobName);

        // Only save if it doesn't already exist (checkpoints are immutable)
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            var json = JsonSerializer.Serialize(projection.Checkpoint, CheckpointJsonContext.Default.Checkpoint);
            var bytes = Encoding.UTF8.GetBytes(json);

            await blobClient.UploadAsync(
                new BinaryData(bytes),
                overwrite: false,
                cancellationToken);
        }
    }

    /// <summary>
    /// Loads a checkpoint from external blob storage using the projection's CheckpointFingerprint.
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

        var checkpointBlobName = $"checkpoints/{typeof(T).Name}/{checkpointFingerprint}.json";
        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(checkpointBlobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
        var json = downloadResult.Value.Content.ToString();

        return JsonSerializer.Deserialize(json, CheckpointJsonContext.Default.Checkpoint);
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
        blobName ??= $"{typeof(T).Name}.json";

        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            // Create a minimal projection with the status set
            var statusJson = JsonSerializer.Serialize(new { Status = status }, StatusOnlyJsonContext.Default.StatusOnly);
            var statusBytes = Encoding.UTF8.GetBytes(statusJson);
            await blobClient.UploadAsync(new BinaryData(statusBytes), overwrite: true, cancellationToken);
            return;
        }

        // Read existing projection, update status, and save back
        var downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
        var json = downloadResult.Value.Content.ToString();

        // Parse and update status using JsonDocument
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Build a new JSON object with the updated status
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == StatusPropertyName)
                {
                    writer.WriteNumber(StatusPropertyName, (int)status);
                }
                else
                {
                    property.WriteTo(writer);
                }
            }
            // If $status wasn't in the original, add it
            if (!root.TryGetProperty(StatusPropertyName, out _))
            {
                writer.WriteNumber(StatusPropertyName, (int)status);
            }
            writer.WriteEndObject();
        }

        var updatedJson = Encoding.UTF8.GetString(stream.ToArray());
        var bytes = Encoding.UTF8.GetBytes(updatedJson);
        await blobClient.UploadAsync(new BinaryData(bytes), overwrite: true, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<ProjectionStatus> GetStatusAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= $"{typeof(T).Name}.json";

        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return ProjectionStatus.Active;
        }

        // Read just enough to get the status property
        var downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
        var json = downloadResult.Value.Content.ToString();

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(StatusPropertyName, out var statusElement)
            && statusElement.TryGetInt32(out var statusValue))
        {
            return (ProjectionStatus)statusValue;
        }

        return ProjectionStatus.Active;
    }
}

/// <summary>
/// Helper type for serializing just the status property.
/// </summary>
internal record StatusOnly
{
    [System.Text.Json.Serialization.JsonPropertyName("$status")]
    public ProjectionStatus Status { get; init; }
}

/// <summary>
/// JSON serializer context for status-only serialization.
/// </summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(StatusOnly))]
internal partial class StatusOnlyJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
