using System.Dynamic;
using System.Net;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Model;
using Microsoft.Azure.Cosmos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbObjectIdProviderTests
{
    private readonly CosmosClient cosmosClient;
    private readonly EventStreamCosmosDbSettings settings;
    private readonly Database database;
    private readonly Container container;

    public CosmosDbObjectIdProviderTests()
    {
        cosmosClient = Substitute.For<CosmosClient>();
        database = Substitute.For<Database>();
        container = Substitute.For<Container>();

        settings = new EventStreamCosmosDbSettings
        {
            DatabaseName = "test-db",
            DocumentsContainerName = "documents",
            AutoCreateContainers = false
        };

        cosmosClient.GetDatabase(settings.DatabaseName).Returns(database);
        database.GetContainer(settings.DocumentsContainerName).Returns(container);
    }

    public class Constructor : CosmosDbObjectIdProviderTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_cosmos_client_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new CosmosDbObjectIdProvider(null!, settings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new CosmosDbObjectIdProvider(cosmosClient, null!));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);
            Assert.NotNull(sut);
        }
    }

    public class GetObjectIdsAsync : CosmosDbObjectIdProviderTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetObjectIdsAsync(null!, null, 10));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_object_name_is_empty()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentException>(() => sut.GetObjectIdsAsync("", null, 10));
        }

        [Fact]
        public async Task Should_throw_argument_out_of_range_exception_when_page_size_is_less_than_1()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.GetObjectIdsAsync("TestObject", null, 0));
        }

        [Fact]
        public async Task Should_return_empty_result_when_container_not_found()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);

            var feedIterator = Substitute.For<FeedIterator<dynamic>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            container.GetItemQueryIterator<dynamic>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.GetObjectIdsAsync("TestObject", null, 10);

            Assert.Empty(result.Items);
            Assert.Null(result.ContinuationToken);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public async Task Should_return_empty_result_when_no_objects_found()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);

            var feedResponse = Substitute.For<FeedResponse<dynamic>>();
            feedResponse.GetEnumerator().Returns(new List<dynamic>().GetEnumerator());
            feedResponse.ContinuationToken.Returns((string?)null);

            var feedIterator = Substitute.For<FeedIterator<dynamic>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<dynamic>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.GetObjectIdsAsync("TestObject", null, 10);

            Assert.Empty(result.Items);
        }

        [Fact]
        public async Task Should_return_object_ids_when_found()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);

            dynamic item1 = new ExpandoObject();
            item1.objectId = "obj-1";
            dynamic item2 = new ExpandoObject();
            item2.objectId = "obj-2";
            dynamic item3 = new ExpandoObject();
            item3.objectId = "obj-3";

            var items = new List<dynamic> { item1, item2, item3 };

            var feedResponse = Substitute.For<FeedResponse<dynamic>>();
            feedResponse.GetEnumerator().Returns(items.GetEnumerator());
            feedResponse.ContinuationToken.Returns("next-page-token");

            var feedIterator = Substitute.For<FeedIterator<dynamic>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<dynamic>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.GetObjectIdsAsync("TestObject", null, 10);

            Assert.Equal(3, result.Items.Count());
            Assert.Equal("next-page-token", result.ContinuationToken);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public async Task Should_pass_continuation_token_to_query()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);

            var feedResponse = Substitute.For<FeedResponse<dynamic>>();
            feedResponse.GetEnumerator().Returns(new List<dynamic>().GetEnumerator());
            feedResponse.ContinuationToken.Returns((string?)null);

            var feedIterator = Substitute.For<FeedIterator<dynamic>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<dynamic>(
                Arg.Any<QueryDefinition>(),
                Arg.Is<string>("existing-continuation-token"),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            await sut.GetObjectIdsAsync("TestObject", "existing-continuation-token", 25);

            container.Received(1).GetItemQueryIterator<dynamic>(
                Arg.Any<QueryDefinition>(),
                "existing-continuation-token",
                Arg.Any<QueryRequestOptions>());
        }

        [Fact]
        public async Task Should_throw_argument_out_of_range_exception_when_page_size_is_negative()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.GetObjectIdsAsync("TestObject", null, -1));
        }
    }

    public class ContainerCaching : CosmosDbObjectIdProviderTests
    {
        [Fact]
        public async Task Should_reuse_container_on_subsequent_calls()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);

            container.ReadItemAsync<dynamic>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            await sut.ExistsAsync("TestObject", "id-1");
            await sut.ExistsAsync("TestObject", "id-2");

            database.Received(1).GetContainer(settings.DocumentsContainerName);
        }
    }

    public class ExistsAsync : CosmosDbObjectIdProviderTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ExistsAsync(null!, "test-id"));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_id_is_null()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ExistsAsync("TestObject", null!));
        }

        [Fact]
        public async Task Should_return_false_when_document_not_found()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);

            container.ReadItemAsync<dynamic>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            var result = await sut.ExistsAsync("TestObject", "test-id");

            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_true_when_document_exists()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);

            var itemResponse = Substitute.For<ItemResponse<dynamic>>();
            container.ReadItemAsync<dynamic>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            var result = await sut.ExistsAsync("TestObject", "test-id");

            Assert.True(result);
        }
    }

    public class CountAsync : CosmosDbObjectIdProviderTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.CountAsync(null!));
        }

        [Fact]
        public async Task Should_return_zero_when_container_not_found()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);

            var feedIterator = Substitute.For<FeedIterator<long>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            container.GetItemQueryIterator<long>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.CountAsync("TestObject");

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_return_zero_when_no_results()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);

            var feedIterator = Substitute.For<FeedIterator<long>>();
            feedIterator.HasMoreResults.Returns(false);

            container.GetItemQueryIterator<long>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.CountAsync("TestObject");

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_return_count_when_objects_exist()
        {
            var sut = new CosmosDbObjectIdProvider(cosmosClient, settings);

            var feedResponse = Substitute.For<FeedResponse<long>>();
            feedResponse.GetEnumerator().Returns(new List<long> { 42L }.GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<long>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<long>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.CountAsync("TestObject");

            Assert.Equal(42, result);
        }
    }

    public class AutoCreateContainers : CosmosDbObjectIdProviderTests
    {
        [Fact]
        public async Task Should_create_database_and_container_when_auto_create_is_enabled()
        {
            var autoCreateSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                DocumentsContainerName = "documents",
                AutoCreateContainers = true
            };

            var sut = new CosmosDbObjectIdProvider(cosmosClient, autoCreateSettings);

            var databaseResponse = Substitute.For<DatabaseResponse>();
            databaseResponse.Database.Returns(database);
            // Source code uses simple overload: CreateDatabaseIfNotExistsAsync(string)
            cosmosClient.CreateDatabaseIfNotExistsAsync(Arg.Any<string>()).Returns(databaseResponse);

            var containerResponse = Substitute.For<ContainerResponse>();
            containerResponse.Container.Returns(container);
            database.CreateContainerIfNotExistsAsync(Arg.Any<ContainerProperties>()).Returns(containerResponse);

            container.ReadItemAsync<dynamic>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            await sut.ExistsAsync("TestObject", "test-id");

            await cosmosClient.Received(1).CreateDatabaseIfNotExistsAsync(autoCreateSettings.DatabaseName);
        }
    }
}
