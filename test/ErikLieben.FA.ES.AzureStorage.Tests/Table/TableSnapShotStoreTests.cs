using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.AzureStorage.Table.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using Microsoft.Extensions.Azure;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableSnapShotStoreTests
{
    protected readonly IAzureClientFactory<TableServiceClient> ClientFactory;
    protected readonly TableServiceClient TableServiceClient;
    protected readonly TableClient SnapshotTableClient;
    protected readonly EventStreamTableSettings TableSettings;
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public TableSnapShotStoreTests()
    {
        ClientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
        TableServiceClient = Substitute.For<TableServiceClient>();
        SnapshotTableClient = Substitute.For<TableClient>();
        TableSettings = new EventStreamTableSettings("test-connection");

        ClientFactory.CreateClient(Arg.Any<string>()).Returns(TableServiceClient);
        TableServiceClient.GetTableClient(TableSettings.DefaultSnapshotTableName).Returns(SnapshotTableClient);
    }

    protected TableSnapShotStore CreateSut() => new(ClientFactory, TableSettings);

    protected static IObjectDocument CreateMockDocument(string objectName, string objectId, string? snapshotStore = null)
    {
        var document = Substitute.For<IObjectDocument>();
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
            SnapShotStore = snapshotStore ?? "test-connection"
        };

        document.Active.Returns(streamInfo);
        document.ObjectName.Returns(objectName);
        document.ObjectId.Returns(objectId);
        document.TerminatedStreams.Returns(new List<TerminatedStream>());

        return document;
    }

    // Test aggregate for snapshots
    public class TestAggregate : IBase
    {
        public string Name { get; set; } = "Test";
        public int Value { get; set; } = 42;

        public Task Fold() => Task.CompletedTask;
        public void Fold(IEvent @event) { }
        public void ProcessSnapshot(object snapshot) { }
    }

    protected static TestAggregate CreateTestAggregate() => new();

    protected static JsonTypeInfo GetJsonTypeInfo() => JsonOptions.GetTypeInfo(typeof(TestAggregate));

    public class SetAsyncMethod : TableSnapShotStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            // Arrange
            var sut = CreateSut();
            var document = Substitute.For<IObjectDocument>();
            document.ObjectName.Returns((string?)null);
            var aggregate = CreateTestAggregate();
            var jsonTypeInfo = GetJsonTypeInfo();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SetAsync(aggregate, jsonTypeInfo, document, 1));
        }

        [Fact]
        public async Task Should_upsert_entity_to_table()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var aggregate = CreateTestAggregate();
            var jsonTypeInfo = GetJsonTypeInfo();

            // Act
            await sut.SetAsync(aggregate, jsonTypeInfo, document, 1);

            // Assert
            await SnapshotTableClient.Received(1).UpsertEntityAsync(
                Arg.Is<TableSnapshotEntity>(e =>
                    e.Version == 1 &&
                    e.StreamIdentifier == document.Active.StreamIdentifier),
                TableUpdateMode.Replace,
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_format_row_key_with_padded_version()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var aggregate = CreateTestAggregate();
            var jsonTypeInfo = GetJsonTypeInfo();

            TableSnapshotEntity? capturedEntity = null;
            SnapshotTableClient.UpsertEntityAsync(
                Arg.Do<TableSnapshotEntity>(e => capturedEntity = e),
                Arg.Any<TableUpdateMode>(),
                Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response>());

            // Act
            await sut.SetAsync(aggregate, jsonTypeInfo, document, 12345);

            // Assert
            Assert.NotNull(capturedEntity);
            Assert.Equal("00000000000000012345", capturedEntity.RowKey);
        }

        [Fact]
        public async Task Should_include_name_in_row_key_when_provided()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var aggregate = CreateTestAggregate();
            var jsonTypeInfo = GetJsonTypeInfo();

            TableSnapshotEntity? capturedEntity = null;
            SnapshotTableClient.UpsertEntityAsync(
                Arg.Do<TableSnapshotEntity>(e => capturedEntity = e),
                Arg.Any<TableUpdateMode>(),
                Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response>());

            // Act
            await sut.SetAsync(aggregate, jsonTypeInfo, document, 100, "v2");

            // Assert
            Assert.NotNull(capturedEntity);
            Assert.Equal("00000000000000000100_v2", capturedEntity.RowKey);
        }

        [Fact]
        public async Task Should_format_partition_key_correctly()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var aggregate = CreateTestAggregate();
            var jsonTypeInfo = GetJsonTypeInfo();

            TableSnapshotEntity? capturedEntity = null;
            SnapshotTableClient.UpsertEntityAsync(
                Arg.Do<TableSnapshotEntity>(e => capturedEntity = e),
                Arg.Any<TableUpdateMode>(),
                Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response>());

            // Act
            await sut.SetAsync(aggregate, jsonTypeInfo, document, 1);

            // Assert
            Assert.NotNull(capturedEntity);
            Assert.StartsWith("testobject_", capturedEntity.PartitionKey);
        }

        [Fact]
        public async Task Should_throw_table_not_found_exception_when_table_missing()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var aggregate = CreateTestAggregate();
            var jsonTypeInfo = GetJsonTypeInfo();

            SnapshotTableClient.UpsertEntityAsync(
                Arg.Any<TableSnapshotEntity>(),
                Arg.Any<TableUpdateMode>(),
                Arg.Any<CancellationToken>())
                .Throws(new RequestFailedException(404, "Table not found"));

            // Act & Assert
            await Assert.ThrowsAsync<TableDocumentStoreTableNotFoundException>(() =>
                sut.SetAsync(aggregate, jsonTypeInfo, document, 1));
        }

        [Fact]
        public async Task Should_auto_create_table_when_enabled()
        {
            // Arrange
            var autoCreateSettings = new EventStreamTableSettings("test-connection") { AutoCreateTable = true };
            var sut = new TableSnapShotStore(ClientFactory, autoCreateSettings);
            var document = CreateMockDocument("TestObject", "test-id");
            var aggregate = CreateTestAggregate();
            var jsonTypeInfo = GetJsonTypeInfo();

            // Act
            await sut.SetAsync(aggregate, jsonTypeInfo, document, 1);

            // Assert
            await SnapshotTableClient.Received(1).CreateIfNotExistsAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_serialize_aggregate_data()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var aggregate = new TestAggregate { Name = "MyAggregate", Value = 100 };
            var jsonTypeInfo = GetJsonTypeInfo();

            TableSnapshotEntity? capturedEntity = null;
            SnapshotTableClient.UpsertEntityAsync(
                Arg.Do<TableSnapshotEntity>(e => capturedEntity = e),
                Arg.Any<TableUpdateMode>(),
                Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response>());

            // Act
            await sut.SetAsync(aggregate, jsonTypeInfo, document, 1);

            // Assert
            Assert.NotNull(capturedEntity);
            Assert.Contains("MyAggregate", capturedEntity.Data);
            Assert.Contains("100", capturedEntity.Data);
        }

        [Fact]
        public async Task Should_store_aggregate_type_name()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var aggregate = CreateTestAggregate();
            var jsonTypeInfo = GetJsonTypeInfo();

            TableSnapshotEntity? capturedEntity = null;
            SnapshotTableClient.UpsertEntityAsync(
                Arg.Do<TableSnapshotEntity>(e => capturedEntity = e),
                Arg.Any<TableUpdateMode>(),
                Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response>());

            // Act
            await sut.SetAsync(aggregate, jsonTypeInfo, document, 1);

            // Assert
            Assert.NotNull(capturedEntity);
            Assert.Contains("TestAggregate", capturedEntity.AggregateType);
        }
    }

    public class GetAsyncGenericMethod : TableSnapShotStoreTests
    {
        [Fact]
        public async Task Should_return_null_when_table_not_found()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var jsonTypeInfo = GetJsonTypeInfo();

            SnapshotTableClient.GetEntityIfExistsAsync<TableSnapshotEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Throws(new RequestFailedException(404, "Table not found"));

            // Act
            var result = await sut.GetAsync(jsonTypeInfo, document, 1);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Should_query_with_correct_partition_and_row_key()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var jsonTypeInfo = GetJsonTypeInfo();

            string? capturedPartitionKey = null;
            string? capturedRowKey = null;
            var response = Substitute.For<NullableResponse<TableSnapshotEntity>>();
            response.HasValue.Returns(false);
            SnapshotTableClient.GetEntityIfExistsAsync<TableSnapshotEntity>(
                Arg.Do<string>(pk => capturedPartitionKey = pk),
                Arg.Do<string>(rk => capturedRowKey = rk),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<CancellationToken>())
                .Returns(response);

            // Act
            await sut.GetAsync(jsonTypeInfo, document, 42);

            // Assert
            Assert.NotNull(capturedPartitionKey);
            Assert.NotNull(capturedRowKey);
            Assert.StartsWith("testobject_", capturedPartitionKey);
            Assert.Equal("00000000000000000042", capturedRowKey);
        }

        [Fact]
        public async Task Should_include_name_in_row_key_query_when_provided()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var jsonTypeInfo = GetJsonTypeInfo();

            string? capturedRowKey = null;
            var response = Substitute.For<NullableResponse<TableSnapshotEntity>>();
            response.HasValue.Returns(false);
            SnapshotTableClient.GetEntityIfExistsAsync<TableSnapshotEntity>(
                Arg.Any<string>(),
                Arg.Do<string>(rk => capturedRowKey = rk),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<CancellationToken>())
                .Returns(response);

            // Act
            await sut.GetAsync(jsonTypeInfo, document, 100, "v2");

            // Assert
            Assert.Equal("00000000000000000100_v2", capturedRowKey);
        }

        [Fact]
        public async Task Should_use_custom_snapshot_store_connection()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id", snapshotStore: "custom-snapshot-store");
            var jsonTypeInfo = GetJsonTypeInfo();

            var customServiceClient = Substitute.For<TableServiceClient>();
            var customTableClient = Substitute.For<TableClient>();
            ClientFactory.CreateClient("custom-snapshot-store").Returns(customServiceClient);
            customServiceClient.GetTableClient(Arg.Any<string>()).Returns(customTableClient);

            var response = Substitute.For<NullableResponse<TableSnapshotEntity>>();
            response.HasValue.Returns(false);
            customTableClient.GetEntityIfExistsAsync<TableSnapshotEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(response);

            // Act
            await sut.GetAsync(jsonTypeInfo, document, 1);

            // Assert
            ClientFactory.Received(1).CreateClient("custom-snapshot-store");
        }

        [Fact]
        public async Task Should_return_null_when_entity_not_found()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var jsonTypeInfo = GetJsonTypeInfo();

            var response = Substitute.For<NullableResponse<TableSnapshotEntity>>();
            response.HasValue.Returns(false);
            SnapshotTableClient.GetEntityIfExistsAsync<TableSnapshotEntity>(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(response);

            // Act
            var result = await sut.GetAsync(jsonTypeInfo, document, 1);

            // Assert
            Assert.Null(result);
        }
    }
}
