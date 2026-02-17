using ErikLieben.FA.ES.S3.Configuration;
using Testcontainers.Minio;

namespace ErikLieben.FA.ES.S3.Tests.Integration;

/// <summary>
/// Shared fixture for MinIO container used across all S3 integration tests.
/// Starts a single MinIO container for the entire test collection.
/// </summary>
public class MinioContainerFixture : IAsyncLifetime
{
    private readonly MinioContainer _minioContainer;

    public MinioContainerFixture()
    {
        _minioContainer = new MinioBuilder("minio/minio:latest")
            .Build();
    }

    /// <summary>
    /// Gets the AWS access key configured for the MinIO container.
    /// </summary>
    public string AccessKey => _minioContainer.GetAccessKey();

    /// <summary>
    /// Gets the AWS secret key configured for the MinIO container.
    /// </summary>
    public string SecretKey => _minioContainer.GetSecretKey();

    /// <summary>
    /// Gets the service URL for the MinIO instance (e.g., http://localhost:9000).
    /// </summary>
    public string ServiceUrl => _minioContainer.GetConnectionString();

    /// <summary>
    /// Creates settings for the S3 integration tests targeting the MinIO container.
    /// </summary>
    /// <param name="bucketName">Optional bucket name. Defaults to "event-store".</param>
    /// <param name="autoCreateBucket">Whether to auto-create the bucket. Defaults to true.</param>
    public EventStreamS3Settings CreateSettings(
        string bucketName = "event-store",
        bool autoCreateBucket = true) =>
        new(
            defaultDataStore: "s3",
            bucketName: bucketName,
            serviceUrl: ServiceUrl,
            accessKey: AccessKey,
            secretKey: SecretKey,
            forcePathStyle: true,
            region: "us-east-1",
            autoCreateBucket: autoCreateBucket);

    public async Task InitializeAsync()
    {
        await _minioContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _minioContainer.DisposeAsync();
    }
}

/// <summary>
/// Collection definition for sharing the MinIO container across S3 integration test classes.
/// </summary>
[CollectionDefinition("MinIO")]
public class MinioCollection : ICollectionFixture<MinioContainerFixture>
{
}
