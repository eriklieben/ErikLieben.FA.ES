using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ErikLieben.FA.ES.AzureStorage.Migration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Migration;

public class BlobLeaseDistributedLockProviderTests
{
    private readonly BlobServiceClient mockBlobServiceClient;
    private readonly ILoggerFactory mockLoggerFactory;
    private readonly ILogger<BlobLeaseDistributedLockProvider> mockLogger;
    private readonly ILogger<BlobLeaseDistributedLock> mockLockLogger;

    public BlobLeaseDistributedLockProviderTests()
    {
        mockBlobServiceClient = Substitute.For<BlobServiceClient>();
        mockLoggerFactory = Substitute.For<ILoggerFactory>();
        mockLogger = Substitute.For<ILogger<BlobLeaseDistributedLockProvider>>();
        mockLockLogger = Substitute.For<ILogger<BlobLeaseDistributedLock>>();

        mockLoggerFactory.CreateLogger<BlobLeaseDistributedLockProvider>().Returns(mockLogger);
        mockLoggerFactory.CreateLogger<BlobLeaseDistributedLock>().Returns(mockLockLogger);
    }

    public class Constructor : BlobLeaseDistributedLockProviderTests
    {
        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Act
            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_blobServiceClient_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobLeaseDistributedLockProvider(null!, mockLoggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_loggerFactory_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobLeaseDistributedLockProvider(mockBlobServiceClient, null!));
        }

        [Fact]
        public void Should_use_default_container_name_when_not_specified()
        {
            // Act
            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Assert
            Assert.Equal("blob-lease", sut.ProviderName);
        }

