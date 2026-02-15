using ErikLieben.FA.ES.Projections;
using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbProjectionStatusCoordinatorTests
{
    private readonly CosmosClient cosmosClient;
    private readonly Database database;
    private readonly Container container;

    public CosmosDbProjectionStatusCoordinatorTests()
    {
        cosmosClient = Substitute.For<CosmosClient>();
        database = Substitute.For<Database>();
        container = Substitute.For<Container>();

        cosmosClient.GetDatabase("test-db").Returns(database);
        database.GetContainer("projection-status").Returns(container);
    }

    public class Constructor : CosmosDbProjectionStatusCoordinatorTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_cosmos_client_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbProjectionStatusCoordinator(null!, "test-db"));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_database_name_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbProjectionStatusCoordinator(cosmosClient, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_custom_container_name()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", "custom-container");
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_null_logger()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", null, null);
            Assert.NotNull(sut);
        }
    }

    public class StartRebuildAsync : CosmosDbProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_projection_name_is_null()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartRebuildAsync(null!, "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_id_is_null()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartRebuildAsync("TestProjection", null!, RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5)));
        }
    }
}
