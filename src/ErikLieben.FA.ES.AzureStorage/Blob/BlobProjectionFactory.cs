using Azure.Storage.Blobs;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.Azure;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Base factory class for creating and managing projections stored in Azure Blob Storage.
/// </summary>
/// <typeparam name="T">The projection type that inherits from <see cref="Projection"/>.</typeparam>
public abstract class BlobProjectionFactory<T> where T : Projection
{
    private readonly IAzureClientFactory<BlobServiceClient> _blobServiceClientFactory;
    private readonly string _connectionName;
    private readonly string _containerOrPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobProjectionFactory{T}"/> class.
    /// </summary>
    /// <param name="blobServiceClientFactory">The factory for creating Azure Blob Service clients.</param>
    /// <param name="connectionName">The name of the Azure client connection.</param>
    /// <param name="containerOrPath">The container name or blob path where the projection is stored.</param>
    protected BlobProjectionFactory(
        IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
        string connectionName,
        string containerOrPath)
    {
        _blobServiceClientFactory = blobServiceClientFactory ?? throw new ArgumentNullException(nameof(blobServiceClientFactory));
        _connectionName = connectionName ?? throw new ArgumentNullException(nameof(connectionName));
        _containerOrPath = containerOrPath ?? throw new ArgumentNullException(nameof(containerOrPath));
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
        await containerClient.CreateIfNotExistsAsync();
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
        blobName ??= $"{typeof(T).Name}.json";

        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(blobName);

        if (await blobClient.ExistsAsync(cancellationToken))
        {
            var downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
            var json = downloadResult.Value.Content.ToString();

            var projection = LoadFromJson(json, documentFactory, eventStreamFactory);
            if (projection != null)
            {
                return projection;
            }
        }

        return New();
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
        if (projection == null) throw new ArgumentNullException(nameof(projection));

        blobName ??= $"{typeof(T).Name}.json";

        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(blobName);

        var json = projection.ToJson();
        var bytes = Encoding.UTF8.GetBytes(json);

        await blobClient.UploadAsync(
            new BinaryData(bytes),
            overwrite: true,
            cancellationToken);
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
}
