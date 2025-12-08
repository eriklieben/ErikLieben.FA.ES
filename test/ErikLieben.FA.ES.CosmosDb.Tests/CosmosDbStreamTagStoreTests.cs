using System.Dynamic;
using System.Net;
using System.Text.Json;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Model;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbStreamTagStoreTests
{
    private readonly CosmosClient cosmosClient;
    private readonly EventStreamCosmosDbSettings settings;
    private readonly Database database;
    private readonly Container container;
    private readonly IObjectDocument objectDocument;
    private readonly StreamInformation streamInformation;

    public CosmosDbStreamTagStoreTests()
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
            TagsContainerName = "tags",
            AutoCreateContainers = false
        };

        objectDocument.Active.Returns(streamInformation);
        objectDocument.ObjectName.Returns("TestObject");
        objectDocument.ObjectId.Returns("test-id");

        cosmosClient.GetDatabase(settings.DatabaseName).Returns(database);
        database.GetContainer(settings.TagsContainerName).Returns(container);
    }

    public class Constructor : CosmosDbStreamTagStoreTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_cosmos_client_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new CosmosDbStreamTagStore(null!, settings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new CosmosDbStreamTagStore(cosmosClient, null!));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            var sut = new CosmosDbStreamTagStore(cosmosClient, settings);
            Assert.NotNull(sut);
        }
    }

    public class SetAsync : CosmosDbStreamTagStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = new CosmosDbStreamTagStore(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(null!, "test-tag"));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_stream_identifier_is_null()
        {
            var sut = new CosmosDbStreamTagStore(cosmosClient, settings);
            streamInformation.StreamIdentifier = null!;
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(objectDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_upsert_stream_tag_entity()
        {
            var sut = new CosmosDbStreamTagStore(cosmosClient, settings);

            var itemResponse = Substitute.For<ItemResponse<CosmosDbTagEntity>>();
            container.UpsertItemAsync(
                Arg.Any<CosmosDbTagEntity>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            await sut.SetAsync(objectDocument, "test-tag");

            await container.Received(1).UpsertItemAsync(
                Arg.Is<CosmosDbTagEntity>(e =>
                    e.TagType == "stream" &&
                    e.ObjectName == "TestObject" &&
                    e.ObjectId == "test-id"),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_sanitize_tag_with_invalid_characters()
        {
            var sut = new CosmosDbStreamTagStore(cosmosClient, settings);

            var itemResponse = Substitute.For<ItemResponse<CosmosDbTagEntity>>();
            container.UpsertItemAsync(
                Arg.Any<CosmosDbTagEntity>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            await sut.SetAsync(objectDocument, "test/tag#with?invalid\\chars");

            await container.Received(1).UpsertItemAsync(
                Arg.Is<CosmosDbTagEntity>(e => e.Tag == "test/tag#with?invalid\\chars"),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class GetAsync : CosmosDbStreamTagStoreTests
    {
        private static JsonElement CreateJsonElement(string objectId)
        {
            var json = JsonSerializer.Serialize(new { objectId });
            return JsonDocument.Parse(json).RootElement.Clone();
        }

        [Fact]
        public async Task Should_return_empty_when_container_not_found()
        {
            var sut = new CosmosDbStreamTagStore(cosmosClient, settings);

            var feedIterator = Substitute.For<FeedIterator<JsonElement>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            container.GetItemQueryIterator<JsonElement>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.GetAsync("TestObject", "test-tag");

            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_return_empty_when_no_tags_found()
        {
            var sut = new CosmosDbStreamTagStore(cosmosClient, settings);

            var feedResponse = Substitute.For<FeedResponse<JsonElement>>();
            feedResponse.GetEnumerator().Returns(new List<JsonElement>().GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<JsonElement>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<JsonElement>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.GetAsync("TestObject", "test-tag");

            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_return_stream_ids_when_tags_found()
        {
            var sut = new CosmosDbStreamTagStore(cosmosClient, settings);

            var item1 = CreateJsonElement("stream-1");
            var item2 = CreateJsonElement("stream-2");

            var items = new List<JsonElement> { item1, item2 };

            var feedResponse = Substitute.For<FeedResponse<JsonElement>>();
            feedResponse.GetEnumerator().Returns(items.GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<JsonElement>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<JsonElement>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.GetAsync("TestObject", "test-tag");

            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task Should_handle_multiple_pages_of_results()
        {
            var sut = new CosmosDbStreamTagStore(cosmosClient, settings);

            var item1 = CreateJsonElement("stream-1");
            var item2 = CreateJsonElement("stream-2");

            var page1Items = new List<JsonElement> { item1 };
            var page2Items = new List<JsonElement> { item2 };

            var feedResponse1 = Substitute.For<FeedResponse<JsonElement>>();
            feedResponse1.GetEnumerator().Returns(page1Items.GetEnumerator());

            var feedResponse2 = Substitute.For<FeedResponse<JsonElement>>();
            feedResponse2.GetEnumerator().Returns(page2Items.GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<JsonElement>>();
            feedIterator.HasMoreResults.Returns(true, true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse1, feedResponse2);

            container.GetItemQueryIterator<JsonElement>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.GetAsync("TestObject", "test-tag");

            Assert.Equal(2, result.Count());
        }
    }

    public class ContainerCaching : CosmosDbStreamTagStoreTests
    {
        [Fact]
        public async Task Should_reuse_container_on_subsequent_calls()
        {
            var sut = new CosmosDbStreamTagStore(cosmosClient, settings);

            var feedIterator = Substitute.For<FeedIterator<JsonElement>>();
            feedIterator.HasMoreResults.Returns(false);

            container.GetItemQueryIterator<JsonElement>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            await sut.GetAsync("TestObject", "tag1");
            await sut.GetAsync("TestObject", "tag2");

            database.Received(1).GetContainer(settings.TagsContainerName);
        }
    }

    public class AutoCreateContainers : CosmosDbStreamTagStoreTests
    {
        [Fact]
        public async Task Should_create_database_and_container_when_auto_create_is_enabled()
        {
            var autoCreateSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                TagsContainerName = "tags",
                AutoCreateContainers = true,
                DatabaseThroughput = new ThroughputSettings { AutoscaleMaxThroughput = 4000 }
            };

            var sut = new CosmosDbStreamTagStore(cosmosClient, autoCreateSettings);

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

            var feedIterator = Substitute.For<FeedIterator<JsonElement>>();
            feedIterator.HasMoreResults.Returns(false);

            container.GetItemQueryIterator<JsonElement>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            await sut.GetAsync("TestObject", "test-tag");

            await cosmosClient.Received(1).CreateDatabaseIfNotExistsAsync(
                autoCreateSettings.DatabaseName,
                Arg.Any<ThroughputProperties>(),
                Arg.Any<RequestOptions>(),
                Arg.Any<CancellationToken>());
        }
    }
}
