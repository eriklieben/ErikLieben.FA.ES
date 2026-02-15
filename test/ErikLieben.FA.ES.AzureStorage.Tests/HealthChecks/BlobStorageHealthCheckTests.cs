using Azure;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.HealthChecks;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.HealthChecks;

public class BlobStorageHealthCheckTests
{
    public class Constructor
    {
        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            // Arrange
            var settings = new EventStreamBlobSettings("Store");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobStorageHealthCheck(null!, settings));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobStorageHealthCheck(clientFactory, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamBlobSettings("Store");

            // Act
            var sut = new BlobStorageHealthCheck(clientFactory, settings);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_custom_client_name()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamBlobSettings("Store");

            // Act
            var sut = new BlobStorageHealthCheck(clientFactory, settings, "CustomClient");

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class CheckHealthAsync
    {
        [Fact]
        public async Task Should_return_unhealthy_when_blob_storage_throws_exception()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamBlobSettings("Store");
            var blobServiceClient = Substitute.For<BlobServiceClient>();

            blobServiceClient.GetAccountInfoAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(500, "Service unavailable"));

            clientFactory.CreateClient("Store").Returns(blobServiceClient);

            var sut = new BlobStorageHealthCheck(clientFactory, settings);
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("test", sut, null, null)
            };

            // Act
            var result = await sut.CheckHealthAsync(context);

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("Azure Blob Storage is not accessible", result.Description);
            Assert.NotNull(result.Exception);
        }

        [Fact]
        public async Task Should_return_unhealthy_when_authentication_fails()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamBlobSettings("Store");
            var blobServiceClient = Substitute.For<BlobServiceClient>();

            blobServiceClient.GetAccountInfoAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(401, "Unauthorized"));

            clientFactory.CreateClient("Store").Returns(blobServiceClient);

            var sut = new BlobStorageHealthCheck(clientFactory, settings);
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("test", sut, null, null)
            };

            // Act
            var result = await sut.CheckHealthAsync(context);

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("Unauthorized", result.Description);
        }

        [Fact]
        public async Task Should_support_cancellation()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamBlobSettings("Store");
            var blobServiceClient = Substitute.For<BlobServiceClient>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            blobServiceClient.GetAccountInfoAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            clientFactory.CreateClient("Store").Returns(blobServiceClient);

            var sut = new BlobStorageHealthCheck(clientFactory, settings);
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("test", sut, null, null)
            };

            // Act
            var result = await sut.CheckHealthAsync(context, cts.Token);

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
        }
    }
}
