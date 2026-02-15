using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Observability;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.Extensions;
using ErikLieben.FA.ES.Snapshots;
using System.Net;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// Provides an S3-compatible storage-backed implementation of <see cref="ISnapShotStore"/> for persisting and retrieving aggregate snapshots.
/// </summary>
/// <param name="clientFactory">The S3 client factory used to create <see cref="IAmazonS3"/> instances.</param>
/// <param name="settings">The S3 settings controlling bucket creation and defaults.</param>
public partial class S3SnapShotStore(
    IS3ClientFactory clientFactory,
    EventStreamS3Settings settings)
    : ISnapShotStore
{
    /// <summary>
    /// Persists a snapshot of the aggregate to S3 storage using the supplied JSON type info.
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
        using var activity = FaesInstrumentation.Storage.StartActivity("S3SnapShotStore.Set");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationWrite);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var key = BuildSnapshotKey(document, version, name);
        var (s3Client, bucketName) = await GetClientAndBucket(document);
        await s3Client.PutObjectAsync(bucketName, key, @object, jsonTypeInfo);

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
    public async Task<T?> GetAsync<T>(JsonTypeInfo<T> jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default) where T : class, IBase
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("S3SnapShotStore.Get");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var key = BuildSnapshotKey(document, version, name);
        var (s3Client, bucketName) = await GetClientAndBucket(document);
        var (entity, _, _) = await s3Client.GetObjectAsEntityAsync(bucketName, key, jsonTypeInfo);
        return entity;
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
    public async Task<object?> GetAsync(JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("S3SnapShotStore.Get");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var key = BuildSnapshotKey(document, version, name);
        var (s3Client, bucketName) = await GetClientAndBucket(document);
        return await s3Client.GetObjectAsEntityAsync(bucketName, key, jsonTypeInfo);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SnapshotMetadata>> ListSnapshotsAsync(
        IObjectDocument document,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("S3SnapShotStore.List");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationQuery);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
        }

        ArgumentNullException.ThrowIfNull(document.ObjectName);

        var (s3Client, bucketName) = await GetClientAndBucket(document);
        var prefix = $"snapshot/{document.Active.StreamIdentifier}-";
        var snapshots = new List<SnapshotMetadata>();

        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix,
        };

        ListObjectsV2Response response;
        do
        {
            response = await s3Client.ListObjectsV2Async(request, cancellationToken);
            foreach (var s3Object in response.S3Objects)
            {
                var metadata = ParseSnapshotObjectKey(s3Object.Key, s3Object.Size ?? 0, s3Object.LastModified ?? DateTime.MinValue);
                if (metadata is not null)
                {
                    snapshots.Add(metadata);
                }
            }

            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated == true);

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
        using var activity = FaesInstrumentation.Storage.StartActivity("S3SnapShotStore.Delete");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationDelete);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.SnapshotVersion, version);
            activity.SetTag(FaesSemanticConventions.SnapshotName, name);
        }

        var key = BuildSnapshotKey(document, version, name);
        var (s3Client, bucketName) = await GetClientAndBucket(document);

        // Check if the object exists before deleting
        var exists = await s3Client.ObjectExistsAsync(bucketName, key);
        if (!exists)
        {
            activity?.SetTag(FaesSemanticConventions.Success, false);
            return false;
        }

        await s3Client.DeleteObjectAsync(bucketName, key, cancellationToken);

        activity?.SetTag(FaesSemanticConventions.Success, true);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> DeleteManyAsync(
        IObjectDocument document,
        IEnumerable<int> versions,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("S3SnapShotStore.DeleteMany");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
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
    /// Builds the S3 object key for a snapshot document.
    /// </summary>
    private static string BuildSnapshotKey(IObjectDocument document, int version, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return $"snapshot/{document.Active.StreamIdentifier}-{version:d20}_{name}.json";
        }

        return $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
    }

    /// <summary>
    /// Parses snapshot metadata from an S3 object key.
    /// </summary>
    private static SnapshotMetadata? ParseSnapshotObjectKey(string key, long contentLength, DateTime lastModified)
    {
        // Pattern: snapshot/{streamId}-{version:d20}.json or snapshot/{streamId}-{version:d20}_{name}.json
        var match = SnapshotObjectKeyRegex().Match(key);
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups["version"].Value, out var version))
            return null;

        var name = match.Groups["name"].Success ? match.Groups["name"].Value : null;
        return new SnapshotMetadata(version, lastModified, name, contentLength);
    }

    [GeneratedRegex(@"snapshot/[^-]+-(?<version>\d+)(?:_(?<name>[^.]+))?\.json$")]
    private static partial Regex SnapshotObjectKeyRegex();

    /// <summary>
    /// Gets the S3 client and bucket name for the given document, ensuring the bucket exists when configured.
    /// </summary>
    /// <param name="objectDocument">The object document that provides the bucket scope and client name.</param>
    /// <returns>A tuple containing the <see cref="IAmazonS3"/> client and the bucket name.</returns>
    private async Task<(IAmazonS3 Client, string BucketName)> GetClientAndBucket(IObjectDocument objectDocument)
    {
        ArgumentNullException.ThrowIfNull(objectDocument.ObjectName);

        var clientName = !string.IsNullOrWhiteSpace(objectDocument.Active.SnapShotStore)
            ? objectDocument.Active.SnapShotStore
            : settings.DefaultSnapShotStore;

        var s3Client = clientFactory.CreateClient(clientName);
        var bucketName = objectDocument.ObjectName.ToLowerInvariant();

        if (settings.AutoCreateBucket)
        {
            await s3Client.EnsureBucketAsync(bucketName);
        }

        return (s3Client, bucketName);
    }
}
