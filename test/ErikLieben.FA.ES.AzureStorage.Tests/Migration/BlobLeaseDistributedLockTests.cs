using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ErikLieben.FA.ES.AzureStorage.Migration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Migration;

public class BlobLeaseDistributedLockTests
{
    private readonly BlobLeaseClient mockLeaseClient;
    private readonly ILogger<BlobLeaseDistributedLock> mockLogger;

    public BlobLeaseDistributedLockTests()
    {
        mockLeaseClient = Substitute.For<BlobLeaseClient>();
        mockLogger = Substitute.For<ILogger<BlobLeaseDistributedLock>>();
    }

    public class Constructor : BlobLeaseDistributedLockTests
    {
        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Act
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_leaseClient_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobLeaseDistributedLock(null!, "lease-123", "test-lock", mockLogger));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_leaseId_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobLeaseDistributedLock(mockLeaseClient, null!, "test-lock", mockLogger));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_logger_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobLeaseDistributedLock(mockLeaseClient, "lease-123", "test-lock", null!));
        }

        [Fact]
        public void Should_set_LockId_from_leaseId()
        {
            // Act
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            // Assert
            Assert.Equal("lease-123", sut.LockId);
        }

        [Fact]
        public void Should_set_LockKey_from_lockKey_parameter()
        {
            // Act
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            // Assert
            Assert.Equal("test-lock", sut.LockKey);
        }

        [Fact]
        public void Should_set_AcquiredAt_to_current_time()
        {
            // Arrange
            var before = DateTimeOffset.UtcNow;

            // Act
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            var after = DateTimeOffset.UtcNow;

            // Assert
            Assert.True(sut.AcquiredAt >= before && sut.AcquiredAt <= after);
        }

        [Fact]
        public void Should_set_ExpiresAt_to_60_seconds_after_AcquiredAt()
        {
            // Act
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            // Assert
            Assert.Equal(sut.AcquiredAt.AddSeconds(60), sut.ExpiresAt);
        }
    }

    public class IsValidProperty : BlobLeaseDistributedLockTests
    {
        [Fact]
        public void Should_return_true_when_not_disposed_and_not_expired()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            // Act & Assert
            Assert.True(sut.IsValid);
        }

        [Fact]
        public async Task Should_return_false_after_release()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            // Act
            await sut.ReleaseAsync();

            // Assert
            Assert.False(sut.IsValid);
        }
    }

    public class RenewAsyncMethod : BlobLeaseDistributedLockTests
    {
        [Fact]
        public async Task Should_return_true_when_renewal_succeeds()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            // Act
            var result = await sut.RenewAsync();

            // Assert
            Assert.True(result);
            await mockLeaseClient.Received(1).RenewAsync(cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_update_ExpiresAt_when_renewal_succeeds()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            var originalExpiry = sut.ExpiresAt;

            // Small delay to ensure time difference
            await Task.Delay(10);

            // Act
            await sut.RenewAsync();

            // Assert
            Assert.True(sut.ExpiresAt > originalExpiry);
        }

        [Fact]
        public async Task Should_return_false_after_release()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            await sut.ReleaseAsync();

            // Act
            var result = await sut.RenewAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_false_when_409_conflict_returned()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            mockLeaseClient.RenewAsync(cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(409, "Conflict"));

            // Act
            var result = await sut.RenewAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_false_when_404_not_found_returned()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            mockLeaseClient.RenewAsync(cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not Found"));

            // Act
            var result = await sut.RenewAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_throw_when_unexpected_exception_occurs()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            mockLeaseClient.RenewAsync(cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RenewAsync());
        }
    }

    public class IsValidAsyncMethod : BlobLeaseDistributedLockTests
    {
        [Fact]
        public async Task Should_return_true_when_lock_is_still_valid()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            // Act
            var result = await sut.IsValidAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Should_return_false_after_release()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            await sut.ReleaseAsync();

            // Act
            var result = await sut.IsValidAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_false_when_409_conflict_returned()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            mockLeaseClient.RenewAsync(cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(409, "Conflict"));

            // Act
            var result = await sut.IsValidAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_false_when_404_not_found_returned()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            mockLeaseClient.RenewAsync(cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not Found"));

            // Act
            var result = await sut.IsValidAsync();

            // Assert
            Assert.False(result);
        }
    }

    public class ReleaseAsyncMethod : BlobLeaseDistributedLockTests
    {
        [Fact]
        public async Task Should_call_leaseClient_ReleaseAsync()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            // Act
            await sut.ReleaseAsync();

            // Assert
            await mockLeaseClient.Received(1).ReleaseAsync(cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_call_release_again_after_disposed()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            await sut.ReleaseAsync();
            mockLeaseClient.ClearReceivedCalls();

            // Act
            await sut.ReleaseAsync();

            // Assert
            await mockLeaseClient.DidNotReceive().ReleaseAsync(cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_handle_409_conflict_gracefully()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            mockLeaseClient.ReleaseAsync(cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(409, "Conflict"));

            // Act & Assert (should not throw)
            await sut.ReleaseAsync();
        }

        [Fact]
        public async Task Should_handle_404_not_found_gracefully()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            mockLeaseClient.ReleaseAsync(cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not Found"));

            // Act & Assert (should not throw)
            await sut.ReleaseAsync();
        }

        [Fact]
        public async Task Should_throw_when_unexpected_exception_occurs()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            mockLeaseClient.ReleaseAsync(cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ReleaseAsync());
        }
    }

    public class DisposeAsyncMethod : BlobLeaseDistributedLockTests
    {
        [Fact]
        public async Task Should_call_ReleaseAsync_when_not_disposed()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            // Act
            await sut.DisposeAsync();

            // Assert
            await mockLeaseClient.Received(1).ReleaseAsync(cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_call_ReleaseAsync_when_already_disposed()
        {
            // Arrange
            var sut = new BlobLeaseDistributedLock(
                mockLeaseClient,
                "lease-123",
                "test-lock",
                mockLogger);

            await sut.ReleaseAsync();
            mockLeaseClient.ClearReceivedCalls();

            // Act
            await sut.DisposeAsync();

            // Assert
            await mockLeaseClient.DidNotReceive().ReleaseAsync(cancellationToken: Arg.Any<CancellationToken>());
        }
    }
}
