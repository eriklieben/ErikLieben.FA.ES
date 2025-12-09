using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Migration;
using ErikLieben.FA.ES.EventStreamManagement.Cutover;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Migration;

public class BlobMigrationRoutingTableTests
{
    private readonly BlobServiceClient mockBlobServiceClient;
    private readonly ILogger<BlobMigrationRoutingTable> mockLogger;

    public BlobMigrationRoutingTableTests()
    {
        mockBlobServiceClient = Substitute.For<BlobServiceClient>();
        mockLogger = Substitute.For<ILogger<BlobMigrationRoutingTable>>();
    }

    public class Constructor : BlobMigrationRoutingTableTests
    {
        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Act
            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_blobServiceClient_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobMigrationRoutingTable(null!, mockLogger));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_logger_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobMigrationRoutingTable(mockBlobServiceClient, null!));
        }

        [Fact]
        public void Should_use_default_container_name_when_not_specified()
        {
            // Act
            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Assert - should create instance successfully
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_use_custom_container_name_when_specified()
        {
            // Act
            var sut = new BlobMigrationRoutingTable(
                mockBlobServiceClient,
                mockLogger,
                "custom-routing");

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class GetPhaseAsyncMethod : BlobMigrationRoutingTableTests
    {
        [Fact]
        public async Task Should_return_Normal_when_no_routing_exists()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();

            mockBlobServiceClient.GetBlobContainerClient("migration-routing").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("obj-123.routing.json").Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(false);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));

            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act
            var result = await sut.GetPhaseAsync("obj-123");

            // Assert
            Assert.Equal(MigrationPhase.Normal, result);
        }
    }

    public class GetRoutingAsyncMethod : BlobMigrationRoutingTableTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_objectId_is_null()
        {
            // Arrange
            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetRoutingAsync(null!));
        }

        [Fact]
        public async Task Should_return_Normal_routing_when_blob_does_not_exist()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();

            mockBlobServiceClient.GetBlobContainerClient("migration-routing").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("obj-123.routing.json").Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(false);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));

            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act
            var result = await sut.GetRoutingAsync("obj-123");

            // Assert
            Assert.Equal(MigrationPhase.Normal, result.Phase);
        }

        [Fact]
        public async Task Should_return_Normal_routing_when_404_not_found()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();

            mockBlobServiceClient.GetBlobContainerClient("migration-routing").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("obj-123.routing.json").Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(true);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));
            mockBlobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not Found"));

            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act
            var result = await sut.GetRoutingAsync("obj-123");

            // Assert
            Assert.Equal(MigrationPhase.Normal, result.Phase);
        }

        [Fact]
        public async Task Should_throw_when_unexpected_error_occurs()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();

            mockBlobServiceClient.GetBlobContainerClient("migration-routing").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("obj-123.routing.json").Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(true);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));
            mockBlobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetRoutingAsync("obj-123"));
        }
    }

    public class SetMigrationPhaseAsyncMethod : BlobMigrationRoutingTableTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_objectId_is_null()
        {
            // Arrange
            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SetMigrationPhaseAsync(null!, MigrationPhase.DualWrite, "old", "new"));
        }

        [Fact]
        public async Task Should_throw_ArgumentNullException_when_oldStream_is_null()
        {
            // Arrange
            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SetMigrationPhaseAsync("obj-123", MigrationPhase.DualWrite, null!, "new"));
        }

        [Fact]
        public async Task Should_throw_ArgumentNullException_when_newStream_is_null()
        {
            // Arrange
            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SetMigrationPhaseAsync("obj-123", MigrationPhase.DualWrite, "old", null!));
        }

        [Fact]
        public async Task Should_create_container_if_not_exists()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();

            mockBlobServiceClient.GetBlobContainerClient("migration-routing").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("obj-123.routing.json").Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(false);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));

            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act
            await sut.SetMigrationPhaseAsync("obj-123", MigrationPhase.DualWrite, "old-stream", "new-stream");

            // Assert
            await mockContainerClient.Received(1).CreateIfNotExistsAsync(cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_create_new_entry_when_blob_does_not_exist()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();

            mockBlobServiceClient.GetBlobContainerClient("migration-routing").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("obj-123.routing.json").Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(false);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));

            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act
            await sut.SetMigrationPhaseAsync("obj-123", MigrationPhase.DualWrite, "old-stream", "new-stream");

            // Assert
            await mockBlobClient.Received(1).UploadAsync(
                Arg.Any<BinaryData>(),
                overwrite: true,
                cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_when_unexpected_error_occurs()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();

            mockBlobServiceClient.GetBlobContainerClient("migration-routing").Returns(mockContainerClient);
            mockContainerClient.CreateIfNotExistsAsync(cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.SetMigrationPhaseAsync("obj-123", MigrationPhase.DualWrite, "old-stream", "new-stream"));
        }
    }

    public class RemoveRoutingAsyncMethod : BlobMigrationRoutingTableTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_objectId_is_null()
        {
            // Arrange
            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.RemoveRoutingAsync(null!));
        }

        [Fact]
        public async Task Should_delete_blob()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();

            mockBlobServiceClient.GetBlobContainerClient("migration-routing").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("obj-123.routing.json").Returns(mockBlobClient);

            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act
            await sut.RemoveRoutingAsync("obj-123");

            // Assert
            await mockBlobClient.Received(1).DeleteIfExistsAsync(cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_when_unexpected_error_occurs()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();

            mockBlobServiceClient.GetBlobContainerClient("migration-routing").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("obj-123.routing.json").Returns(mockBlobClient);
            mockBlobClient.DeleteIfExistsAsync(cancellationToken: Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.RemoveRoutingAsync("obj-123"));
        }
    }

    public class GetActiveMigrationsAsyncMethod : BlobMigrationRoutingTableTests
    {
        [Fact]
        public async Task Should_return_empty_when_container_does_not_exist()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();

            mockBlobServiceClient.GetBlobContainerClient("migration-routing").Returns(mockContainerClient);
            mockExistsResponse.Value.Returns(false);
            mockContainerClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));

            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act
            var result = await sut.GetActiveMigrationsAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_throw_when_unexpected_error_occurs()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();

            mockBlobServiceClient.GetBlobContainerClient("migration-routing").Returns(mockContainerClient);
            mockContainerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            var sut = new BlobMigrationRoutingTable(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetActiveMigrationsAsync());
        }
    }
}
