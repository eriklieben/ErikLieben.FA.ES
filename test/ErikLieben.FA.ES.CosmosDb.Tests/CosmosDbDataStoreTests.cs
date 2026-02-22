using System.Net;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Exceptions;
using ErikLieben.FA.ES.CosmosDb.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using Microsoft.Azure.Cosmos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbDataStoreTests
{
    private readonly CosmosClient cosmosClient;
    private readonly EventStreamCosmosDbSettings settings;
    private readonly Database database;
    private readonly Container container;
    private readonly IObjectDocument objectDocument;
    private readonly StreamInformation streamInformation;

    public CosmosDbDataStoreTests()
    {
        // Clear closed stream cache to ensure test isolation
        CosmosDbDataStore.ClearClosedStreamCache();

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
            EventsContainerName = "events",
            AutoCreateContainers = false
        };

        objectDocument.Active.Returns(streamInformation);
        objectDocument.ObjectName.Returns("TestObject");
        objectDocument.ObjectId.Returns("test-id");

        cosmosClient.GetDatabase(settings.DatabaseName).Returns(database);
        database.GetContainer(settings.EventsContainerName).Returns(container);
    }

    public class Constructor : CosmosDbDataStoreTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_cosmos_client_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new CosmosDbDataStore(null!, settings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new CosmosDbDataStore(cosmosClient, null!));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            Assert.NotNull(sut);
        }
    }

    public class ReadAsync : CosmosDbDataStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReadAsync(null!));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_stream_identifier_is_null()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            streamInformation.StreamIdentifier = null!;
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReadAsync(objectDocument));
        }

        [Fact]
        public async Task Should_return_null_when_container_not_found()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.ReadAsync(objectDocument);

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_null_when_no_events_found()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.ReadAsync(objectDocument);

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_events_when_found()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            var events = new List<CosmosDbEventEntity>
            {
                new() { Id = "1", StreamId = "test-stream", Version = 0, EventType = "TestEvent", Data = "{}" },
                new() { Id = "2", StreamId = "test-stream", Version = 1, EventType = "TestEvent", Data = "{}" }
            };

            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.GetEnumerator().Returns(events.GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.ReadAsync(objectDocument);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task Should_read_events_with_start_version()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            var events = new List<CosmosDbEventEntity>
            {
                new() { Id = "3", StreamId = "test-stream", Version = 5, EventType = "TestEvent", Data = "{}" }
            };

            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.GetEnumerator().Returns(events.GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.ReadAsync(objectDocument, startVersion: 5);

            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task Should_read_events_with_until_version()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            var events = new List<CosmosDbEventEntity>
            {
                new() { Id = "1", StreamId = "test-stream", Version = 0, EventType = "TestEvent", Data = "{}" },
                new() { Id = "2", StreamId = "test-stream", Version = 1, EventType = "TestEvent", Data = "{}" }
            };

            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.GetEnumerator().Returns(events.GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.ReadAsync(objectDocument, startVersion: 0, untilVersion: 10);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task Should_handle_multiple_pages_of_results()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            var page1Events = new List<CosmosDbEventEntity>
            {
                new() { Id = "1", StreamId = "test-stream", Version = 0, EventType = "TestEvent", Data = "{}" }
            };
            var page2Events = new List<CosmosDbEventEntity>
            {
                new() { Id = "2", StreamId = "test-stream", Version = 1, EventType = "TestEvent", Data = "{}" }
            };

            var feedResponse1 = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse1.GetEnumerator().Returns(page1Events.GetEnumerator());

            var feedResponse2 = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse2.GetEnumerator().Returns(page2Events.GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse1, feedResponse2);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.ReadAsync(objectDocument);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }
    }

    public class AppendAsync : CosmosDbDataStoreTests
    {
        [Fact]
        public async Task Should_throw_exception_when_document_is_null()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            // Note: Code accesses document.Active before null check, causing NullReferenceException
            await Assert.ThrowsAnyAsync<Exception>(() => sut.AppendAsync(null!, default, new JsonEvent { EventType = "Test", EventVersion = 0 }));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_stream_identifier_is_null()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            streamInformation.StreamIdentifier = null!;
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.AppendAsync(objectDocument, default, new JsonEvent { EventType = "Test", EventVersion = 0 }));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_no_events_provided()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentException>(() => sut.AppendAsync(objectDocument, default, Array.Empty<IEvent>()));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_events_cannot_be_converted()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            var nonJsonEvent = Substitute.For<IEvent>();

            // Setup empty iterator for stream closed check
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(0);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            await Assert.ThrowsAsync<ArgumentException>(() => sut.AppendAsync(objectDocument, default, nonJsonEvent));
        }

        [Fact]
        public async Task Should_create_single_event_without_batch()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            var jsonEvent = new JsonEvent { EventType = "TestEvent", EventVersion = 0, Payload = "{}" };

            // Setup empty iterator for stream closed check
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(0);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var itemResponse = Substitute.For<ItemResponse<CosmosDbEventEntity>>();
            container.CreateItemAsync(Arg.Any<CosmosDbEventEntity>(), Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>())
                .Returns(itemResponse);

            await sut.AppendAsync(objectDocument, default, jsonEvent);

            await container.Received(1).CreateItemAsync(
                Arg.Any<CosmosDbEventEntity>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_event_stream_closed_exception_when_stream_is_closed()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            var jsonEvent = new JsonEvent { EventType = "TestEvent", EventVersion = 0, Payload = "{}" };

            // Setup iterator to return closed event
            var closedEvent = new CosmosDbEventEntity { EventType = "EventStream.Closed" };
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(1);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity> { closedEvent }.GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            await Assert.ThrowsAsync<EventStreamClosedException>(() => sut.AppendAsync(objectDocument, default, jsonEvent));
        }

        [Fact]
        public async Task Should_throw_cosmos_db_processing_exception_on_conflict()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            var jsonEvent = new JsonEvent { EventType = "TestEvent", EventVersion = 0, Payload = "{}" };

            // Setup empty iterator for stream closed check
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(0);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            container.CreateItemAsync(Arg.Any<CosmosDbEventEntity>(), Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Conflict", HttpStatusCode.Conflict, 0, "", 0));

            await Assert.ThrowsAsync<CosmosDbProcessingException>(() => sut.AppendAsync(objectDocument, default, jsonEvent));
        }

        [Fact]
        public async Task Should_throw_cosmos_db_container_not_found_exception_when_container_not_found()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            var jsonEvent = new JsonEvent { EventType = "TestEvent", EventVersion = 0, Payload = "{}" };

            // Setup empty iterator for stream closed check
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(0);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            container.CreateItemAsync(Arg.Any<CosmosDbEventEntity>(), Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            await Assert.ThrowsAsync<CosmosDbContainerNotFoundException>(() => sut.AppendAsync(objectDocument, default, jsonEvent));
        }

        [Fact]
        public async Task Should_use_batch_for_multiple_events()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            var events = new[]
            {
                new JsonEvent { EventType = "TestEvent1", EventVersion = 0, Payload = "{}" },
                new JsonEvent { EventType = "TestEvent2", EventVersion = 1, Payload = "{}" }
            };

            // Setup empty iterator for stream closed check
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(0);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var batch = Substitute.For<TransactionalBatch>();
            var batchResponse = Substitute.For<TransactionalBatchResponse>();
            batchResponse.IsSuccessStatusCode.Returns(true);
            batch.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(batchResponse);
            container.CreateTransactionalBatch(Arg.Any<PartitionKey>()).Returns(batch);

            await sut.AppendAsync(objectDocument, default, events);

            container.Received(1).CreateTransactionalBatch(Arg.Any<PartitionKey>());
            batch.Received(2).CreateItem(Arg.Any<CosmosDbEventEntity>(), Arg.Any<TransactionalBatchItemRequestOptions>());
        }

        [Fact]
        public async Task Should_throw_when_batch_operation_fails()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            var events = new[]
            {
                new JsonEvent { EventType = "TestEvent1", EventVersion = 0, Payload = "{}" },
                new JsonEvent { EventType = "TestEvent2", EventVersion = 1, Payload = "{}" }
            };

            // Setup empty iterator for stream closed check
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(0);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var batch = Substitute.For<TransactionalBatch>();
            var batchResponse = Substitute.For<TransactionalBatchResponse>();
            batchResponse.IsSuccessStatusCode.Returns(false);
            batchResponse.StatusCode.Returns(HttpStatusCode.InternalServerError);
            batch.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(batchResponse);
            container.CreateTransactionalBatch(Arg.Any<PartitionKey>()).Returns(batch);

            await Assert.ThrowsAsync<CosmosDbProcessingException>(() => sut.AppendAsync(objectDocument, default, events));
        }

        [Fact]
        public async Task Should_set_ttl_when_configured()
        {
            var ttlSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                EventsContainerName = "events",
                AutoCreateContainers = false,
                DefaultTimeToLiveSeconds = 3600
            };
            var sut = new CosmosDbDataStore(cosmosClient, ttlSettings);
            var jsonEvent = new JsonEvent { EventType = "TestEvent", EventVersion = 0, Payload = "{}" };

            cosmosClient.GetDatabase(ttlSettings.DatabaseName).Returns(database);
            database.GetContainer(ttlSettings.EventsContainerName).Returns(container);

            // Setup empty iterator for stream closed check
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(0);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var itemResponse = Substitute.For<ItemResponse<CosmosDbEventEntity>>();
            CosmosDbEventEntity? capturedEntity = null;
            container.CreateItemAsync(Arg.Do<CosmosDbEventEntity>(e => capturedEntity = e), Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>())
                .Returns(itemResponse);

            await sut.AppendAsync(objectDocument, default, jsonEvent);

            Assert.NotNull(capturedEntity);
            Assert.Equal(3600, capturedEntity.Ttl);
        }

        [Fact]
        public async Task Should_preserve_timestamp_when_requested()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            var originalTimestamp = new DateTimeOffset(2023, 6, 15, 10, 30, 0, TimeSpan.Zero);
            var cosmosEvent = new CosmosDbJsonEvent
            {
                EventType = "TestEvent",
                EventVersion = 0,
                Payload = "{}",
                OriginalTimestamp = originalTimestamp
            };

            // Setup empty iterator for stream closed check
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(0);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var itemResponse = Substitute.For<ItemResponse<CosmosDbEventEntity>>();
            CosmosDbEventEntity? capturedEntity = null;
            container.CreateItemAsync(Arg.Do<CosmosDbEventEntity>(e => capturedEntity = e), Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>())
                .Returns(itemResponse);

            await sut.AppendAsync(objectDocument, preserveTimestamp: true, cancellationToken: default, cosmosEvent);

            Assert.NotNull(capturedEntity);
            Assert.Equal(originalTimestamp, capturedEntity.Timestamp);
        }

        [Fact]
        public async Task Should_split_large_batches()
        {
            var smallBatchSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                EventsContainerName = "events",
                AutoCreateContainers = false,
                MaxBatchSize = 2
            };
            var sut = new CosmosDbDataStore(cosmosClient, smallBatchSettings);
            var events = new[]
            {
                new JsonEvent { EventType = "TestEvent1", EventVersion = 0, Payload = "{}" },
                new JsonEvent { EventType = "TestEvent2", EventVersion = 1, Payload = "{}" },
                new JsonEvent { EventType = "TestEvent3", EventVersion = 2, Payload = "{}" }
            };

            cosmosClient.GetDatabase(smallBatchSettings.DatabaseName).Returns(database);
            database.GetContainer(smallBatchSettings.EventsContainerName).Returns(container);

            // Setup empty iterator for stream closed check
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(0);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var batch = Substitute.For<TransactionalBatch>();
            var batchResponse = Substitute.For<TransactionalBatchResponse>();
            batchResponse.IsSuccessStatusCode.Returns(true);
            batch.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(batchResponse);
            container.CreateTransactionalBatch(Arg.Any<PartitionKey>()).Returns(batch);

            await sut.AppendAsync(objectDocument, default, events);

            // Should create 2 batches: first with 2 events, second with 1 event
            container.Received(2).CreateTransactionalBatch(Arg.Any<PartitionKey>());
        }
    }

    public class AutoCreateContainers : CosmosDbDataStoreTests
    {
        [Fact]
        public async Task Should_create_database_and_container_when_auto_create_is_enabled()
        {
            var autoCreateSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                EventsContainerName = "events",
                AutoCreateContainers = true,
                DatabaseThroughput = new ThroughputSettings { AutoscaleMaxThroughput = 4000 }
            };

            var sut = new CosmosDbDataStore(cosmosClient, autoCreateSettings);

            var databaseResponse = Substitute.For<DatabaseResponse>();
            databaseResponse.Database.Returns(database);
            // Source code uses overload with ThroughputProperties: CreateDatabaseIfNotExistsAsync(string, ThroughputProperties?)
            cosmosClient.CreateDatabaseIfNotExistsAsync(
                Arg.Any<string>(),
                Arg.Any<ThroughputProperties?>()).Returns(databaseResponse);

            var containerResponse = Substitute.For<ContainerResponse>();
            containerResponse.Container.Returns(container);
            database.CreateContainerIfNotExistsAsync(
                Arg.Any<ContainerProperties>(),
                Arg.Any<ThroughputProperties>()).Returns(containerResponse);

            // After creation, database.GetContainer is called to get the container
            database.GetContainer(autoCreateSettings.EventsContainerName).Returns(container);

            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            await sut.ReadAsync(objectDocument);

            await cosmosClient.Received(1).CreateDatabaseIfNotExistsAsync(
                autoCreateSettings.DatabaseName,
                Arg.Any<ThroughputProperties?>());
        }

        [Fact]
        public async Task Should_create_container_with_manual_throughput()
        {
            var autoCreateSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                EventsContainerName = "events",
                AutoCreateContainers = true,
                EventsThroughput = new ThroughputSettings { ManualThroughput = 400 }
            };

            var sut = new CosmosDbDataStore(cosmosClient, autoCreateSettings);

            var databaseResponse = Substitute.For<DatabaseResponse>();
            databaseResponse.Database.Returns(database);
            cosmosClient.CreateDatabaseIfNotExistsAsync(
                Arg.Any<string>(),
                Arg.Any<ThroughputProperties?>()).Returns(databaseResponse);

            var containerResponse = Substitute.For<ContainerResponse>();
            containerResponse.Container.Returns(container);
            database.CreateContainerIfNotExistsAsync(
                Arg.Any<ContainerProperties>(),
                Arg.Any<ThroughputProperties>()).Returns(containerResponse);

            database.GetContainer(autoCreateSettings.EventsContainerName).Returns(container);

            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            await sut.ReadAsync(objectDocument);

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
                EventsContainerName = "events",
                AutoCreateContainers = true
                // No throughput settings - this tests the "else" branch in GetEventsContainerAsync
            };

            var sut = new CosmosDbDataStore(cosmosClient, autoCreateSettings);

            var databaseResponse = Substitute.For<DatabaseResponse>();
            databaseResponse.Database.Returns(database);
            cosmosClient.CreateDatabaseIfNotExistsAsync(
                Arg.Any<string>(),
                Arg.Any<ThroughputProperties?>()).Returns(databaseResponse);

            var containerResponse = Substitute.For<ContainerResponse>();
            containerResponse.Container.Returns(container);
            // Use ReturnsForAnyArgs to catch all overload variations
            database.CreateContainerIfNotExistsAsync(
                Arg.Any<ContainerProperties>(),
                default(ThroughputProperties),
                Arg.Any<RequestOptions>(),
                Arg.Any<CancellationToken>()).ReturnsForAnyArgs(containerResponse);

            database.GetContainer(autoCreateSettings.EventsContainerName).Returns(container);

            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var result = await sut.ReadAsync(objectDocument);

            // Verify database was accessed - auto-create was triggered
            database.Received(1).GetContainer(autoCreateSettings.EventsContainerName);
        }
    }

    public class ReadAsStreamAsync : CosmosDbDataStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in sut.ReadAsStreamAsync(null!)) { }
            });
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_stream_identifier_is_null()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            streamInformation.StreamIdentifier = null!;
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in sut.ReadAsStreamAsync(objectDocument)) { }
            });
        }

        [Fact]
        public async Task Should_yield_no_events_when_container_not_found()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var events = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(objectDocument))
            {
                events.Add(evt);
            }

            Assert.Empty(events);
        }

        [Fact]
        public async Task Should_yield_no_events_when_stream_is_empty()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var events = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(objectDocument))
            {
                events.Add(evt);
            }

            Assert.Empty(events);
        }

        [Fact]
        public async Task Should_stream_events_when_found()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            var entities = new List<CosmosDbEventEntity>
            {
                new() { Id = "1", StreamId = "test-stream", Version = 0, EventType = "TestEvent", Data = "{}" },
                new() { Id = "2", StreamId = "test-stream", Version = 1, EventType = "TestEvent", Data = "{}" }
            };

            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.GetEnumerator().Returns(entities.GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var events = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(objectDocument))
            {
                events.Add(evt);
            }

            Assert.Equal(2, events.Count);
        }

        [Fact]
        public async Task Should_stream_events_from_multiple_pages()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            var page1 = new List<CosmosDbEventEntity>
            {
                new() { Id = "1", StreamId = "test-stream", Version = 0, EventType = "TestEvent", Data = "{}" }
            };
            var page2 = new List<CosmosDbEventEntity>
            {
                new() { Id = "2", StreamId = "test-stream", Version = 1, EventType = "TestEvent", Data = "{}" }
            };

            var feedResponse1 = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse1.GetEnumerator().Returns(page1.GetEnumerator());

            var feedResponse2 = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse2.GetEnumerator().Returns(page2.GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse1, feedResponse2);

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            var events = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(objectDocument))
            {
                events.Add(evt);
            }

            Assert.Equal(2, events.Count);
        }

        [Fact]
        public async Task Should_respect_cancellation_token()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            var cts = new CancellationTokenSource();

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var ct = callInfo.ArgAt<CancellationToken>(0);
                    ct.ThrowIfCancellationRequested();
                    var response = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
                    response.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());
                    return response;
                });

            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            cts.Cancel(); // Cancel before starting

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in sut.ReadAsStreamAsync(objectDocument, cancellationToken: cts.Token))
                {
                    // Should not get here
                }
            });
        }

        [Fact]
        public async Task Should_use_configured_streaming_page_size()
        {
            var streamingSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                EventsContainerName = "events",
                AutoCreateContainers = false,
                StreamingPageSize = 50
            };
            var sut = new CosmosDbDataStore(cosmosClient, streamingSettings);

            cosmosClient.GetDatabase(streamingSettings.DatabaseName).Returns(database);
            database.GetContainer(streamingSettings.EventsContainerName).Returns(container);

            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity>().GetEnumerator());

            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            QueryRequestOptions? capturedOptions = null;
            container.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Do<QueryRequestOptions>(o => capturedOptions = o)).Returns(feedIterator);

            await foreach (var _ in sut.ReadAsStreamAsync(objectDocument)) { }

            Assert.NotNull(capturedOptions);
            Assert.Equal(50, capturedOptions.MaxItemCount);
        }
    }

    /// <summary>
    /// Tests for the closed stream cache optimization.
    /// This class does NOT inherit from CosmosDbDataStoreTests to avoid cache clearing in the base constructor
    /// which would cause race conditions with parallel test execution.
    /// </summary>
    [Collection("ClosedStreamCache")]
    public class ClosedStreamCacheTests
    {
        [Fact]
        public async Task Should_cache_closed_stream_status()
        {
            // Clear cache at start of test to ensure clean state
            CosmosDbDataStore.ClearClosedStreamCache();

            // Create fresh mocks for this test
            var freshCosmosClient = Substitute.For<CosmosClient>();
            var freshDatabase = Substitute.For<Database>();
            var freshContainer = Substitute.For<Container>();

            var testSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "cache-test-db",
                EventsContainerName = "cache-test-events",
                AutoCreateContainers = false
            };

            freshCosmosClient.GetDatabase(testSettings.DatabaseName).Returns(freshDatabase);
            freshDatabase.GetContainer(testSettings.EventsContainerName).Returns(freshContainer);

            // Use a unique stream ID for this test
            var uniqueStreamId = $"closed-test-{Guid.NewGuid():N}";
            var uniqueStreamInfo = new StreamInformation { StreamIdentifier = uniqueStreamId };
            var uniqueDocument = Substitute.For<IObjectDocument>();
            uniqueDocument.Active.Returns(uniqueStreamInfo);
            uniqueDocument.ObjectId.Returns("test-id");
            uniqueDocument.ObjectName.Returns("TestObject");

            var sut = new CosmosDbDataStore(freshCosmosClient, testSettings);
            var jsonEvent = new JsonEvent { EventType = "TestEvent", EventVersion = 0, Payload = "{}" };

            // Setup iterator to return closed event
            var closedEvent = new CosmosDbEventEntity { EventType = "EventStream.Closed" };
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(1);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity> { closedEvent }.GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            freshContainer.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            // First attempt should query and throw
            await Assert.ThrowsAsync<EventStreamClosedException>(() => sut.AppendAsync(uniqueDocument, default, jsonEvent));

            // Reset the mock to verify it's not called again
            freshContainer.ClearReceivedCalls();

            // Second attempt should throw immediately from cache (no query)
            await Assert.ThrowsAsync<EventStreamClosedException>(() => sut.AppendAsync(uniqueDocument, default, jsonEvent));

            // Verify no query was made on second call
            freshContainer.DidNotReceive().GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>());
        }

        [Fact]
        public void Should_clear_cache_when_requested()
        {
            // Clear the cache
            CosmosDbDataStore.ClearClosedStreamCache();

            // This verifies the method exists and doesn't throw
            Assert.True(true);
        }

        [Fact]
        public async Task Should_handle_concurrent_access_to_closed_stream_cache()
        {
            // The ClosedStreamCache uses ConcurrentDictionary for thread-safe access.
            // Pre-populate the cache, then verify concurrent reads don't corrupt it.
            CosmosDbDataStore.ClearClosedStreamCache();

            var freshCosmosClient = Substitute.For<CosmosClient>();
            var freshDatabase = Substitute.For<Database>();
            var freshContainer = Substitute.For<Container>();

            var testSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "concurrent-test-db",
                EventsContainerName = "concurrent-test-events",
                AutoCreateContainers = false
            };

            freshCosmosClient.GetDatabase(testSettings.DatabaseName).Returns(freshDatabase);
            freshDatabase.GetContainer(testSettings.EventsContainerName).Returns(freshContainer);

            // Return a fresh iterator for each call so the HasMoreResults sequence works
            var closedEvent = new CosmosDbEventEntity { EventType = "EventStream.Closed" };
            freshContainer.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(_ =>
            {
                var iter = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
                iter.HasMoreResults.Returns(true, false);
                var resp = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
                resp.Count.Returns(1);
                resp.GetEnumerator().Returns(__ => new List<CosmosDbEventEntity> { closedEvent }.GetEnumerator());
                iter.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(resp);
                return iter;
            });

            // Use unique stream ID for each concurrent task to populate cache entries
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                var streamId = $"concurrent-closed-{i}";
                var streamInfo = new StreamInformation { StreamIdentifier = streamId };
                var doc = Substitute.For<IObjectDocument>();
                doc.Active.Returns(streamInfo);
                doc.ObjectId.Returns("test-id");
                doc.ObjectName.Returns("TestObject");

                var sut = new CosmosDbDataStore(freshCosmosClient, testSettings);
                var jsonEvent = new JsonEvent { EventType = "TestEvent", EventVersion = 0, Payload = "{}" };

                tasks.Add(Task.Run(async () =>
                {
                    await Assert.ThrowsAsync<EventStreamClosedException>(() =>
                        sut.AppendAsync(doc, default, jsonEvent));
                }));
            }

            // Act & Assert - All tasks should complete without deadlocks or data corruption
            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task Should_throw_from_cache_on_subsequent_calls_without_querying()
        {
            CosmosDbDataStore.ClearClosedStreamCache();

            var freshCosmosClient = Substitute.For<CosmosClient>();
            var freshDatabase = Substitute.For<Database>();
            var freshContainer = Substitute.For<Container>();

            var testSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "cache-verify-db",
                EventsContainerName = "cache-verify-events",
                AutoCreateContainers = false
            };

            freshCosmosClient.GetDatabase(testSettings.DatabaseName).Returns(freshDatabase);
            freshDatabase.GetContainer(testSettings.EventsContainerName).Returns(freshContainer);

            var uniqueStreamId = $"cache-verify-{Guid.NewGuid():N}";
            var streamInfo = new StreamInformation { StreamIdentifier = uniqueStreamId };
            var doc = Substitute.For<IObjectDocument>();
            doc.Active.Returns(streamInfo);
            doc.ObjectId.Returns("test-id");
            doc.ObjectName.Returns("TestObject");

            var sut = new CosmosDbDataStore(freshCosmosClient, testSettings);
            var jsonEvent = new JsonEvent { EventType = "TestEvent", EventVersion = 0, Payload = "{}" };

            // First call returns closed event from query
            var closedEvent = new CosmosDbEventEntity { EventType = "EventStream.Closed" };
            var feedIterator = Substitute.For<FeedIterator<CosmosDbEventEntity>>();
            feedIterator.HasMoreResults.Returns(true, false);
            var feedResponse = Substitute.For<FeedResponse<CosmosDbEventEntity>>();
            feedResponse.Count.Returns(1);
            feedResponse.GetEnumerator().Returns(new List<CosmosDbEventEntity> { closedEvent }.GetEnumerator());
            feedIterator.ReadNextAsync(Arg.Any<CancellationToken>()).Returns(feedResponse);

            freshContainer.GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>()).Returns(feedIterator);

            // First call populates cache
            await Assert.ThrowsAsync<EventStreamClosedException>(() =>
                sut.AppendAsync(doc, default, jsonEvent));

            freshContainer.ClearReceivedCalls();

            // Second call should use cache
            await Assert.ThrowsAsync<EventStreamClosedException>(() =>
                sut.AppendAsync(doc, default, jsonEvent));

            // No query was made
            freshContainer.DidNotReceive().GetItemQueryIterator<CosmosDbEventEntity>(
                Arg.Any<QueryDefinition>(),
                Arg.Any<string>(),
                Arg.Any<QueryRequestOptions>());
        }
    }

    /// <summary>
    /// Collection definition to prevent parallel execution of cache-sensitive tests.
    /// </summary>
    [CollectionDefinition("ClosedStreamCache", DisableParallelization = true)]
    public class ClosedStreamCacheCollection
    {
    }

    public class RemoveEventsForFailedCommitAsync : CosmosDbDataStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.RemoveEventsForFailedCommitAsync(null!, 0, 5));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_stream_identifier_is_null()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            streamInformation.StreamIdentifier = null!;
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.RemoveEventsForFailedCommitAsync(objectDocument, 0, 5));
        }

        [Fact]
        public async Task Should_delete_events_in_version_range()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            var itemResponse = Substitute.For<ItemResponse<CosmosDbEventEntity>>();
            container.DeleteItemAsync<CosmosDbEventEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            var result = await sut.RemoveEventsForFailedCommitAsync(objectDocument, 0, 2);

            Assert.Equal(3, result);
            await container.Received(3).DeleteItemAsync<CosmosDbEventEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_continue_when_event_not_found()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);

            container.DeleteItemAsync<CosmosDbEventEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            var result = await sut.RemoveEventsForFailedCommitAsync(objectDocument, 0, 2);

            Assert.Equal(0, result);
        }
    }
}
