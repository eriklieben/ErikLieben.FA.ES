#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks

using System.Net;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Exceptions;
using ErikLieben.FA.ES.CosmosDb.Model;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbDocumentStoreTests
{
    private readonly CosmosClient cosmosClient;
    private readonly IDocumentTagDocumentFactory documentTagFactory;
    private readonly EventStreamCosmosDbSettings settings;
    private readonly Database database;
    private readonly Container container;

    public CosmosDbDocumentStoreTests()
    {
        cosmosClient = Substitute.For<CosmosClient>();
        documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
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

    public class Constructor : CosmosDbDocumentStoreTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_cosmos_client_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbDocumentStore(null!, documentTagFactory, settings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_document_tag_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbDocumentStore(cosmosClient, null!, settings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbDocumentStore(cosmosClient, documentTagFactory, null!));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);
            Assert.NotNull(sut);
        }
    }

    public class CreateAsync : CosmosDbDocumentStoreTests
    {
        [Fact]
        public async Task Should_return_existing_document_when_found()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);

            var existingEntity = new CosmosDbDocumentEntity
            {
                Id = "testobject_test-id",
                ObjectName = "testobject",
                ObjectId = "test-id",
                Active = new CosmosDbStreamInfo
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "cosmosdb",
                    CurrentStreamVersion = 5
                }
            };

            var itemResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            itemResponse.Resource.Returns(existingEntity);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            var result = await sut.CreateAsync("TestObject", "test-id");

            Assert.NotNull(result);
            Assert.Equal("test-id", result.ObjectId);
            Assert.Equal("stream-123", result.Active.StreamIdentifier);
        }

        [Fact]
        public async Task Should_create_new_document_when_not_found()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            var createdEntity = new CosmosDbDocumentEntity
            {
                Id = "testobject_test-id",
                ObjectName = "testobject",
                ObjectId = "test-id",
                Active = new CosmosDbStreamInfo
                {
                    StreamIdentifier = "testid-0000000000",
                    StreamType = "cosmosdb",
                    CurrentStreamVersion = -1
                }
            };

            var createResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            createResponse.Resource.Returns(createdEntity);

            container.CreateItemAsync(
                Arg.Any<CosmosDbDocumentEntity>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(createResponse);

            var result = await sut.CreateAsync("TestObject", "test-id");

            Assert.NotNull(result);
            Assert.Equal("test-id", result.ObjectId);
        }
    }

    public class GetAsync : CosmosDbDocumentStoreTests
    {
        [Fact]
        public async Task Should_return_document_when_found()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);

            var existingEntity = new CosmosDbDocumentEntity
            {
                Id = "testobject_test-id",
                ObjectName = "testobject",
                ObjectId = "test-id",
                Active = new CosmosDbStreamInfo
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "cosmosdb",
                    CurrentStreamVersion = 5
                }
            };

            var itemResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            itemResponse.Resource.Returns(existingEntity);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            var result = await sut.GetAsync("TestObject", "test-id");

            Assert.NotNull(result);
            Assert.Equal("test-id", result.ObjectId);
        }

        [Fact]
        public async Task Should_throw_document_not_found_exception_when_not_found()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            await Assert.ThrowsAsync<CosmosDbDocumentNotFoundException>(() =>
                sut.GetAsync("TestObject", "test-id"));
        }
    }

    public class SetAsync : CosmosDbDocumentStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(null!));
        }

        [Fact]
        public async Task Should_replace_document_without_concurrency_check_when_hash_is_empty()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);

            var document = Substitute.For<IObjectDocument>();
            document.ObjectName.Returns("testobject");
            document.ObjectId.Returns("test-id");
            document.Hash.Returns((string?)null);
            document.Active.Returns(new StreamInformation
            {
                StreamIdentifier = "stream-123",
                StreamType = "cosmosdb",
                CurrentStreamVersion = 5
            });
            document.TerminatedStreams.Returns([]);

            var itemResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            container.ReplaceItemAsync(
                Arg.Any<CosmosDbDocumentEntity>(),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            await sut.SetAsync(document);

            await container.Received(1).ReplaceItemAsync(
                Arg.Any<CosmosDbDocumentEntity>(),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_replace_document_with_optimistic_concurrency()
        {
            var settingsWithConcurrency = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                DocumentsContainerName = "documents",
                AutoCreateContainers = false,
                UseOptimisticConcurrency = true
            };

            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settingsWithConcurrency);

            var document = Substitute.For<IObjectDocument>();
            document.ObjectName.Returns("TestObject");
            document.ObjectId.Returns("test-id");
            document.Hash.Returns("existing-hash");
            document.Active.Returns(new StreamInformation
            {
                StreamIdentifier = "stream-123",
                StreamType = "cosmosdb",
                CurrentStreamVersion = 5
            });
            document.TerminatedStreams.Returns([]);

            var existingEntity = new CosmosDbDocumentEntity { ETag = "etag-123" };
            var readResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            readResponse.Resource.Returns(existingEntity);
            readResponse.ETag.Returns("etag-123");

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(readResponse);

            var replaceResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            container.ReplaceItemAsync(
                Arg.Any<CosmosDbDocumentEntity>(),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(replaceResponse);

            await sut.SetAsync(document);

            await container.Received(1).ReplaceItemAsync(
                Arg.Any<CosmosDbDocumentEntity>(),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_create_document_when_not_found_during_concurrency_check()
        {
            var settingsWithConcurrency = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                DocumentsContainerName = "documents",
                AutoCreateContainers = false,
                UseOptimisticConcurrency = true
            };

            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settingsWithConcurrency);

            var document = Substitute.For<IObjectDocument>();
            document.ObjectName.Returns("TestObject");
            document.ObjectId.Returns("test-id");
            document.Hash.Returns("existing-hash");
            document.Active.Returns(new StreamInformation
            {
                StreamIdentifier = "stream-123",
                StreamType = "cosmosdb",
                CurrentStreamVersion = 5
            });
            document.TerminatedStreams.Returns([]);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            var createResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            container.CreateItemAsync(
                Arg.Any<CosmosDbDocumentEntity>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(createResponse);

            await sut.SetAsync(document);

            await container.Received(1).CreateItemAsync(
                Arg.Any<CosmosDbDocumentEntity>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_processing_exception_on_concurrency_conflict()
        {
            var settingsWithConcurrency = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                DocumentsContainerName = "documents",
                AutoCreateContainers = false,
                UseOptimisticConcurrency = true
            };

            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settingsWithConcurrency);

            var document = Substitute.For<IObjectDocument>();
            document.ObjectName.Returns("TestObject");
            document.ObjectId.Returns("test-id");
            document.Hash.Returns("existing-hash");
            document.Active.Returns(new StreamInformation
            {
                StreamIdentifier = "stream-123",
                StreamType = "cosmosdb",
                CurrentStreamVersion = 5
            });
            document.TerminatedStreams.Returns([]);

            var existingEntity = new CosmosDbDocumentEntity { ETag = "etag-123" };
            var readResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            readResponse.Resource.Returns(existingEntity);
            readResponse.ETag.Returns("etag-123");

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(readResponse);

            container.ReplaceItemAsync(
                Arg.Any<CosmosDbDocumentEntity>(),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Conflict", HttpStatusCode.PreconditionFailed, 0, "", 0));

            await Assert.ThrowsAsync<CosmosDbProcessingException>(() => sut.SetAsync(document));
        }

        [Fact]
        public async Task Should_preserve_terminated_streams()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);

            var terminatedStreams = new List<TerminatedStream>
            {
                new TerminatedStream
                {
                    StreamIdentifier = "old-stream",
                    StreamVersion = 10,
                    TerminationDate = DateTimeOffset.UtcNow.AddDays(-1),
                    Reason = "Migrated"
                }
            };

            var document = Substitute.For<IObjectDocument>();
            document.ObjectName.Returns("testobject");
            document.ObjectId.Returns("test-id");
            document.Hash.Returns((string?)null);
            document.Active.Returns(new StreamInformation
            {
                StreamIdentifier = "stream-123",
                StreamType = "cosmosdb",
                CurrentStreamVersion = 5
            });
            document.TerminatedStreams.Returns(terminatedStreams);

            CosmosDbDocumentEntity? capturedEntity = null;
            var itemResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            container.ReplaceItemAsync(
                Arg.Do<CosmosDbDocumentEntity>(e => capturedEntity = e),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            await sut.SetAsync(document);

            Assert.NotNull(capturedEntity);
            Assert.Single(capturedEntity.TerminatedStreams);
            Assert.Equal("old-stream", capturedEntity.TerminatedStreams[0].StreamIdentifier);
        }
    }

    private static readonly string[] SingleTagIds = ["test-id"];
    private static readonly string[] MultipleTagIds = ["test-id-1", "test-id-2"];

    public class GetFirstByDocumentByTagAsync : CosmosDbDocumentStoreTests
    {
        [Fact]
        public async Task Should_return_null_when_no_documents_have_tag()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);

            var tagStore = Substitute.For<IDocumentTagStore>();
            tagStore.GetAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Array.Empty<string>());
            documentTagFactory.CreateDocumentTagStore(Arg.Any<string>()).Returns(tagStore);

            var result = await sut.GetFirstByDocumentByTagAsync("TestObject", "test-tag");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_document_when_tag_matches()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);

            var tagStore = Substitute.For<IDocumentTagStore>();
            tagStore.GetAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(SingleTagIds);
            documentTagFactory.CreateDocumentTagStore(Arg.Any<string>()).Returns(tagStore);

            var existingEntity = new CosmosDbDocumentEntity
            {
                Id = "testobject_test-id",
                ObjectName = "testobject",
                ObjectId = "test-id",
                Active = new CosmosDbStreamInfo
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "cosmosdb"
                }
            };

            var itemResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            itemResponse.Resource.Returns(existingEntity);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            var result = await sut.GetFirstByDocumentByTagAsync("TestObject", "test-tag");

            Assert.NotNull(result);
            Assert.Equal("test-id", result.ObjectId);
        }
    }

    public class GetByDocumentByTagAsync : CosmosDbDocumentStoreTests
    {
        [Fact]
        public async Task Should_return_empty_when_no_documents_have_tag()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);

            var tagStore = Substitute.For<IDocumentTagStore>();
            tagStore.GetAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Array.Empty<string>());
            documentTagFactory.CreateDocumentTagStore(Arg.Any<string>()).Returns(tagStore);

            var result = await sut.GetByDocumentByTagAsync("TestObject", "test-tag");

            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_return_all_documents_with_tag()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);

            var tagStore = Substitute.For<IDocumentTagStore>();
            tagStore.GetAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(MultipleTagIds);
            documentTagFactory.CreateDocumentTagStore(Arg.Any<string>()).Returns(tagStore);

            var entity1 = new CosmosDbDocumentEntity
            {
                ObjectName = "testobject",
                ObjectId = "test-id-1",
                Active = new CosmosDbStreamInfo { StreamIdentifier = "stream-1", StreamType = "cosmosdb" }
            };

            var entity2 = new CosmosDbDocumentEntity
            {
                ObjectName = "testobject",
                ObjectId = "test-id-2",
                Active = new CosmosDbStreamInfo { StreamIdentifier = "stream-2", StreamType = "cosmosdb" }
            };

            var response1 = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            response1.Resource.Returns(entity1);

            var response2 = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            response2.Resource.Returns(entity2);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Is<string>(s => s.Contains("test-id-1")),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(response1);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Is<string>(s => s.Contains("test-id-2")),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(response2);

            var result = await sut.GetByDocumentByTagAsync("TestObject", "test-tag");

            Assert.Equal(2, result.Count());
        }
    }

    public class AutoCreateContainers : CosmosDbDocumentStoreTests
    {
        [Fact]
        public async Task Should_create_database_and_container_with_autoscale_throughput()
        {
            var autoCreateSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                DocumentsContainerName = "documents",
                AutoCreateContainers = true,
                DatabaseThroughput = new ThroughputSettings { AutoscaleMaxThroughput = 4000 },
                DocumentsThroughput = new ThroughputSettings { AutoscaleMaxThroughput = 4000 }
            };

            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, autoCreateSettings);

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

            database.GetContainer(autoCreateSettings.DocumentsContainerName).Returns(container);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            var createResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            createResponse.Resource.Returns(new CosmosDbDocumentEntity
            {
                ObjectId = "test-id",
                ObjectName = "testobject",
                Active = new CosmosDbStreamInfo { StreamIdentifier = "stream-123" }
            });
            container.CreateItemAsync(
                Arg.Any<CosmosDbDocumentEntity>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(createResponse);

            await sut.CreateAsync("TestObject", "test-id");

            await cosmosClient.Received(1).CreateDatabaseIfNotExistsAsync(
                autoCreateSettings.DatabaseName,
                Arg.Any<ThroughputProperties>(),
                Arg.Any<RequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_create_database_and_container_with_manual_throughput()
        {
            var autoCreateSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                DocumentsContainerName = "documents",
                AutoCreateContainers = true,
                DocumentsThroughput = new ThroughputSettings { ManualThroughput = 400 }
            };

            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, autoCreateSettings);

            var databaseResponse = Substitute.For<DatabaseResponse>();
            databaseResponse.Database.Returns(database);
            cosmosClient.CreateDatabaseIfNotExistsAsync(
                Arg.Any<string>(),
                Arg.Any<ThroughputProperties?>()).Returns(databaseResponse);

            var containerResponse = Substitute.For<ContainerResponse>();
            database.CreateContainerIfNotExistsAsync(
                Arg.Any<ContainerProperties>(),
                Arg.Any<ThroughputProperties>()).Returns(containerResponse);

            database.GetContainer(autoCreateSettings.DocumentsContainerName).Returns(container);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            var createResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            createResponse.Resource.Returns(new CosmosDbDocumentEntity
            {
                ObjectId = "test-id",
                ObjectName = "testobject",
                Active = new CosmosDbStreamInfo { StreamIdentifier = "stream-123" }
            });
            container.CreateItemAsync(
                Arg.Any<CosmosDbDocumentEntity>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(createResponse);

            await sut.CreateAsync("TestObject", "test-id");

            await database.Received(1).CreateContainerIfNotExistsAsync(
                Arg.Any<ContainerProperties>(),
                Arg.Any<ThroughputProperties>());
        }

        [Fact]
        public async Task Should_create_database_and_container_without_throughput()
        {
            var autoCreateSettings = new EventStreamCosmosDbSettings
            {
                DatabaseName = "test-db",
                DocumentsContainerName = "documents",
                AutoCreateContainers = true
                // No throughput settings
            };

            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, autoCreateSettings);

            var databaseResponse = Substitute.For<DatabaseResponse>();
            databaseResponse.Database.Returns(database);
            cosmosClient.CreateDatabaseIfNotExistsAsync(
                Arg.Any<string>(),
                Arg.Any<ThroughputProperties?>()).Returns(databaseResponse);

            var containerResponse = Substitute.For<ContainerResponse>();
            database.CreateContainerIfNotExistsAsync(
                Arg.Any<ContainerProperties>()).Returns(containerResponse);

            database.GetContainer(autoCreateSettings.DocumentsContainerName).Returns(container);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

            var createResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            createResponse.Resource.Returns(new CosmosDbDocumentEntity
            {
                ObjectId = "test-id",
                ObjectName = "testobject",
                Active = new CosmosDbStreamInfo { StreamIdentifier = "stream-123" }
            });
            container.CreateItemAsync(
                Arg.Any<CosmosDbDocumentEntity>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(createResponse);

            await sut.CreateAsync("TestObject", "test-id");

            await database.Received(1).CreateContainerIfNotExistsAsync(Arg.Any<ContainerProperties>());
        }
    }

    public class ContainerCaching : CosmosDbDocumentStoreTests
    {
        [Fact]
        public async Task Should_reuse_container_on_subsequent_calls()
        {
            var sut = new CosmosDbDocumentStore(cosmosClient, documentTagFactory, settings);

            var existingEntity = new CosmosDbDocumentEntity
            {
                ObjectId = "test-id",
                ObjectName = "testobject",
                Active = new CosmosDbStreamInfo { StreamIdentifier = "stream-123" }
            };

            var itemResponse = Substitute.For<ItemResponse<CosmosDbDocumentEntity>>();
            itemResponse.Resource.Returns(existingEntity);

            container.ReadItemAsync<CosmosDbDocumentEntity>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>()).Returns(itemResponse);

            await sut.GetAsync("TestObject", "test-id-1");
            await sut.GetAsync("TestObject", "test-id-2");

            database.Received(1).GetContainer(settings.DocumentsContainerName);
        }
    }
}
