using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Observability;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.S3.Extensions;
using ErikLieben.FA.ES.S3.Model;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// Base factory class for creating and managing projections stored in S3-compatible storage.
/// </summary>
/// <typeparam name="T">The projection type that inherits from <see cref="Projection"/>.</typeparam>
public abstract class S3ProjectionFactory<T> : IProjectionFactory<T>, IProjectionFactory where T : Projection
{
    private readonly IS3ClientFactory _s3ClientFactory;
    private readonly string _clientName;
    private readonly string _bucketOrPath;
    private readonly bool _autoCreateBucket;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3ProjectionFactory{T}"/> class.
    /// </summary>
    /// <param name="s3ClientFactory">The factory for creating S3 client instances.</param>
    /// <param name="clientName">The name of the S3 client to use.</param>
    /// <param name="bucketOrPath">The bucket name or path where the projection is stored.</param>
    /// <param name="autoCreateBucket">A value indicating whether the target bucket is created automatically when missing. Defaults to true.</param>
    protected S3ProjectionFactory(
        IS3ClientFactory s3ClientFactory,
        string clientName,
        string bucketOrPath,
        bool autoCreateBucket = true)
    {
        ArgumentNullException.ThrowIfNull(s3ClientFactory);
        ArgumentNullException.ThrowIfNull(clientName);
        ArgumentNullException.ThrowIfNull(bucketOrPath);

        _s3ClientFactory = s3ClientFactory;
        _clientName = clientName;
        _bucketOrPath = bucketOrPath;
        _autoCreateBucket = autoCreateBucket;
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
    /// Gets the S3 client for the configured connection.
    /// </summary>
    /// <returns>The <see cref="IAmazonS3"/> client.</returns>
    protected IAmazonS3 GetS3Client()
    {
        return _s3ClientFactory.CreateClient(_clientName);
    }

    /// <summary>
    /// Gets the bucket name, ensuring the bucket exists when configured.
    /// </summary>
    /// <returns>The bucket name.</returns>
    protected async Task<string> GetBucketNameAsync()
    {
        if (_autoCreateBucket)
        {
            var s3Client = GetS3Client();
            await s3Client.EnsureBucketAsync(_bucketOrPath);
        }

        return _bucketOrPath;
    }

    /// <summary>
    /// Loads the projection from S3 storage, or creates a new instance if it doesn't exist.
    /// </summary>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <param name="blobName">Optional object name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded or newly created projection instance.</returns>
    public virtual async Task<T> GetOrCreateAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("S3ProjectionFactory.GetOrCreate");

        blobName ??= $"{typeof(T).Name}.json";

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.ProjectionType, typeof(T).Name);
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.DbName, _bucketOrPath);
        }

        var s3Client = GetS3Client();
        var bucketName = await GetBucketNameAsync();

        var json = await s3Client.GetObjectAsStringAsync(bucketName, blobName);
        if (json != null)
        {
            var projection = LoadFromJson(json, documentFactory, eventStreamFactory);
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

                if (activity?.IsAllDataRequested == true)
                {
                    activity.SetTag(FaesSemanticConventions.LoadedFromCache, true);
                }

                return projection;
            }
        }

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.LoadedFromCache, false);
        }

        return New();
    }

    /// <summary>
    /// Saves the projection to S3 storage.
    /// </summary>
    /// <param name="projection">The projection to save.</param>
    /// <param name="blobName">Optional object name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task SaveAsync(
        T projection,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("S3ProjectionFactory.Save");

        ArgumentNullException.ThrowIfNull(projection);

        blobName ??= $"{typeof(T).Name}.json";

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.ProjectionType, typeof(T).Name);
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.DbName, _bucketOrPath);
            activity.SetTag(FaesSemanticConventions.ProjectionStatus, projection.Status.ToString());
        }

        var s3Client = GetS3Client();
        var bucketName = await GetBucketNameAsync();

        var json = projection.ToJson();
        var bytes = Encoding.UTF8.GetBytes(json);

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = blobName,
            ContentType = "application/json",
            InputStream = new MemoryStream(bytes),
        };

        await s3Client.PutObjectAsync(request, cancellationToken);

        // If external checkpoint is enabled and fingerprint is set, save it separately
        // (fingerprint is only set after events have been processed)
        if (HasExternalCheckpoint && !string.IsNullOrEmpty(projection.CheckpointFingerprint))
        {
            await SaveCheckpointAsync(projection, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes the projection from S3 storage.
    /// </summary>
    /// <param name="blobName">Optional object name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task DeleteAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= $"{typeof(T).Name}.json";

        var s3Client = GetS3Client();
        var bucketName = await GetBucketNameAsync();

        try
        {
            await s3Client.DeleteObjectAsync(bucketName, blobName, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Object doesn't exist, nothing to delete
        }
    }

    /// <summary>
    /// Checks if the projection exists in S3 storage.
    /// </summary>
    /// <param name="blobName">Optional object name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the projection exists; otherwise, false.</returns>
    public virtual async Task<bool> ExistsAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= $"{typeof(T).Name}.json";

        var s3Client = GetS3Client();
        var bucketName = await GetBucketNameAsync();

        return await s3Client.ObjectExistsAsync(bucketName, blobName);
    }

    /// <summary>
    /// Gets the last modified timestamp of the projection object.
    /// </summary>
    /// <param name="blobName">Optional object name. If not provided, uses a default name based on the projection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The last modified timestamp, or null if the object doesn't exist.</returns>
    public virtual async Task<DateTimeOffset?> GetLastModifiedAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= $"{typeof(T).Name}.json";

        var s3Client = GetS3Client();
        var bucketName = await GetBucketNameAsync();

        try
        {
            var metadata = await s3Client.GetObjectMetadataAsync(bucketName, blobName, cancellationToken);
            return metadata.LastModified;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves a checkpoint to external S3 storage using the projection's CheckpointFingerprint.
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

        var checkpointKey = $"checkpoints/{typeof(T).Name}/{projection.CheckpointFingerprint}.json";

        var s3Client = GetS3Client();
        var bucketName = await GetBucketNameAsync();

        // Only save if it doesn't already exist (checkpoints are immutable)
        if (!await s3Client.ObjectExistsAsync(bucketName, checkpointKey))
        {
            var json = JsonSerializer.Serialize(projection.Checkpoint, CheckpointJsonContext.Default.Checkpoint);
            var bytes = Encoding.UTF8.GetBytes(json);

            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = checkpointKey,
                ContentType = "application/json",
                InputStream = new MemoryStream(bytes),
            };

            await s3Client.PutObjectAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Loads a checkpoint from external S3 storage using the projection's CheckpointFingerprint.
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

        var checkpointKey = $"checkpoints/{typeof(T).Name}/{checkpointFingerprint}.json";
        var s3Client = GetS3Client();
        var bucketName = await GetBucketNameAsync();

        try
        {
            using var response = await s3Client.GetObjectAsync(bucketName, checkpointKey, cancellationToken);
            return await JsonSerializer.DeserializeAsync(response.ResponseStream, CheckpointJsonContext.Default.Checkpoint, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            return null;
        }
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

        var s3Client = GetS3Client();
        var bucketName = await GetBucketNameAsync();

        if (!await s3Client.ObjectExistsAsync(bucketName, blobName))
        {
            // Create a minimal projection with the status set
            var statusJson = JsonSerializer.Serialize(new StatusOnly { Status = status }, StatusOnlyJsonContext.Default.StatusOnly);
            var statusBytes = Encoding.UTF8.GetBytes(statusJson);

            var createRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = blobName,
                ContentType = "application/json",
                InputStream = new MemoryStream(statusBytes),
            };

            await s3Client.PutObjectAsync(createRequest, cancellationToken);
            return;
        }

        // Read existing projection, update status, and save back
        var json = await s3Client.GetObjectAsStringAsync(bucketName, blobName);
        if (json == null)
        {
            return;
        }

        // Parse and update status using JsonDocument
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Build a new JSON object with the updated status
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "$status")
                {
                    writer.WriteNumber("$status", (int)status);
                }
                else
                {
                    property.WriteTo(writer);
                }
            }
            // If $status wasn't in the original, add it
            if (!root.TryGetProperty("$status", out _))
            {
                writer.WriteNumber("$status", (int)status);
            }
            writer.WriteEndObject();
        }

        var updatedJson = Encoding.UTF8.GetString(stream.ToArray());
        var bytes = Encoding.UTF8.GetBytes(updatedJson);

        var updateRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = blobName,
            ContentType = "application/json",
            InputStream = new MemoryStream(bytes),
        };

        await s3Client.PutObjectAsync(updateRequest, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<ProjectionStatus> GetStatusAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        blobName ??= $"{typeof(T).Name}.json";

        var s3Client = GetS3Client();
        var bucketName = await GetBucketNameAsync();

        var json = await s3Client.GetObjectAsStringAsync(bucketName, blobName);
        if (json == null)
        {
            return ProjectionStatus.Active;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("$status", out var statusElement))
        {
            if (statusElement.TryGetInt32(out var statusValue))
            {
                return (ProjectionStatus)statusValue;
            }
        }

        return ProjectionStatus.Active;
    }
}

/// <summary>
/// Helper type for serializing just the status property.
/// </summary>
internal record StatusOnly
{
    [JsonPropertyName("$status")]
    public ProjectionStatus Status { get; init; }
}

/// <summary>
/// JSON serializer context for status-only serialization.
/// </summary>
[JsonSerializable(typeof(StatusOnly))]
internal partial class StatusOnlyJsonContext : JsonSerializerContext
{
}
