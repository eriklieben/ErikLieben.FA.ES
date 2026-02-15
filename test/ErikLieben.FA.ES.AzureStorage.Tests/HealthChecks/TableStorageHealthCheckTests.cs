using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.HealthChecks;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.HealthChecks;

public class TableStorageHealthCheckTests
{
    public class Constructor
    {
        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            // Arrange
            var settings = new EventStreamTableSettings("Store");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TableStorageHealthCheck(null!, settings));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TableStorageHealthCheck(clientFactory, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var settings = new EventStreamTableSettings("Store");

            // Act
            var sut = new TableStorageHealthCheck(clientFactory, settings);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_custom_client_name()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var settings = new EventStreamTableSettings("Store");

            // Act
            var sut = new TableStorageHealthCheck(clientFactory, settings, "CustomClient");

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class CheckHealthAsync
    {
        [Fact]
        public async Task Should_return_unhealthy_when_table_storage_throws_exception()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var settings = new EventStreamTableSettings("Store");
            var tableServiceClient = Substitute.For<TableServiceClient>();

            tableServiceClient.GetPropertiesAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(500, "Service unavailable"));

            clientFactory.CreateClient("Store").Returns(tableServiceClient);

            var sut = new TableStorageHealthCheck(clientFactory, settings);
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("test", sut, null, null)
            };

            // Act
            var result = await sut.CheckHealthAsync(context);

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("Azure Table Storage is not accessible", result.Description);
            Assert.NotNull(result.Exception);
        }

        [Fact]
        public async Task Should_return_unhealthy_when_authentication_fails()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var settings = new EventStreamTableSettings("Store");
            var tableServiceClient = Substitute.For<TableServiceClient>();

            tableServiceClient.GetPropertiesAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(403, "Forbidden"));

            clientFactory.CreateClient("Store").Returns(tableServiceClient);

            var sut = new TableStorageHealthCheck(clientFactory, settings);
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("test", sut, null, null)
            };

            // Act
            var result = await sut.CheckHealthAsync(context);

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("Forbidden", result.Description);
        }

        [Fact]
        public async Task Should_support_cancellation()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var settings = new EventStreamTableSettings("Store");
            var tableServiceClient = Substitute.For<TableServiceClient>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            tableServiceClient.GetPropertiesAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            clientFactory.CreateClient("Store").Returns(tableServiceClient);

            var sut = new TableStorageHealthCheck(clientFactory, settings);
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
