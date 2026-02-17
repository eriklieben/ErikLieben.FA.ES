using System.ComponentModel.DataAnnotations;

namespace ErikLieben.FA.ES.S3.Configuration;

/// <summary>
/// Represents configuration settings for S3-compatible storage-backed event streams and related stores.
/// </summary>
public record EventStreamS3Settings
{
    /// <summary>
    /// Gets the default data store key used for event streams (e.g., "s3").
    /// </summary>
    [Required]
    public string DefaultDataStore { get; init; }

    /// <summary>
    /// Gets the default document store key used for object documents.
    /// </summary>
    [Required]
    public string DefaultDocumentStore { get; init; }

    /// <summary>
    /// Gets the default snapshot store key used for snapshots.
    /// </summary>
    [Required]
    public string DefaultSnapShotStore { get; init; }

    /// <summary>
    /// Gets the default tag store key used for document and stream tags.
    /// </summary>
    [Required]
    public string DefaultDocumentTagStore { get; init; }

    /// <summary>
    /// Gets the bucket name used for storing event streams and documents.
    /// </summary>
    [Required]
    public string BucketName { get; init; }

    /// <summary>
    /// Gets the S3 service endpoint URL (e.g., "http://localhost:9000" for MinIO).
    /// </summary>
    public string? ServiceUrl { get; init; }

    /// <summary>
    /// Gets the access key for S3 authentication.
    /// </summary>
    public string? AccessKey { get; init; }

    /// <summary>
    /// Gets the secret key for S3 authentication.
    /// </summary>
    public string? SecretKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether to use path-style addressing (required for MinIO and most S3-compatible services).
    /// </summary>
    public bool ForcePathStyle { get; init; }

    /// <summary>
    /// Gets the AWS region for the S3 bucket. Defaults to "us-east-1".
    /// </summary>
    public string Region { get; init; }

    /// <summary>
    /// Gets a value indicating whether buckets are automatically created when missing.
    /// </summary>
    public bool AutoCreateBucket { get; init; }

    /// <summary>
    /// Gets a value indicating whether the S3 provider supports conditional writes (If-Match, If-None-Match).
    /// When true, enables S3 conditional writes for optimistic concurrency control.
    /// Supported by AWS S3 (since Nov 2024) and Cloudflare R2.
    /// When false, falls back to document hash-based concurrency (weaker guarantees for new stream creation).
    /// </summary>
    public bool SupportsConditionalWrites { get; init; }

    /// <summary>
    /// Gets a value indicating whether event stream chunking is enabled.
    /// </summary>
    public bool EnableStreamChunks { get; init; }

    /// <summary>
    /// Gets the default number of events per chunk when chunking is enabled.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Chunk size must be at least 1")]
    public int DefaultChunkSize { get; init; }

    /// <summary>
    /// Gets the default bucket prefix or container name used to store materialized object documents.
    /// </summary>
    [Required]
    public string DefaultDocumentContainerName { get; init; }

    /// <summary>
    /// Gets the maximum number of connections per server for the S3 client.
    /// </summary>
    public int? MaxConnectionsPerServer { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamS3Settings"/> record.
    /// </summary>
    public EventStreamS3Settings(
        string defaultDataStore,
        string bucketName = "event-store",
        string? defaultDocumentStore = null,
        string? defaultSnapShotStore = null,
        string? defaultDocumentTagStore = null,
        string? serviceUrl = null,
        string? accessKey = null,
        string? secretKey = null,
        bool forcePathStyle = true,
        string region = "us-east-1",
        bool autoCreateBucket = true,
        bool supportsConditionalWrites = false,
        bool enableStreamChunks = false,
        int defaultChunkSize = 1000,
        string defaultDocumentContainerName = "object-document-store",
        int? maxConnectionsPerServer = null)
    {
        ArgumentNullException.ThrowIfNull(defaultDataStore);

        DefaultDataStore = defaultDataStore;
        BucketName = bucketName;
        DefaultDocumentStore = defaultDocumentStore ?? DefaultDataStore;
        DefaultSnapShotStore = defaultSnapShotStore ?? DefaultDataStore;
        DefaultDocumentTagStore = defaultDocumentTagStore ?? DefaultDataStore;
        ServiceUrl = serviceUrl;
        AccessKey = accessKey;
        SecretKey = secretKey;
        ForcePathStyle = forcePathStyle;
        Region = region;
        AutoCreateBucket = autoCreateBucket;
        SupportsConditionalWrites = supportsConditionalWrites;
        EnableStreamChunks = enableStreamChunks;
        DefaultChunkSize = defaultChunkSize;
        DefaultDocumentContainerName = defaultDocumentContainerName;
        MaxConnectionsPerServer = maxConnectionsPerServer;
    }
}
