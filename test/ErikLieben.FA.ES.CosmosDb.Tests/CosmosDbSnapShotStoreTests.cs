using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Model;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbSnapShotStoreTests
{
    private readonly CosmosClient cosmosClient;
    private readonly EventStreamCosmosDbSettings settings;
    private readonly Database database;
    private readonly Container container;
    private readonly IObjectDocument objectDocument;
    private readonly StreamInformation streamInformation;

    public CosmosDbSnapShotStoreTests()
    {
        cosmosClient = Substitute.For<CosmosClient>();
        database = Substitute.For<Database>();
        container = Substitute.For<Container>();
        objectDocument = Substitute.For<IObjectDocument>();
        streamInformation = new StreamInformation
        {
            StreamIdentifier = "test-stream-0000000000"
        };

        settings = new EventStreamCosmosDbSettings
        {
            DatabaseName = "test-db",
            SnapshotsContainerName = "snapshots",
            AutoCreateContainers = false
        };

        objectDocument.Active.Returns(streamInformation);

        cosmosClient.GetDatabase(settings.DatabaseName).Returns(database);
        database.GetContainer(settings.SnapshotsContainerName).Returns(container);
    }

    public class Constructor : CosmosDbSnapShotStoreTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_cosmos_client_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new CosmosDbSnapShotStore(null!, settings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new CosmosDbSnapShotStore(cosmosClient, null!));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            var sut = new CosmosDbSnapShotStore(cosmosClient, settings);
            Assert.NotNull(sut);
        }
    }

    public class SetAsync : CosmosDbSnapShotStoreTests
    {
        [Fact]
        public async Task Should_upsert_snapshot_to_cosmos_db()
        {
            var sut = new CosmosDbSnapShotStore(cosmosClient, settings);
            var testEntity = new TestEntity { Name = "Test", Value = 42 };

            var itemResponse = Substitute.For<ItemResponse<CosmosDbSnapshotEntity>>();
            container.UpsertItemAsync(
                Arg.Any<CosmosDbSnapshotEntity>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            await sut.SetAsync(testEntity, TestJsonContext.Default.TestEntity, objectDocument, 5);

            await container.Received(1).UpsertItemAsync(
                Arg.Is<CosmosDbSnapshotEntity>(e =>
                    e.StreamId == streamInformation.StreamIdentifier &&
                    e.Version == 5),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_include_name_in_snapshot_id_when_provided()
        {
            var sut = new CosmosDbSnapShotStore(cosmosClient, settings);
            var testEntity = new TestEntity { Name = "Test", Value = 42 };

            var itemResponse = Substitute.For<ItemResponse<CosmosDbSnapshotEntity>>();
            container.UpsertItemAsync(
                Arg.Any<CosmosDbSnapshotEntity>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            await sut.SetAsync(testEntity, TestJsonContext.Default.TestEntity, objectDocument, 5, "v2");

            await container.Received(1).UpsertItemAsync(
                Arg.Is<CosmosDbSnapshotEntity>(e => e.Name == "v2"),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class GetAsyncGeneric : CosmosDbSnapShotStoreTests
    {
        [Fact]
        public async Task Should_return_null_when_snapshot_not_found()
        {
            var sut = new CosmosDbSnapShotStore(cosmosClient, settings);

            container.ReadItemAsync<CosmosDbSnapshotEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, objectDocument, 5);

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_deserialized_snapshot_when_found()
        {
            var sut = new CosmosDbSnapShotStore(cosmosClient, settings);

            var testEntity = new TestEntity { Name = "Test", Value = 42 };
            var json = JsonSerializer.Serialize(testEntity, TestJsonContext.Default.TestEntity);

            var snapshotEntity = new CosmosDbSnapshotEntity
            {
                Id = "test",
                StreamId = streamInformation.StreamIdentifier,
                Version = 5,
                Data = json
            };

            var itemResponse = Substitute.For<ItemResponse<CosmosDbSnapshotEntity>>();
            itemResponse.Resource.Returns(snapshotEntity);

            container.ReadItemAsync<CosmosDbSnapshotEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, objectDocument, 5);

            Assert.NotNull(result);
            Assert.Equal("Test", result.Name);
            Assert.Equal(42, result.Value);
        }
    }

    public class GetAsyncNonGeneric : CosmosDbSnapShotStoreTests
    {
        [Fact]
        public async Task Should_return_null_when_snapshot_not_found()
        {
            var sut = new CosmosDbSnapShotStore(cosmosClient, settings);

            container.ReadItemAsync<CosmosDbSnapshotEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            var result = await sut.GetAsync((JsonTypeInfo)TestJsonContext.Default.TestEntity, objectDocument, 5);

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_deserialized_snapshot_when_found()
        {
            var sut = new CosmosDbSnapShotStore(cosmosClient, settings);

            var testEntity = new TestEntity { Name = "Test", Value = 42 };
            var json = JsonSerializer.Serialize(testEntity, TestJsonContext.Default.TestEntity);

            var snapshotEntity = new CosmosDbSnapshotEntity
            {
                Id = "test",
                StreamId = streamInformation.StreamIdentifier,
                Version = 5,
                Data = json
            };

            var itemResponse = Substitute.For<ItemResponse<CosmosDbSnapshotEntity>>();
            itemResponse.Resource.Returns(snapshotEntity);

            container.ReadItemAsync<CosmosDbSnapshotEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            var result = await sut.GetAsync((JsonTypeInfo)TestJsonContext.Default.TestEntity, objectDocument, 5);

            Assert.NotNull(result);
            var entity = Assert.IsType<TestEntity>(result);
            Assert.Equal("Test", entity.Name);
            Assert.Equal(42, entity.Value);
        }
    }

    public class AutoCreateContainers : CosmosDbSnapShotStoreTests
    {
        [Fact]
        public async Task Should_create_database_and_container_when_auto_create_is_enabled()
        {
            var autoCreateSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                SnapshotsContainerName = "snapshots",
                AutoCreateContainers = true,
                DatabaseThroughput = new ThroughputSettings { AutoscaleMaxThroughput = 4000 }
            };

            var sut = new CosmosDbSnapShotStore(cosmosClient, autoCreateSettings);

            var databaseResponse = Substitute.For<DatabaseResponse>();
            databaseResponse.Database.Returns(database);
            cosmosClient.CreateDatabaseIfNotExistsAsync(
                autoCreateSettings.DatabaseName,
                Arg.Any<ThroughputProperties>(),
                Arg.Any<RequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(databaseResponse);

            var containerResponse = Substitute.For<ContainerResponse>();
            database.CreateContainerIfNotExistsAsync(
                Arg.Any<ContainerProperties>(),
                Arg.Any<ThroughputProperties>(),
                Arg.Any<RequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(containerResponse);

            container.ReadItemAsync<CosmosDbSnapshotEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            await sut.GetAsync(TestJsonContext.Default.TestEntity, objectDocument, 5);

            await cosmosClient.Received(1).CreateDatabaseIfNotExistsAsync(
                autoCreateSettings.DatabaseName,
                Arg.Any<ThroughputProperties>(),
                Arg.Any<RequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_create_container_with_manual_throughput()
        {
            var autoCreateSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                SnapshotsContainerName = "snapshots",
                AutoCreateContainers = true,
                SnapshotsThroughput = new ThroughputSettings { ManualThroughput = 400 }
            };

            var sut = new CosmosDbSnapShotStore(cosmosClient, autoCreateSettings);

            var databaseResponse = Substitute.For<DatabaseResponse>();
            databaseResponse.Database.Returns(database);
            cosmosClient.CreateDatabaseIfNotExistsAsync(
                Arg.Any<string>(),
                Arg.Any<ThroughputProperties?>()).Returns(databaseResponse);

            var containerResponse = Substitute.For<ContainerResponse>();
            database.CreateContainerIfNotExistsAsync(
                Arg.Any<ContainerProperties>(),
                Arg.Any<ThroughputProperties>()).Returns(containerResponse);

            database.GetContainer(autoCreateSettings.SnapshotsContainerName).Returns(container);

            container.ReadItemAsync<CosmosDbSnapshotEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            await sut.GetAsync(TestJsonContext.Default.TestEntity, objectDocument, 5);

            await database.Received(1).CreateContainerIfNotExistsAsync(
                Arg.Any<ContainerProperties>(),
                Arg.Any<ThroughputProperties>());
        }

        [Fact]
        public async Task Should_create_container_without_throughput()
        {
            var autoCreateSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                SnapshotsContainerName = "snapshots",
                AutoCreateContainers = true
                // No throughput settings
            };

            var sut = new CosmosDbSnapShotStore(cosmosClient, autoCreateSettings);

            var databaseResponse = Substitute.For<DatabaseResponse>();
            databaseResponse.Database.Returns(database);
            cosmosClient.CreateDatabaseIfNotExistsAsync(
                Arg.Any<string>(),
                Arg.Any<ThroughputProperties?>()).Returns(databaseResponse);

            var containerResponse = Substitute.For<ContainerResponse>();
            database.CreateContainerIfNotExistsAsync(
                Arg.Any<ContainerProperties>(),
                Arg.Any<int?>(),
                Arg.Any<RequestOptions>(),
                Arg.Any<CancellationToken>()).ReturnsForAnyArgs(containerResponse);

            database.GetContainer(autoCreateSettings.SnapshotsContainerName).Returns(container);

            container.ReadItemAsync<CosmosDbSnapshotEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            await sut.GetAsync(TestJsonContext.Default.TestEntity, objectDocument, 5);

            // Verify container was accessed
            database.Received(1).GetContainer(autoCreateSettings.SnapshotsContainerName);
        }
    }

    public class GetAsyncWithName : CosmosDbSnapShotStoreTests
    {
        [Fact]
        public async Task Should_return_null_when_named_snapshot_not_found()
        {
            var sut = new CosmosDbSnapShotStore(cosmosClient, settings);

            container.ReadItemAsync<CosmosDbSnapshotEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, objectDocument, 5, "v2");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_named_snapshot_when_found()
        {
            var sut = new CosmosDbSnapShotStore(cosmosClient, settings);

            var testEntity = new TestEntity { Name = "Named", Value = 100 };
            var json = JsonSerializer.Serialize(testEntity, TestJsonContext.Default.TestEntity);

            var snapshotEntity = new CosmosDbSnapshotEntity
            {
                Id = "test",
                StreamId = streamInformation.StreamIdentifier,
                Version = 5,
                Name = "v2",
                Data = json
            };

            var itemResponse = Substitute.For<ItemResponse<CosmosDbSnapshotEntity>>();
            itemResponse.Resource.Returns(snapshotEntity);

            container.ReadItemAsync<CosmosDbSnapshotEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, objectDocument, 5, "v2");

            Assert.NotNull(result);
            Assert.Equal("Named", result.Name);
        }
    }

    public class ContainerCaching : CosmosDbSnapShotStoreTests
    {
        [Fact]
        public async Task Should_reuse_container_on_subsequent_calls()
        {
            var sut = new CosmosDbSnapShotStore(cosmosClient, settings);

            container.ReadItemAsync<CosmosDbSnapshotEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            await sut.GetAsync(TestJsonContext.Default.TestEntity, objectDocument, 5);
            await sut.GetAsync(TestJsonContext.Default.TestEntity, objectDocument, 10);

            // Database.GetContainer should only be called once due to caching
            database.Received(1).GetContainer(settings.SnapshotsContainerName);
        }
    }
}
