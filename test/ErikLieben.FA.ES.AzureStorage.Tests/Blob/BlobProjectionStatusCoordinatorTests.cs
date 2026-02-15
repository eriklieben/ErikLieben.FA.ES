#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Projections;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

public class BlobProjectionStatusCoordinatorTests
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly BlobClient _blobClient;

    public BlobProjectionStatusCoordinatorTests()
    {
        _blobServiceClient = Substitute.For<BlobServiceClient>();
        _containerClient = Substitute.For<BlobContainerClient>();
        _blobClient = Substitute.For<BlobClient>();

        _blobServiceClient.GetBlobContainerClient(Arg.Any<string>()).Returns(_containerClient);
        _containerClient.GetBlobClient(Arg.Any<string>()).Returns(_blobClient);
    }

    private BlobProjectionStatusCoordinator CreateSut(string containerName = "projection-status")
    {
        return new BlobProjectionStatusCoordinator(_blobServiceClient, containerName);
    }

    public class Constructor : BlobProjectionStatusCoordinatorTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_blob_service_client_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobProjectionStatusCoordinator(null!));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_container_name_is_null()
        {
            // Arrange
            var blobServiceClient = Substitute.For<BlobServiceClient>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobProjectionStatusCoordinator(blobServiceClient, null!));
        }

        [Fact]
        public void Should_create_instance_with_default_container_name()
        {
            // Arrange
            var blobServiceClient = Substitute.For<BlobServiceClient>();
            var containerClient = Substitute.For<BlobContainerClient>();
            blobServiceClient.GetBlobContainerClient("projection-status").Returns(containerClient);

            // Act
            var sut = new BlobProjectionStatusCoordinator(blobServiceClient);

            // Assert
            Assert.NotNull(sut);
            blobServiceClient.Received(1).GetBlobContainerClient("projection-status");
        }

        [Fact]
        public void Should_create_instance_with_custom_container_name()
        {
            // Arrange
            var blobServiceClient = Substitute.For<BlobServiceClient>();
            var containerClient = Substitute.For<BlobContainerClient>();
            blobServiceClient.GetBlobContainerClient("custom-container").Returns(containerClient);

            // Act
            var sut = new BlobProjectionStatusCoordinator(blobServiceClient, "custom-container");

            // Assert
            Assert.NotNull(sut);
            blobServiceClient.Received(1).GetBlobContainerClient("custom-container");
        }

        [Fact]
        public void Should_create_instance_with_null_logger()
        {
            // Arrange
            var blobServiceClient = Substitute.For<BlobServiceClient>();
            var containerClient = Substitute.For<BlobContainerClient>();
            blobServiceClient.GetBlobContainerClient(Arg.Any<string>()).Returns(containerClient);

            // Act
            var sut = new BlobProjectionStatusCoordinator(blobServiceClient, "projection-status", null);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class StartRebuildAsync : BlobProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_projection_name_is_null()
        {
            // Arrange
            var sut = CreateSut();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartRebuildAsync(null!, "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30)));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_id_is_null()
        {
            // Arrange
            var sut = CreateSut();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartRebuildAsync("MyProjection", null!, RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30)));
        }

        [Fact]
        public async Task Should_return_rebuild_token_on_success()
        {
            // Arrange
            var sut = CreateSut();

            var contentInfo = BlobsModelFactory.BlobContentInfo(
                new ETag("etag"),
                DateTimeOffset.UtcNow,
                [],
                "1.0",
                null,
                null,
                0);
            var uploadResponse = Substitute.For<Response<BlobContentInfo>>();
            uploadResponse.Value.Returns(contentInfo);

            _blobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>(), Arg.Any<CancellationToken>())
                .Returns(uploadResponse);

            // Act
            var token = await sut.StartRebuildAsync(
                "MyProjection", "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30));

            // Assert
            Assert.NotNull(token);
            Assert.Equal("MyProjection", token.ProjectionName);
            Assert.Equal("object-1", token.ObjectId);
            Assert.Equal(RebuildStrategy.BlockingWithCatchUp, token.Strategy);
            Assert.False(token.IsExpired);
        }

        [Fact]
        public async Task Should_upload_status_document_to_blob()
        {
            // Arrange
            var sut = CreateSut();

            var contentInfo = BlobsModelFactory.BlobContentInfo(
                new ETag("etag"),
                DateTimeOffset.UtcNow,
                [],
                "1.0",
                null,
                null,
                0);
            var uploadResponse = Substitute.For<Response<BlobContentInfo>>();
            uploadResponse.Value.Returns(contentInfo);

            _blobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>(), Arg.Any<CancellationToken>())
                .Returns(uploadResponse);

            // Act
            await sut.StartRebuildAsync(
                "MyProjection", "object-1", RebuildStrategy.BlueGreen, TimeSpan.FromMinutes(30));

            // Assert
            await _blobClient.Received(1).UploadAsync(
                Arg.Any<Stream>(),
                Arg.Any<BlobUploadOptions>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class GetStatusAsync : BlobProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_return_null_when_blob_not_found()
        {
            // Arrange
            var sut = CreateSut();

            _blobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not found"));

            // Act
            var result = await sut.GetStatusAsync("MyProjection", "object-1");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Should_propagate_non_404_request_failed_exception()
        {
            // Arrange
            var sut = CreateSut();

            _blobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(500, "Internal Server Error"));

            // Act & Assert
            await Assert.ThrowsAsync<RequestFailedException>(() =>
                sut.GetStatusAsync("MyProjection", "object-1"));
        }
    }

    public class StartCatchUpAsync : BlobProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            // Arrange
            var sut = CreateSut();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartCatchUpAsync(null!));
        }
    }

    public class MarkReadyAsync : BlobProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            // Arrange
            var sut = CreateSut();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.MarkReadyAsync(null!));
        }
    }

    public class CompleteRebuildAsync : BlobProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            // Arrange
            var sut = CreateSut();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.CompleteRebuildAsync(null!));
        }
    }

    public class CancelRebuildAsync : BlobProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            // Arrange
            var sut = CreateSut();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.CancelRebuildAsync(null!));
        }
    }
}
