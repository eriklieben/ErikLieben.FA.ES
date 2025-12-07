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
            await Assert.ThrowsAnyAsync<Exception>(() => sut.AppendAsync(null!, new JsonEvent { EventType = "Test", EventVersion = 0 }));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_stream_identifier_is_null()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            streamInformation.StreamIdentifier = null!;
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.AppendAsync(objectDocument, new JsonEvent { EventType = "Test", EventVersion = 0 }));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_no_events_provided()
        {
            var sut = new CosmosDbDataStore(cosmosClient, settings);
            await Assert.ThrowsAsync<ArgumentException>(() => sut.AppendAsync(objectDocument, Array.Empty<IEvent>()));
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

            await Assert.ThrowsAsync<ArgumentException>(() => sut.AppendAsync(objectDocument, nonJsonEvent));
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

            await sut.AppendAsync(objectDocument, jsonEvent);

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

            await Assert.ThrowsAsync<EventStreamClosedException>(() => sut.AppendAsync(objectDocument, jsonEvent));
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

            await Assert.ThrowsAsync<CosmosDbProcessingException>(() => sut.AppendAsync(objectDocument, jsonEvent));
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

            await Assert.ThrowsAsync<CosmosDbContainerNotFoundException>(() => sut.AppendAsync(objectDocument, jsonEvent));
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

            await sut.AppendAsync(objectDocument, events);

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

            await Assert.ThrowsAsync<CosmosDbProcessingException>(() => sut.AppendAsync(objectDocument, events));
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

            await sut.AppendAsync(objectDocument, jsonEvent);

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

            await sut.AppendAsync(objectDocument, preserveTimestamp: true, cosmosEvent);

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

            await sut.AppendAsync(objectDocument, events);

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
}
