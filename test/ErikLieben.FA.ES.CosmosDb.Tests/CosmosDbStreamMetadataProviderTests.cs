using System.Net;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Model;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbStreamMetadataProviderTests
{
    private readonly CosmosClient cosmosClient;
    private readonly EventStreamCosmosDbSettings settings;
    private readonly Database database;
    private readonly Container documentsContainer;
    private readonly Container eventsContainer;
    private readonly ILogger<CosmosDbStreamMetadataProvider> logger;

    public CosmosDbStreamMetadataProviderTests()
    {
        cosmosClient = Substitute.For<CosmosClient>();
        database = Substitute.For<Database>();
        documentsContainer = Substitute.For<Container>();
        eventsContainer = Substitute.For<Container>();
        logger = Substitute.For<ILogger<CosmosDbStreamMetadataProvider>>();

        settings = new EventStreamCosmosDbSettings
        {
            DatabaseName = "test-db",
            DocumentsContainerName = "documents",
            EventsContainerName = "events"
        };

        cosmosClient.GetDatabase(settings.DatabaseName).Returns(database);
        database.GetContainer(settings.DocumentsContainerName).Returns(documentsContainer);
        database.GetContainer(settings.EventsContainerName).Returns(eventsContainer);
    }

    public class Constructor : CosmosDbStreamMetadataProviderTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_cosmos_client_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbStreamMetadataProvider(null!, settings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbStreamMetadataProvider(cosmosClient, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings);
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_null_logger()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings, null);
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_logger()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings, logger);
            Assert.NotNull(sut);
        }
    }

    public class GetStreamMetadataAsync : CosmosDbStreamMetadataProviderTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetStreamMetadataAsync(null!, "test-id"));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_id_is_null()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetStreamMetadataAsync("TestObject", null!));
        }

        [Fact]
        public async Task Should_return_null_when_document_not_found()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings, logger);

            documentsContainer.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            var result = await sut.GetStreamMetadataAsync("TestObject", "test-id");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_null_when_no_events_found()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings);

            SetupDocumentLookup("test-stream-0000000000");
            SetupAggregateQuery(0, null, null);

            var result = await sut.GetStreamMetadataAsync("TestObject", "test-id");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_metadata_when_events_exist()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings);

            var oldest = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var newest = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);

            SetupDocumentLookup("test-stream-0000000000");
            SetupAggregateQuery(5, oldest, newest);

            var result = await sut.GetStreamMetadataAsync("TestObject", "test-id");

            Assert.NotNull(result);
            Assert.Equal("TestObject", result.ObjectName);
            Assert.Equal("test-id", result.ObjectId);
            Assert.Equal(5, result.EventCount);
            Assert.Equal(oldest, result.OldestEventDate);
            Assert.Equal(newest, result.NewestEventDate);
        }

        [Fact]
        public async Task Should_return_null_when_events_container_not_found()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings, logger);

            SetupDocumentLookup("test-stream-0000000000");

            var feedIterator = Substitute.For<FeedIterator<CosmosDbStreamMetadataProvider.AggregateResult>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            eventsContainer.GetItemQueryIterator<CosmosDbStreamMetadataProvider.AggregateResult>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.GetStreamMetadataAsync("TestObject", "test-id");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_metadata_with_single_event()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings);

            var timestamp = new DateTimeOffset(2024, 3, 10, 8, 30, 0, TimeSpan.Zero);

            SetupDocumentLookup("single-event-stream");
            SetupAggregateQuery(1, timestamp, timestamp);

            var result = await sut.GetStreamMetadataAsync("TestObject", "test-id");

            Assert.NotNull(result);
            Assert.Equal(1, result.EventCount);
            Assert.Equal(timestamp, result.OldestEventDate);
            Assert.Equal(timestamp, result.NewestEventDate);
        }

        [Fact]
        public async Task Should_return_null_when_iterator_has_no_results()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings);

            SetupDocumentLookup("test-stream-0000000000");

            var feedIterator = Substitute.For<FeedIterator<CosmosDbStreamMetadataProvider.AggregateResult>>();
            feedIterator.HasMoreResults.Returns(false);

            eventsContainer.GetItemQueryIterator<CosmosDbStreamMetadataProvider.AggregateResult>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.GetStreamMetadataAsync("TestObject", "test-id");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_null_when_document_has_no_stream_identifier()
        {
            var sut = new CosmosDbStreamMetadataProvider(cosmosClient, settings);

            var entity = new CosmosDbDocumentEntity
            {
                Id = "testobject_test-id",
                ObjectName = "testobject",
                ObjectId = "test-id",
                Active = new CosmosDbStreamInfo { StreamIdentifier = null! }
            };

            var response = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            response.Resource.Returns(entity);

            documentsContainer.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(response);

            // When streamId is null, the query should still work but return null
            SetupAggregateQuery(0, null, null);

            var result = await sut.GetStreamMetadataAsync("TestObject", "test-id");

            Assert.Null(result);
        }

        private void SetupDocumentLookup(string streamId)
        {
            var entity = new CosmosDbDocumentEntity
            {
                Id = "testobject_test-id",
                ObjectName = "testobject",
                ObjectId = "test-id",
                Active = new CosmosDbStreamInfo
                {
                    StreamIdentifier = streamId
                }
            };

            var response = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            response.Resource.Returns(entity);

            documentsContainer.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(response);
        }

        private void SetupAggregateQuery(int eventCount, DateTimeOffset? oldest, DateTimeOffset? newest)
        {
            var aggregateResult = new CosmosDbStreamMetadataProvider.AggregateResult
            {
                EventCount = eventCount,
                Oldest = oldest,
                Newest = newest
            };

            var feedResponse = Substitute.For<FeedResponse<CosmosDbStreamMetadataProvider.AggregateResult>>();
            feedResponse.GetEnumerator().Returns(
                new List<CosmosDbStreamMetadataProvider.AggregateResult> { aggregateResult }.GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbStreamMetadataProvider.AggregateResult>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            eventsContainer.GetItemQueryIterator<CosmosDbStreamMetadataProvider.AggregateResult>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);
        }
    }
}
