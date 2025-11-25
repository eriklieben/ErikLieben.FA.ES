using Azure.Storage.Blobs;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.Azure;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Base factory class for creating and managing routed projections stored in Azure Blob Storage.
/// The main projection file (e.g., kanban.json) contains $checkpoint and $metadata.
/// Each destination is stored in a separate file (e.g., kanban/project-123.json).
/// </summary>
/// <typeparam name="TProjection">The routed projection type.</typeparam>
public abstract class RoutedBlobProjectionFactory<TProjection>
    : BlobProjectionFactory<TProjection>
    where TProjection : RoutedProjection, new()
{
    private readonly string _pathTemplate;
    private readonly string _blobPath;

    /// <summary>
    /// Creates a new routed blob projection factory.
    /// </summary>
    /// <param name="blobServiceClientFactory">Factory for creating blob service clients.</param>
    /// <param name="connectionName">The connection name for blob storage.</param>
    /// <param name="pathTemplate">The path template from [BlobJsonProjection] attribute.</param>
    /// <param name="autoCreateContainer">Whether to auto-create the container.</param>
    protected RoutedBlobProjectionFactory(
        IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
        string connectionName,
        string pathTemplate,
        bool autoCreateContainer = true)
        : base(
            blobServiceClientFactory,
            connectionName,
            BlobPathTemplateResolver.GetContainerName(pathTemplate),
            autoCreateContainer)
    {
        _pathTemplate = pathTemplate;

        // Extract blob path without container name
        var containerName = BlobPathTemplateResolver.GetContainerName(pathTemplate);
        _blobPath = pathTemplate.StartsWith(containerName + "/")
            ? pathTemplate.Substring(containerName.Length + 1)
            : pathTemplate;
    }

    /// <summary>
    /// Routed projections use external checkpoints stored in the main file.
    /// </summary>
    protected override bool HasExternalCheckpoint => true;

    /// <summary>
    /// Creates a new projection instance.
    /// </summary>
    protected override TProjection New() => new TProjection();

    /// <summary>
    /// Not used for routed projections - use GetOrCreateAsync instead.
    /// </summary>
    protected override TProjection? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
    {
        throw new NotSupportedException("Routed projections use LoadMainProjectionFromJson instead.");
    }

    /// <summary>
    /// Gets the last modified timestamp of the main projection blob.
    /// </summary>
    public override async Task<DateTimeOffset?> GetLastModifiedAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(_blobPath);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        return properties.Value.LastModified;
    }

    /// <summary>
    /// AOT-compatible JSON serializer context for the projection metadata.
    /// Implemented by source generator.
    /// </summary>
    protected abstract JsonSerializerContext GetProjectionJsonContext();

    /// <summary>
    /// Loads destination projection from JSON.
    /// Implemented by source generator.
    /// </summary>
    protected abstract Projection LoadDestinationFromJson(
        string json,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string destinationKey);

    /// <summary>
    /// Checks if a destination type has external checkpoint enabled.
    /// Implemented by source generator.
    /// </summary>
    protected abstract bool DestinationHasExternalCheckpoint(string destinationTypeName);

    /// <summary>
    /// Loads the main projection file and all destination files.
    /// </summary>
    public override async Task<TProjection> GetOrCreateAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerClientAsync();
        var mainBlobClient = containerClient.GetBlobClient(_blobPath);

        TProjection projection;

        if (await mainBlobClient.ExistsAsync(cancellationToken))
        {
            var content = await mainBlobClient.DownloadContentAsync(cancellationToken);
            var json = content.Value.Content.ToString();

            projection = LoadMainProjectionFromJson(json, documentFactory, eventStreamFactory) ?? New();
        }
        else
        {
            projection = New();
        }

        projection.PathTemplate = _blobPath;
        SetFactories(projection, documentFactory, eventStreamFactory);

        // Load each destination from its separate file
        foreach (var (destinationKey, destMetadata) in projection.Registry.Destinations)
        {
            if (!destMetadata.Metadata.TryGetValue("blobPath", out var blobPath))
            {
                continue; // No blob path stored, skip
            }

            var destBlobClient = containerClient.GetBlobClient(blobPath);

            if (await destBlobClient.ExistsAsync(cancellationToken))
            {
                var destContent = await destBlobClient.DownloadContentAsync(cancellationToken);
                var destJson = destContent.Value.Content.ToString();

                var destination = LoadDestinationFromJson(
                    destJson,
                    documentFactory,
                    eventStreamFactory,
                    destinationKey);

                // Re-initialize metadata from registry (in case LoadFromJson didn't restore it)
                if (destMetadata.UserMetadata.Count > 0)
                {
                    destination.InitializeFromMetadata(destMetadata.UserMetadata);
                }

                // Load external checkpoint if the destination type has it enabled
                if (DestinationHasExternalCheckpoint(destMetadata.DestinationTypeName) &&
                    !string.IsNullOrEmpty(destination.CheckpointFingerprint))
                {
                    var checkpoint = await LoadDestinationCheckpointAsync(
                        blobPath,
                        destination.CheckpointFingerprint,
                        cancellationToken);
                    if (checkpoint != null)
                    {
                        destination.Checkpoint = checkpoint;
                    }
                }

                AddDestinationToProjection(projection, destinationKey, destination);
            }
        }

        return projection;
    }

    /// <summary>
    /// Loads the main projection (checkpoint and metadata) from JSON.
    /// Implemented by source generator.
    /// </summary>
    protected abstract TProjection? LoadMainProjectionFromJson(
        string json,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory);

    /// <summary>
    /// Saves the main projection file and all destination files.
    /// </summary>
    public override async Task SaveAsync(
        TProjection projection,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerClientAsync();

        // Update registry metadata
        projection.Registry.LastUpdated = DateTimeOffset.UtcNow;

        // Resolve blob paths for destinations that don't have one yet
        foreach (var (destinationKey, destMetadata) in projection.Registry.Destinations)
        {
            if (!destMetadata.Metadata.TryGetValue("blobPath", out _))
            {
                destMetadata.Metadata["blobPath"] = ResolveDestinationBlobPath(destinationKey, destMetadata);
            }
        }

        // Save main projection file (checkpoint + metadata only)
        var mainJson = SerializeMainProjection(projection);
        await UploadBlobAsync(containerClient, _blobPath, mainJson, cancellationToken);

        // Save each destination to its own file
        var saveTasks = GetDestinationSaveTasks(projection, containerClient, cancellationToken);
        await Task.WhenAll(saveTasks);
    }

    /// <summary>
    /// Resolves the blob path for a destination using its type's [BlobJsonProjection] attribute and user metadata.
    /// Override in generated code for AOT compatibility.
    /// </summary>
    /// <param name="destinationKey">The destination key.</param>
    /// <param name="metadata">The destination metadata containing type name and user metadata.</param>
    /// <returns>The resolved blob path.</returns>
    protected virtual string ResolveDestinationBlobPath(string destinationKey, DestinationMetadata metadata)
    {
        // Try to get path template from destination type
        var pathTemplate = GetDestinationPathTemplate(metadata.DestinationTypeName);

        if (string.IsNullOrEmpty(pathTemplate))
        {
            // Fall back to default: parent path + destination key
            var basePath = _blobPath.Replace(".json", "");
            return $"{basePath}/{destinationKey}.json";
        }

        // Extract just the blob path (remove container prefix if present)
        var containerName = BlobPathTemplateResolver.GetContainerName(pathTemplate);
        var templatePath = pathTemplate.StartsWith(containerName + "/")
            ? pathTemplate.Substring(containerName.Length + 1)
            : pathTemplate;

        // Replace all placeholders in the template with user metadata values
        var resolvedPath = templatePath;
        foreach (var kvp in metadata.UserMetadata)
        {
            resolvedPath = resolvedPath.Replace($"{{{kvp.Key}}}", kvp.Value);
        }

        // Also support {destinationKey} placeholder
        resolvedPath = resolvedPath.Replace("{destinationKey}", destinationKey);

        return resolvedPath;
    }

    /// <summary>
    /// Gets the path template from a destination type's [BlobJsonProjection] attribute.
    /// Override in generated code for AOT compatibility.
    /// </summary>
    /// <param name="destinationTypeName">The destination type name.</param>
    /// <returns>The path template from the attribute, or null if not found.</returns>
    protected virtual string? GetDestinationPathTemplate(string destinationTypeName)
    {
        // Default implementation returns null
        // Generated code should override to return path from [BlobJsonProjection] attribute
        return null;
    }

    /// <summary>
    /// Serializes the main projection (checkpoint and metadata) to JSON.
    /// Implemented by source generator.
    /// </summary>
    protected abstract string SerializeMainProjection(TProjection projection);

    /// <summary>
    /// Gets save tasks for all destinations.
    /// Implemented by source generator.
    /// </summary>
    protected abstract IEnumerable<Task> GetDestinationSaveTasks(
        TProjection projection,
        BlobContainerClient containerClient,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds a loaded destination to the projection.
    /// Implemented by source generator for AOT compatibility.
    /// </summary>
    protected abstract void AddDestinationToProjection(
        TProjection projection,
        string destinationKey,
        Projection destination);

    /// <summary>
    /// Sets the document and event stream factories on the projection.
    /// </summary>
    protected abstract void SetFactories(
        TProjection projection,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory);

    /// <summary>
    /// Uploads content to a blob.
    /// </summary>
    protected async Task UploadBlobAsync(
        BlobContainerClient containerClient,
        string blobPath,
        string content,
        CancellationToken cancellationToken)
    {
        var blobClient = containerClient.GetBlobClient(blobPath);
        var bytes = Encoding.UTF8.GetBytes(content);
        await blobClient.UploadAsync(new BinaryData(bytes), overwrite: true, cancellationToken);
    }

    /// <summary>
    /// Gets the blob path (without container name).
    /// </summary>
    protected string BlobPath => _blobPath;

    /// <summary>
    /// Derives the checkpoint path from a destination's blob path.
    /// For example: "projections/userprofiles/team-members.json" becomes
    /// "projections/userprofiles/checkpoints/team-members/{fingerprint}.json"
    /// </summary>
    /// <param name="destinationBlobPath">The destination's blob path.</param>
    /// <param name="checkpointFingerprint">The checkpoint fingerprint.</param>
    /// <returns>The checkpoint blob path.</returns>
    protected static string GetCheckpointPathFromBlobPath(string destinationBlobPath, string checkpointFingerprint)
    {
        // Get directory (e.g., "projections/userprofiles" from "projections/userprofiles/team-members.json")
        var lastSlashIndex = destinationBlobPath.LastIndexOf('/');
        var directory = lastSlashIndex > 0 ? destinationBlobPath[..lastSlashIndex] : string.Empty;

        // Get filename without extension (e.g., "team-members" or "page-1")
        var fileName = lastSlashIndex > 0
            ? destinationBlobPath[(lastSlashIndex + 1)..]
            : destinationBlobPath;
        var dotIndex = fileName.LastIndexOf('.');
        var fileNameWithoutExtension = dotIndex > 0 ? fileName[..dotIndex] : fileName;

        // Build checkpoint path: {directory}/checkpoints/{filename}/{fingerprint}.json
        return string.IsNullOrEmpty(directory)
            ? $"checkpoints/{fileNameWithoutExtension}/{checkpointFingerprint}.json"
            : $"{directory}/checkpoints/{fileNameWithoutExtension}/{checkpointFingerprint}.json";
    }

    /// <summary>
    /// Saves a checkpoint for a destination to external blob storage.
    /// The checkpoint is stored relative to the destination's blob path.
    /// </summary>
    /// <param name="destinationBlobPath">The destination's blob path.</param>
    /// <param name="destination">The destination projection containing the checkpoint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task SaveDestinationCheckpointAsync(
        string destinationBlobPath,
        Projection destination,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(destination.CheckpointFingerprint))
        {
            return; // No fingerprint, nothing to save
        }

        var checkpointBlobName = GetCheckpointPathFromBlobPath(destinationBlobPath, destination.CheckpointFingerprint);

        var containerClient = await GetContainerClientAsync();
        var blobClient = containerClient.GetBlobClient(checkpointBlobName);

        // Only save if it doesn't already exist (checkpoints are immutable)
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            var json = JsonSerializer.Serialize(destination.Checkpoint, CheckpointJsonContext.Default.Checkpoint);
            var bytes = Encoding.UTF8.GetBytes(json);

            await blobClient.UploadAsync(
                new BinaryData(bytes),
                overwrite: false,
                cancellationToken);
        }
    }

    /// <summary>
    /// Loads a checkpoint for a destination from external blob storage.
    /// The checkpoint is stored relative to the destination's blob path.
    /// </summary>
    /// <param name="destinationBlobPath">The destination's blob path.</param>
    /// <param name="checkpointFingerprint">The checkpoint fingerprint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checkpoint, or null if it doesn't exist.</returns>
    protected async Task<Checkpoint?> LoadDestinationCheckpointAsync(
        string destinationBlobPath,
        string checkpointFingerprint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(checkpointFingerprint))
        {
            return null;
        }

        var checkpointBlobName = GetCheckpointPathFromBlobPath(destinationBlobPath, checkpointFingerprint);
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
}
