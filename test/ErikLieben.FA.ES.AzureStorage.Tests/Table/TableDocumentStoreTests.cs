#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8603 // Possible null reference return - test context

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.AzureStorage.Table.Model;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableDocumentStoreTests
{
    protected readonly IAzureClientFactory<TableServiceClient> ClientFactory;
    protected readonly TableServiceClient TableServiceClient;
    protected readonly TableClient DocumentTableClient;
    protected readonly TableClient ChunkTableClient;
    protected readonly TableClient SnapShotTableClient;
    protected readonly TableClient TerminatedStreamTableClient;
    protected readonly IDocumentTagDocumentFactory DocumentTagStoreFactory;
    protected readonly IDocumentTagStore DocumentTagStore;
    protected readonly EventStreamTableSettings TableSettings;
    protected readonly EventStreamDefaultTypeSettings TypeSettings;

    public TableDocumentStoreTests()
    {
        ClientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
        TableServiceClient = Substitute.For<TableServiceClient>();
        DocumentTableClient = Substitute.For<TableClient>();
        ChunkTableClient = Substitute.For<TableClient>();
        SnapShotTableClient = Substitute.For<TableClient>();
        TerminatedStreamTableClient = Substitute.For<TableClient>();
        DocumentTagStoreFactory = Substitute.For<IDocumentTagDocumentFactory>();
        DocumentTagStore = Substitute.For<IDocumentTagStore>();
        TableSettings = new EventStreamTableSettings("test-connection");
        TypeSettings = new EventStreamDefaultTypeSettings("table");

        // Setup table client chain
        ClientFactory.CreateClient(Arg.Any<string>()).Returns(TableServiceClient);
        TableServiceClient.GetTableClient(TableSettings.DefaultDocumentTableName).Returns(DocumentTableClient);
        TableServiceClient.GetTableClient(TableSettings.DefaultStreamChunkTableName).Returns(ChunkTableClient);
        TableServiceClient.GetTableClient(TableSettings.DefaultDocumentSnapShotTableName).Returns(SnapShotTableClient);
        TableServiceClient.GetTableClient(TableSettings.DefaultTerminatedStreamTableName).Returns(TerminatedStreamTableClient);

        // Setup empty async enumerables for chunk/snapshot/terminated queries
        SetupEmptyQueries();
    }

    protected void SetupEmptyQueries()
    {
        ChunkTableClient.QueryAsync<TableStreamChunkEntity>(
            Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncPageable<TableStreamChunkEntity>.FromPages(Array.Empty<Page<TableStreamChunkEntity>>()));

        SnapShotTableClient.QueryAsync<TableDocumentSnapShotEntity>(
            Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncPageable<TableDocumentSnapShotEntity>.FromPages(Array.Empty<Page<TableDocumentSnapShotEntity>>()));

        TerminatedStreamTableClient.QueryAsync<TableTerminatedStreamEntity>(
            Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(AsyncPageable<TableTerminatedStreamEntity>.FromPages(Array.Empty<Page<TableTerminatedStreamEntity>>()));
    }

    protected TableDocumentStore CreateSut() =>
        new(ClientFactory, DocumentTagStoreFactory, TableSettings, TypeSettings);

    public class Constructor : TableDocumentStoreTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableDocumentStore(null!, DocumentTagStoreFactory, TableSettings, TypeSettings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_document_tag_store_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableDocumentStore(ClientFactory, null!, TableSettings, TypeSettings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_table_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableDocumentStore(ClientFactory, DocumentTagStoreFactory, null!, TypeSettings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_type_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableDocumentStore(ClientFactory, DocumentTagStoreFactory, TableSettings, null!));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            var sut = CreateSut();
            Assert.NotNull(sut);
        }
    }

    public class CreateAsyncMethod : TableDocumentStoreTests
    {
        [Fact]
        public async Task Should_create_new_document_when_not_exists()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "test-id-123";
            var sut = CreateSut();

            // Setup: Entity doesn't exist initially
            var notExistsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            notExistsResponse.HasValue.Returns(false);
            DocumentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                objectName.ToLowerInvariant(), objectId, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(notExistsResponse);

            // Setup: Return entity after creation
            var createdEntity = CreateTableDocumentEntity(objectName, objectId);
            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                objectName.ToLowerInvariant(), objectId, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(createdEntity, Substitute.For<Response>()));

            // Act
            var result = await sut.CreateAsync(objectName, objectId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(objectId, result.ObjectId);
            Assert.Equal(objectName, result.ObjectName);
            await DocumentTableClient.Received(1).AddEntityAsync(Arg.Any<TableDocumentEntity>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_existing_document_when_already_exists()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "test-id-123";
            var sut = CreateSut();

            var existingEntity = CreateTableDocumentEntity(objectName, objectId);

            // Setup: Entity already exists
            var existsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            existsResponse.HasValue.Returns(true);
            existsResponse.Value.Returns(existingEntity);
            DocumentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                objectName.ToLowerInvariant(), objectId, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(existsResponse);

            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                objectName.ToLowerInvariant(), objectId, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(existingEntity, Substitute.For<Response>()));

            // Act
            var result = await sut.CreateAsync(objectName, objectId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(objectId, result.ObjectId);
            await DocumentTableClient.DidNotReceive().AddEntityAsync(Arg.Any<TableDocumentEntity>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_use_lowercase_object_name_as_partition_key()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "test-id-123";
            var sut = CreateSut();

            var notExistsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            notExistsResponse.HasValue.Returns(false);
            DocumentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(notExistsResponse);

            var createdEntity = CreateTableDocumentEntity(objectName, objectId);
            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(createdEntity, Substitute.For<Response>()));

            // Act
            await sut.CreateAsync(objectName, objectId);

            // Assert
            await DocumentTableClient.Received(1).GetEntityIfExistsAsync<TableDocumentEntity>(
                objectName.ToLowerInvariant(), objectId, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_use_custom_store_when_provided()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "test-id-123";
            var customStore = "custom-store";
            var sut = CreateSut();

            var customTableClient = Substitute.For<TableClient>();
            TableServiceClient.GetTableClient(TableSettings.DefaultDocumentTableName).Returns(customTableClient);

            var notExistsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            notExistsResponse.HasValue.Returns(false);
            customTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(notExistsResponse);

            var createdEntity = CreateTableDocumentEntity(objectName, objectId);
            customTableClient.GetEntityAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(createdEntity, Substitute.For<Response>()));

            // Act
            await sut.CreateAsync(objectName, objectId, customStore);

            // Assert
            ClientFactory.Received().CreateClient(customStore);
        }

        [Fact]
        public async Task Should_throw_table_not_found_exception_when_table_does_not_exist()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "test-id-123";
            var sut = CreateSut();

            DocumentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Throws(new RequestFailedException(404, "Table not found"));

            // Act & Assert
            await Assert.ThrowsAsync<TableDocumentStoreTableNotFoundException>(() =>
                sut.CreateAsync(objectName, objectId));
        }

        [Fact]
        public async Task Should_initialize_stream_version_to_minus_one()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "test-id-123";
            var sut = CreateSut();

            TableDocumentEntity? capturedEntity = null;
            var notExistsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            notExistsResponse.HasValue.Returns(false);
            DocumentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(notExistsResponse);

            DocumentTableClient.AddEntityAsync(Arg.Do<TableDocumentEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response>());

            var createdEntity = CreateTableDocumentEntity(objectName, objectId);
            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(createdEntity, Substitute.For<Response>()));

            // Act
            await sut.CreateAsync(objectName, objectId);

            // Assert
            Assert.NotNull(capturedEntity);
            Assert.Equal(-1, capturedEntity.ActiveCurrentStreamVersion);
        }
    }

    public class GetAsyncMethod : TableDocumentStoreTests
    {
        [Fact]
        public async Task Should_return_document_when_exists()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "test-id-123";
            var sut = CreateSut();

            var entity = CreateTableDocumentEntity(objectName, objectId);
            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                objectName.ToLowerInvariant(), objectId, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(entity, Substitute.For<Response>()));

            // Act
            var result = await sut.GetAsync(objectName, objectId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(objectId, result.ObjectId);
            Assert.Equal(objectName, result.ObjectName);
        }

        [Fact]
        public async Task Should_use_lowercase_object_name_as_partition_key()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "test-id-123";
            var sut = CreateSut();

            var entity = CreateTableDocumentEntity(objectName, objectId);
            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(entity, Substitute.For<Response>()));

            // Act
            await sut.GetAsync(objectName, objectId);

            // Assert
            await DocumentTableClient.Received(1).GetEntityAsync<TableDocumentEntity>(
                objectName.ToLowerInvariant(), objectId, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_document_not_found_exception_when_document_does_not_exist()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "non-existent-id";
            var sut = CreateSut();

            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Throws(new RequestFailedException(404, "Entity not found"));

            // Act & Assert
            await Assert.ThrowsAsync<TableDocumentNotFoundException>(() =>
                sut.GetAsync(objectName, objectId));
        }

        [Fact]
        public async Task Should_throw_table_not_found_exception_when_table_does_not_exist()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "test-id-123";
            var sut = CreateSut();

            var ex = new RequestFailedException(404, "Table not found", "TableNotFound", null);
            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Throws(ex);

            // Act & Assert
            await Assert.ThrowsAsync<TableDocumentStoreTableNotFoundException>(() =>
                sut.GetAsync(objectName, objectId));
        }

        [Fact]
        public async Task Should_populate_stream_information_from_entity()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "test-id-123";
            var sut = CreateSut();

            var entity = CreateTableDocumentEntity(objectName, objectId);
            entity.ActiveCurrentStreamVersion = 5;
            entity.ActiveStreamType = "table";
            entity.ActiveDataStore = "custom-store";

            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(entity, Substitute.For<Response>()));

            // Act
            var result = await sut.GetAsync(objectName, objectId);

            // Assert
            Assert.Equal(5, result.Active.CurrentStreamVersion);
            Assert.Equal("table", result.Active.StreamType);
            Assert.Equal("custom-store", result.Active.DataStore);
        }
    }

    public class SetAsyncMethod : TableDocumentStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = CreateSut();
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(null!));
        }

        [Fact]
        public async Task Should_update_existing_document()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id-123");

            var existingEntity = CreateTableDocumentEntity("TestObject", "test-id-123");
            existingEntity.ETag = new ETag("existing-etag");

            var existsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            existsResponse.HasValue.Returns(true);
            existsResponse.Value.Returns(existingEntity);
            DocumentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(existsResponse);

            // Act
            await sut.SetAsync(document);

            // Assert
            await DocumentTableClient.Received(1).UpdateEntityAsync(
                Arg.Any<TableDocumentEntity>(), Arg.Any<ETag>(), TableUpdateMode.Replace, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_add_new_document_when_not_exists()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id-123");

            var notExistsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            notExistsResponse.HasValue.Returns(false);
            DocumentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(notExistsResponse);

            // Act
            await sut.SetAsync(document);

            // Assert
            await DocumentTableClient.Received(1).AddEntityAsync(
                Arg.Any<TableDocumentEntity>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_processing_exception_on_concurrency_conflict()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id-123");

            var existingEntity = CreateTableDocumentEntity("TestObject", "test-id-123");
            var existsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            existsResponse.HasValue.Returns(true);
            existsResponse.Value.Returns(existingEntity);
            DocumentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(existsResponse);

            DocumentTableClient.UpdateEntityAsync(
                Arg.Any<TableDocumentEntity>(), Arg.Any<ETag>(), Arg.Any<TableUpdateMode>(), Arg.Any<CancellationToken>())
                .Throws(new RequestFailedException(412, "Precondition failed"));

            // Act & Assert
            await Assert.ThrowsAsync<TableDataStoreProcessingException>(() =>
                sut.SetAsync(document));
        }

        [Fact]
        public async Task Should_save_stream_chunks()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id-123");
            document.Active.StreamChunks.Add(new StreamChunk(0, 0, 10));
            document.Active.StreamChunks.Add(new StreamChunk(1, 11, 20));

            var notExistsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            notExistsResponse.HasValue.Returns(false);
            DocumentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(notExistsResponse);

            // Act
            await sut.SetAsync(document);

            // Assert
            await ChunkTableClient.Received(2).UpsertEntityAsync(
                Arg.Any<TableStreamChunkEntity>(), TableUpdateMode.Replace, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_save_snapshots()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id-123");
            document.Active.SnapShots.Add(new StreamSnapShot { UntilVersion = 10, Name = "snapshot1" });

            var notExistsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            notExistsResponse.HasValue.Returns(false);
            DocumentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(notExistsResponse);

            // Act
            await sut.SetAsync(document);

            // Assert
            await SnapShotTableClient.Received(1).UpsertEntityAsync(
                Arg.Any<TableDocumentSnapShotEntity>(), TableUpdateMode.Replace, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_update_document_hash()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id-123");

            var notExistsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            notExistsResponse.HasValue.Returns(false);
            DocumentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(notExistsResponse);

            // Act
            await sut.SetAsync(document);

            // Assert
            Assert.NotNull(document.Hash);
            Assert.NotEmpty(document.Hash);
        }
    }

    public class GetFirstByDocumentByTagAsyncMethod : TableDocumentStoreTests
    {
        [Fact]
        public async Task Should_return_document_when_tag_found()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "test-id-123";
            var tag = "test-tag";
            var sut = CreateSut();

            DocumentTagStoreFactory.CreateDocumentTagStore(Arg.Any<string>()).Returns(DocumentTagStore);
            DocumentTagStore.GetAsync(objectName, tag).Returns(new[] { objectId });

            var entity = CreateTableDocumentEntity(objectName, objectId);
            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(entity, Substitute.For<Response>()));

            // Act
            var result = await sut.GetFirstByDocumentByTagAsync(objectName, tag);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(objectId, result.ObjectId);
        }

        [Fact]
        public async Task Should_return_null_when_tag_not_found()
        {
            // Arrange
            var objectName = "TestObject";
            var tag = "non-existent-tag";
            var sut = CreateSut();

            DocumentTagStoreFactory.CreateDocumentTagStore(Arg.Any<string>()).Returns(DocumentTagStore);
            DocumentTagStore.GetAsync(objectName, tag).Returns(Array.Empty<string>());

            // Act
            var result = await sut.GetFirstByDocumentByTagAsync(objectName, tag);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Should_use_custom_tag_store_when_provided()
        {
            // Arrange
            var objectName = "TestObject";
            var tag = "test-tag";
            var customTagStore = "custom-tag-store";
            var sut = CreateSut();

            DocumentTagStoreFactory.CreateDocumentTagStore(customTagStore).Returns(DocumentTagStore);
            DocumentTagStore.GetAsync(objectName, tag).Returns(Array.Empty<string>());

            // Act
            await sut.GetFirstByDocumentByTagAsync(objectName, tag, customTagStore);

            // Assert
            DocumentTagStoreFactory.Received(1).CreateDocumentTagStore(customTagStore);
        }
    }

    public class GetByDocumentByTagAsyncMethod : TableDocumentStoreTests
    {
        [Fact]
        public async Task Should_return_all_documents_matching_tag()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId1 = "test-id-1";
            var objectId2 = "test-id-2";
            var tag = "test-tag";
            var sut = CreateSut();

            DocumentTagStoreFactory.CreateDocumentTagStore(Arg.Any<string>()).Returns(DocumentTagStore);
            DocumentTagStore.GetAsync(objectName, tag).Returns(new[] { objectId1, objectId2 });

            var entity1 = CreateTableDocumentEntity(objectName, objectId1);
            var entity2 = CreateTableDocumentEntity(objectName, objectId2);

            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                objectName.ToLowerInvariant(), objectId1, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(entity1, Substitute.For<Response>()));

            DocumentTableClient.GetEntityAsync<TableDocumentEntity>(
                objectName.ToLowerInvariant(), objectId2, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(entity2, Substitute.For<Response>()));

            // Act
            var result = (await sut.GetByDocumentByTagAsync(objectName, tag)).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, d => d.ObjectId == objectId1);
            Assert.Contains(result, d => d.ObjectId == objectId2);
        }

        [Fact]
        public async Task Should_return_empty_collection_when_no_documents_match()
        {
            // Arrange
            var objectName = "TestObject";
            var tag = "non-existent-tag";
            var sut = CreateSut();

            DocumentTagStoreFactory.CreateDocumentTagStore(Arg.Any<string>()).Returns(DocumentTagStore);
            DocumentTagStore.GetAsync(objectName, tag).Returns(Array.Empty<string>());

            // Act
            var result = await sut.GetByDocumentByTagAsync(objectName, tag);

            // Assert
            Assert.Empty(result);
        }
    }

    #region Helper Methods

    protected static TableDocumentEntity CreateTableDocumentEntity(string objectName, string objectId)
    {
        return new TableDocumentEntity
        {
            PartitionKey = objectName.ToLowerInvariant(),
            RowKey = objectId,
            ObjectId = objectId,
            ObjectName = objectName,
            ActiveStreamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000",
            ActiveStreamType = "table",
            ActiveDocumentTagType = "table",
            ActiveCurrentStreamVersion = -1,
            ActiveDocumentType = "table",
            ActiveEventStreamTagType = "table",
            ActiveDocumentRefType = "table",
            ActiveDataStore = "test-connection",
            ActiveDocumentStore = "test-connection",
            ActiveDocumentTagStore = "test-connection",
            ActiveStreamTagStore = "test-connection",
            ActiveSnapShotStore = "test-connection",
            ActiveChunkingEnabled = false,
            ActiveChunkSize = 1000,
            SchemaVersion = "1.0.0",
            ETag = new ETag("test-etag")
        };
    }

    protected static TestObjectDocument CreateMockDocument(string objectName, string objectId)
    {
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000",
            StreamType = "table",
            DocumentTagType = "table",
            CurrentStreamVersion = 0,
            DocumentType = "table",
            EventStreamTagType = "table",
            DocumentRefType = "table",
            DataStore = "test-connection",
            DocumentStore = "test-connection",
            DocumentTagStore = "test-connection",
            StreamTagStore = "test-connection",
            SnapShotStore = "test-connection"
        };

        return new TestObjectDocument(objectId, objectName, streamInfo, new List<TerminatedStream>());
    }

    // Test object document that extends the abstract base class
    protected class TestObjectDocument : ObjectDocument
    {
        public TestObjectDocument(
            string objectId,
            string objectName,
            StreamInformation active,
            IEnumerable<TerminatedStream> terminatedStreams,
            string? schemaVersion = null,
            string? hash = null,
            string? prevHash = null)
            : base(objectId, objectName, active, terminatedStreams, schemaVersion, hash, prevHash)
        {
        }
    }

    #endregion
}