        [Fact]
        public void Should_use_custom_container_name_when_specified()
        {
            // Act
            var sut = new BlobLeaseDistributedLockProvider(
                mockBlobServiceClient,
                mockLoggerFactory,
                "custom-locks");

            // Assert - provider should be created successfully
            Assert.NotNull(sut);
        }
    }

    public class ProviderNameProperty : BlobLeaseDistributedLockProviderTests
    {
        [Fact]
        public void Should_return_blob_lease()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Assert
            Assert.Equal("blob-lease", sut.ProviderName);
        }
    }

    public class AcquireLockAsyncMethod : BlobLeaseDistributedLockProviderTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_lockKey_is_null()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.AcquireLockAsync(null!, TimeSpan.FromSeconds(30)));
        }

        [Fact]
        public async Task Should_create_container_if_not_exists()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockLeaseClient = Substitute.For<BlobLeaseClient>();
            var mockResponse = Substitute.For<Response<BlobLease>>();
            var mockLease = BlobsModelFactory.BlobLease(default, DateTimeOffset.UtcNow, "lease-123");

            mockBlobServiceClient.GetBlobContainerClient("migration-locks").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.GetBlobLeaseClient(Arg.Any<string>()).Returns(mockLeaseClient);
            mockResponse.Value.Returns(mockLease);
            mockLeaseClient.AcquireAsync(Arg.Any<TimeSpan>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(mockResponse));

            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act
            await sut.AcquireLockAsync("test-lock", TimeSpan.FromSeconds(30));

            // Assert
            await mockContainerClient.Received(1).CreateIfNotExistsAsync(cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_upload_empty_blob_for_lock_placeholder()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockLeaseClient = Substitute.For<BlobLeaseClient>();
            var mockResponse = Substitute.For<Response<BlobLease>>();
            var mockLease = BlobsModelFactory.BlobLease(default, DateTimeOffset.UtcNow, "lease-123");

            mockBlobServiceClient.GetBlobContainerClient("migration-locks").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.GetBlobLeaseClient(Arg.Any<string>()).Returns(mockLeaseClient);
            mockResponse.Value.Returns(mockLease);
            mockLeaseClient.AcquireAsync(Arg.Any<TimeSpan>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(mockResponse));

            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act
            await sut.AcquireLockAsync("test-lock", TimeSpan.FromSeconds(30));

            // Assert
            await mockBlobClient.Received(1).UploadAsync(
                Arg.Any<BinaryData>(),
                overwrite: false,
                cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_handle_blob_already_exists_conflict()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockLeaseClient = Substitute.For<BlobLeaseClient>();
            var mockResponse = Substitute.For<Response<BlobLease>>();
            var mockLease = BlobsModelFactory.BlobLease(default, DateTimeOffset.UtcNow, "lease-123");

            mockBlobServiceClient.GetBlobContainerClient("migration-locks").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.GetBlobLeaseClient(Arg.Any<string>()).Returns(mockLeaseClient);

            // First upload throws 409 (blob exists), which should be handled
            mockBlobClient.UploadAsync(Arg.Any<BinaryData>(), overwrite: false, cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(409, "Blob already exists"));

            mockResponse.Value.Returns(mockLease);
            mockLeaseClient.AcquireAsync(Arg.Any<TimeSpan>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(mockResponse));

            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act
            var result = await sut.AcquireLockAsync("test-lock", TimeSpan.FromSeconds(30));

            // Assert - should still succeed
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Should_return_lock_when_lease_acquired()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockLeaseClient = Substitute.For<BlobLeaseClient>();
            var mockResponse = Substitute.For<Response<BlobLease>>();
            var mockLease = BlobsModelFactory.BlobLease(default, DateTimeOffset.UtcNow, "lease-123");

            mockBlobServiceClient.GetBlobContainerClient("migration-locks").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.GetBlobLeaseClient(Arg.Any<string>()).Returns(mockLeaseClient);
            mockResponse.Value.Returns(mockLease);
            mockLeaseClient.AcquireAsync(Arg.Any<TimeSpan>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(mockResponse));

            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act
            var result = await sut.AcquireLockAsync("test-lock", TimeSpan.FromSeconds(30));

            // Assert
            Assert.NotNull(result);
            Assert.Equal("lease-123", result.LockId);
            Assert.Equal("test-lock", result.LockKey);
        }

        [Fact]
        public async Task Should_return_null_when_timeout_exceeded()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockLeaseClient = Substitute.For<BlobLeaseClient>();

            mockBlobServiceClient.GetBlobContainerClient("migration-locks").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.GetBlobLeaseClient(Arg.Any<string>()).Returns(mockLeaseClient);

            // Always return 409 (lease held)
            mockLeaseClient.AcquireAsync(Arg.Any<TimeSpan>(), cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(409, "Lease already held"));

            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act - use very short timeout
            var result = await sut.AcquireLockAsync("test-lock", TimeSpan.FromMilliseconds(10));

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Should_throw_when_unexpected_error_occurs()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockLeaseClient = Substitute.For<BlobLeaseClient>();

            mockBlobServiceClient.GetBlobContainerClient("migration-locks").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.GetBlobLeaseClient(Arg.Any<string>()).Returns(mockLeaseClient);

            mockLeaseClient.AcquireAsync(Arg.Any<TimeSpan>(), cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.AcquireLockAsync("test-lock", TimeSpan.FromSeconds(30)));
        }

        [Theory]
        [InlineData("test/lock", "test-lock.lock")]
        [InlineData("test\\lock", "test-lock.lock")]
        [InlineData("test:lock", "test-lock.lock")]
        [InlineData("test?lock", "test-lock.lock")]
        [InlineData("test#lock", "test-lock.lock")]
        [InlineData("test[lock]", "test-lock-.lock")]
        [InlineData("test@lock", "test-lock.lock")]
        public async Task Should_sanitize_lock_key_for_blob_name(string lockKey, string expectedBlobName)
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockLeaseClient = Substitute.For<BlobLeaseClient>();
            var mockResponse = Substitute.For<Response<BlobLease>>();
            var mockLease = BlobsModelFactory.BlobLease(default, DateTimeOffset.UtcNow, "lease-123");

            mockBlobServiceClient.GetBlobContainerClient("migration-locks").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(expectedBlobName).Returns(mockBlobClient);
            mockBlobClient.GetBlobLeaseClient(Arg.Any<string>()).Returns(mockLeaseClient);
            mockResponse.Value.Returns(mockLease);
            mockLeaseClient.AcquireAsync(Arg.Any<TimeSpan>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(mockResponse));

            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act
            await sut.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30));

            // Assert
            mockContainerClient.Received(1).GetBlobClient(expectedBlobName);
        }
    }

    public class IsLockedAsyncMethod : BlobLeaseDistributedLockProviderTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_lockKey_is_null()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.IsLockedAsync(null!));
        }

        [Fact]
        public async Task Should_return_false_when_blob_does_not_exist()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();

            mockBlobServiceClient.GetBlobContainerClient("migration-locks").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(false);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));

            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act
            var result = await sut.IsLockedAsync("test-lock");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_true_when_blob_has_active_lease()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();
            var mockPropertiesResponse = Substitute.For<Response<BlobProperties>>();
            var mockProperties = BlobsModelFactory.BlobProperties(leaseState: LeaseState.Leased);

            mockBlobServiceClient.GetBlobContainerClient("migration-locks").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(true);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));
            mockPropertiesResponse.Value.Returns(mockProperties);
            mockBlobClient.GetPropertiesAsync(cancellationToken: Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(mockPropertiesResponse));

            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act
            var result = await sut.IsLockedAsync("test-lock");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Should_return_false_when_blob_has_no_lease()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();
            var mockPropertiesResponse = Substitute.For<Response<BlobProperties>>();
            var mockProperties = BlobsModelFactory.BlobProperties(leaseState: LeaseState.Available);

            mockBlobServiceClient.GetBlobContainerClient("migration-locks").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(true);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));
            mockPropertiesResponse.Value.Returns(mockProperties);
            mockBlobClient.GetPropertiesAsync(cancellationToken: Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(mockPropertiesResponse));

            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act
            var result = await sut.IsLockedAsync("test-lock");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_false_when_404_not_found()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();

            mockBlobServiceClient.GetBlobContainerClient("migration-locks").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(true);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));
            mockBlobClient.GetPropertiesAsync(cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not found"));

            var sut = new BlobLeaseDistributedLockProvider(mockBlobServiceClient, mockLoggerFactory);

            // Act
            var result = await sut.IsLockedAsync("test-lock");

            // Assert
            Assert.False(result);
        }
    }
}
