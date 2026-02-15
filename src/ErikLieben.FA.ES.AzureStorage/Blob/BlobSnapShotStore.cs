using ErikLieben.FA.ES.Processors;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using ErikLieben.FA.ES.Observability;
using ErikLieben.FA.ES.Snapshots;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Provides an Azure Blob Storage-backed implementation of <see cref="ISnapShotStore"/> for persisting and retrieving aggregate snapshots.
/// </summary>
/// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
/// <param name="settings">The Blob settings controlling container creation and defaults.</param>
public partial class BlobSnapShotStore(
    IAzureClientFactory<BlobServiceClient> clientFactory,
    EventStreamBlobSettings settings)
    : ISnapShotStore
{
    /// <summary>
    /// Persists a snapshot of the aggregate to Blob Storage using the supplied JSON type info.
    /// </summary>
    /// <param name="object">The aggregate instance to snapshot.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type info describing the aggregate type.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot is taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    /// <exception cref="DocumentConfigurationException">Thrown when the snapshot blob client cannot be created.</exception>
    public async Task SetAsync(IBase @object, JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("BlobSnapShotStore.Set");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureBlob);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationWrite);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
        if (!string.IsNullOrWhiteSpace(name))
        {
            documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}_{name}.json";
        }
        var blob = await CreateBlobClient(document, documentPath);
        await blob.Save(@object, jsonTypeInfo);

        // Record snapshot metrics
        FaesMetrics.RecordSnapshotCreated(document.ObjectName ?? "unknown");
    }

    /// <summary>
    /// Retrieves a snapshot of the aggregate at the specified version using the supplied JSON type info.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="jsonTypeInfo">The source-generated JSON type info for <typeparamref name="T"/>.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot was taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The deserialized snapshot instance when found; otherwise null.</returns>
    /// <exception cref="DocumentConfigurationException">Thrown when the snapshot blob client cannot be created.</exception>
    public async Task<T?> GetAsync<T>(JsonTypeInfo<T> jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default) where T : class, IBase
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("BlobSnapShotStore.Get");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureBlob);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
        if (!string.IsNullOrWhiteSpace(name))
        {
            documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}_{name}.json";
        }
        var blob = await CreateBlobClient(document, documentPath);
        var (a,_) = await blob.AsEntityAsync(jsonTypeInfo);
        return a;
    }

    /// <summary>
    /// Retrieves a snapshot as an untyped object at the specified version using the supplied JSON type info.
    /// </summary>
    /// <param name="jsonTypeInfo">The source-generated JSON type info representing the runtime type of the snapshot.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot was taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The deserialized snapshot instance when found; otherwise null.</returns>
    /// <exception cref="DocumentConfigurationException">Thrown when the snapshot blob client cannot be created.</exception>
    public async Task<object?> GetAsync(JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("BlobSnapShotStore.Get");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureBlob);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
        if (!string.IsNullOrWhiteSpace(name))
        {
            documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}_{name}.json";
        }
        var blob = await CreateBlobClient(document, documentPath);
        var x = await blob.AsEntityAsync(jsonTypeInfo);
        return x;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SnapshotMetadata>> ListSnapshotsAsync(
        IObjectDocument document,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("BlobSnapShotStore.List");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureBlob);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationQuery);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
        }

        ArgumentNullException.ThrowIfNull(document.ObjectName);

        var container = await GetContainerClient(document);
        var prefix = $"snapshot/{document.Active.StreamIdentifier}-";
        var snapshots = new List<SnapshotMetadata>();

        // List all blobs with the snapshot prefix
        await foreach (var blobItem in container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            var metadata = ParseSnapshotBlobName(blobItem.Name, blobItem.Properties.ContentLength, blobItem.Properties.CreatedOn);
            if (metadata is not null)
            {
                snapshots.Add(metadata);
            }
        }

        activity?.SetTag("faes.snapshot.count", snapshots.Count);

        // Return sorted by version descending (most recent first)
        return snapshots.OrderByDescending(s => s.Version).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        IObjectDocument document,
        int version,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("BlobSnapShotStore.Delete");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureBlob);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationDelete);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
        if (!string.IsNullOrWhiteSpace(name))
        {
            documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}_{name}.json";
        }

        var blob = await CreateBlobClient(document, documentPath);
        var response = await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        activity?.SetTag(FaesSemanticConventions.Success, response.Value);

        return response.Value;
    }

    /// <inheritdoc />
    public async Task<int> DeleteManyAsync(
        IObjectDocument document,
        IEnumerable<int> versions,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("BlobSnapShotStore.DeleteMany");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureBlob);
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

    /// <summary>
    /// Parses snapshot metadata from a blob name.
    /// </summary>
    private static SnapshotMetadata? ParseSnapshotBlobName(string blobName, long? contentLength, DateTimeOffset? createdOn)
    {
        // Pattern: snapshot/{streamId}-{version:d20}.json or snapshot/{streamId}-{version:d20}_{name}.json
        var match = SnapshotBlobNameRegex().Match(blobName);
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups["version"].Value, out var version))
            return null;

        var name = match.Groups["name"].Success ? match.Groups["name"].Value : null;
        return new SnapshotMetadata(version, createdOn ?? DateTimeOffset.MinValue, name, contentLength);
    }

    [GeneratedRegex(@"snapshot/[^-]+-(?<version>\d+)(?:_(?<name>[^.]+))?\.json$")]
    private static partial Regex SnapshotBlobNameRegex();

    /// <summary>
    /// Gets the container client for the document.
    /// </summary>
    private async Task<BlobContainerClient> GetContainerClient(IObjectDocument objectDocument)
    {
        ArgumentNullException.ThrowIfNull(objectDocument.ObjectName);

#pragma warning disable CS0618
        var connectionName = !string.IsNullOrWhiteSpace(objectDocument.Active.SnapShotStore)
            ? objectDocument.Active.SnapShotStore
            : objectDocument.Active.SnapShotConnectionName;
#pragma warning restore CS0618
        var client = clientFactory.CreateClient(connectionName);
        var container = client.GetBlobContainerClient(objectDocument.ObjectName.ToLowerInvariant());

        if (settings.AutoCreateContainer)
        {
            await container.CreateIfNotExistsAsync();
        }

        return container;
    }

    /// <summary>
    /// Creates a <see cref="BlobClient"/> for the given document and snapshot path, ensuring the container exists when configured.
    /// </summary>
    /// <param name="objectDocument">The object document that provides the container scope and connection name.</param>
    /// <param name="documentPath">The blob path of the snapshot document.</param>
    /// <returns>A <see cref="BlobClient"/> configured for the snapshot path.</returns>
    /// <exception cref="DocumentConfigurationException">Thrown when the blob client cannot be created.</exception>
    private async Task<BlobClient> CreateBlobClient(IObjectDocument objectDocument, string documentPath)
    {
        ArgumentNullException.ThrowIfNull(objectDocument.ObjectName);

        // Use SnapShotStore, falling back to deprecated SnapShotConnectionName for backwards compatibility
#pragma warning disable CS0618 // Type or member is obsolete
        var connectionName = !string.IsNullOrWhiteSpace(objectDocument.Active.SnapShotStore)
            ? objectDocument.Active.SnapShotStore
            : objectDocument.Active.SnapShotConnectionName;
#pragma warning restore CS0618
        var client = clientFactory.CreateClient(connectionName);
        var container = client.GetBlobContainerClient(objectDocument.ObjectName.ToLowerInvariant());

        if (settings.AutoCreateContainer)
        {
            await container.CreateIfNotExistsAsync();
        }

        var blob = container.GetBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create blobClient.");
        return blob!;
    }
}
