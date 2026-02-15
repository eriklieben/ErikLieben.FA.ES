using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.HealthChecks;
using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests.HealthChecks;

public class CosmosDbHealthCheckTests
{
    public class Constructor
    {
        [Fact]
        public void Should_throw_when_cosmos_client_is_null()
        {
            // Arrange
            var settings = new EventStreamCosmosDbSettings { DatabaseName = "test" };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbHealthCheck(null!, settings));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            // Arrange
            var cosmosClient = Substitute.For<CosmosClient>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbHealthCheck(cosmosClient, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var cosmosClient = Substitute.For<CosmosClient>();
            var settings = new EventStreamCosmosDbSettings { DatabaseName = "test" };

            // Act
            var sut = new CosmosDbHealthCheck(cosmosClient, settings);

            // Assert
            Assert.NotNull(sut);
        }
    }

    // Note: CheckHealthAsync tests are limited because CosmosClient is not easily mockable
    // For full integration testing, use Testcontainers.CosmosDb which is already referenced
    // in this project. Integration tests would verify the full health check flow against
    // an actual CosmosDB emulator.
}
