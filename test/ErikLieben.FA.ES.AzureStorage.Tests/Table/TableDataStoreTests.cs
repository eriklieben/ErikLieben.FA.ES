#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

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
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using Microsoft.Extensions.Azure;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableDataStoreTests
{
    protected readonly IAzureClientFactory<TableServiceClient> ClientFactory;
    protected readonly TableServiceClient TableServiceClient;
    protected readonly TableClient EventTableClient;
    protected readonly EventStreamTableSettings TableSettings;

    public TableDataStoreTests()
    {
        ClientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
        TableServiceClient = Substitute.For<TableServiceClient>();
        EventTableClient = Substitute.For<TableClient>();
        TableSettings = new EventStreamTableSettings("test-connection");

        ClientFactory.CreateClient(Arg.Any<string>()).Returns(TableServiceClient);
        TableServiceClient.GetTableClient(TableSettings.DefaultEventTableName).Returns(EventTableClient);
    }

    protected TableDataStore CreateSut() => new(ClientFactory, TableSettings);

    protected static TestObjectDocument CreateMockDocument(string objectName, string objectId, int currentVersion = 0, bool chunkingEnabled = false, string? dataStore = null)
    {
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000",
            StreamType = "table",
            DocumentTagType = "table",
            CurrentStreamVersion = currentVersion,
            DocumentType = "table",
            EventStreamTagType = "table",
            DocumentRefType = "table",
            DataStore = dataStore ?? "test-connection",
            DocumentStore = "test-connection",
            DocumentTagStore = "test-connection",
            StreamTagStore = "test-connection",
            SnapShotStore = "test-connection"
        };

        if (chunkingEnabled)
        {
            streamInfo.ChunkSettings = new StreamChunkSettings { ChunkSize = 100, EnableChunks = true };
            streamInfo.StreamChunks.Add(new StreamChunk(0, 0, 99));
        }

        return new TestObjectDocument(objectId, objectName, streamInfo, new List<TerminatedStream>());
    }

    protected static TableEventEntity CreateEventEntity(string streamIdentifier, int version, string eventType = "TestEvent")
    {
        return new TableEventEntity
        {
            PartitionKey = streamIdentifier,
            RowKey = $"{version:d20}",
            ObjectId = "test-id",
            StreamIdentifier = streamIdentifier,
            EventVersion = version,
            EventType = eventType,
            SchemaVersion = 1,
            Payload = "{}",
            LastObjectDocumentHash = "*"
        };
    }

    protected static TableJsonEvent CreateTableJsonEvent(int version, string eventType = "TestEvent")
    {
        return new TableJsonEvent
        {
            EventVersion = version,
            EventType = eventType,
            SchemaVersion = 1,
            Payload = "{}",
            ActionMetadata = new ActionMetadata(),
            Metadata = new Dictionary<string, string>()
        };
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

        public void SetTestHash(string hash)
        {
            SetHash(hash);
        }
    }

    public class Constructor : TableDataStoreTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableDataStore(null!, TableSettings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableDataStore(ClientFactory, null!));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            var sut = CreateSut();
            Assert.NotNull(sut);
        }
    }

    public class ReadAsyncMethod : TableDataStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = CreateSut();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ReadAsync(null!));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_stream_identifier_is_null()
        {
            var sut = CreateSut();
            var document = Substitute.For<IObjectDocument>();
            var streamInfo = new StreamInformation { StreamIdentifier = null! };
            document.Active.Returns(streamInfo);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ReadAsync(document));
        }

        [Fact]
        public async Task Should_return_null_when_no_events_found()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");

            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(Array.Empty<Page<TableEventEntity>>()));

            // Act
            var result = await sut.ReadAsync(document);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_null_when_table_not_found()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");

            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(CreateThrowingAsyncPageable<TableEventEntity>(new RequestFailedException(404, "Table not found")));

            // Act
            var result = await sut.ReadAsync(document);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_events_ordered_by_version()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var streamIdentifier = document.Active.StreamIdentifier;

            var events = new List<TableEventEntity>
            {
                CreateEventEntity(streamIdentifier, 2),
                CreateEventEntity(streamIdentifier, 0),
                CreateEventEntity(streamIdentifier, 1)
            };

            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(CreateAsyncPageable(events));

            // Act
            var result = (await sut.ReadAsync(document))?.ToList();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(0, result[0].EventVersion);
            Assert.Equal(1, result[1].EventVersion);
            Assert.Equal(2, result[2].EventVersion);
        }

        [Fact]
        public async Task Should_apply_start_version_filter()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");

            string? capturedFilter = null;
            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Do<string>(f => capturedFilter = f), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(Array.Empty<Page<TableEventEntity>>()));

            // Act
            await sut.ReadAsync(document, startVersion: 5);

            // Assert
            Assert.NotNull(capturedFilter);
            Assert.Contains("RowKey ge '00000000000000000005'", capturedFilter);
        }

        [Fact]
        public async Task Should_apply_until_version_filter()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");

            string? capturedFilter = null;
            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Do<string>(f => capturedFilter = f), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(Array.Empty<Page<TableEventEntity>>()));

            // Act
            await sut.ReadAsync(document, startVersion: 0, untilVersion: 10);

            // Assert
            Assert.NotNull(capturedFilter);
            Assert.Contains("RowKey le '00000000000000000010'", capturedFilter);
        }

        [Fact]
        public async Task Should_use_chunked_partition_key_when_chunk_specified()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id", chunkingEnabled: true);
            var streamIdentifier = document.Active.StreamIdentifier;

            string? capturedFilter = null;
            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Do<string>(f => capturedFilter = f), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(Array.Empty<Page<TableEventEntity>>()));

            // Act
            await sut.ReadAsync(document, chunk: 5);

            // Assert
            Assert.NotNull(capturedFilter);
            Assert.Contains($"{streamIdentifier}_0000000005", capturedFilter);
        }

        [Fact]
        public async Task Should_use_custom_data_store_connection()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id", dataStore: "custom-data-store");

            var customServiceClient = Substitute.For<TableServiceClient>();
            var customTableClient = Substitute.For<TableClient>();
            ClientFactory.CreateClient("custom-data-store").Returns(customServiceClient);
            customServiceClient.GetTableClient(Arg.Any<string>()).Returns(customTableClient);

            customTableClient.QueryAsync<TableEventEntity>(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(Array.Empty<Page<TableEventEntity>>()));

            // Act
            await sut.ReadAsync(document);

            // Assert
            ClientFactory.Received(1).CreateClient("custom-data-store");
        }

        [Fact]
        public async Task Should_auto_create_table_when_enabled()
        {
            // Arrange
            var autoCreateSettings = new EventStreamTableSettings("test-connection") { AutoCreateTable = true };
            var sut = new TableDataStore(ClientFactory, autoCreateSettings);
            var document = CreateMockDocument("TestObject", "test-id");

            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(Array.Empty<Page<TableEventEntity>>()));

            // Act
            await sut.ReadAsync(document);

            // Assert
            await EventTableClient.Received(1).CreateIfNotExistsAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_use_simple_partition_key_when_no_chunk()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var streamIdentifier = document.Active.StreamIdentifier;

            string? capturedFilter = null;
            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Do<string>(f => capturedFilter = f), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(Array.Empty<Page<TableEventEntity>>()));

            // Act
            await sut.ReadAsync(document);

            // Assert
            Assert.NotNull(capturedFilter);
            Assert.Contains($"PartitionKey eq '{streamIdentifier}'", capturedFilter);
            Assert.DoesNotContain("_", capturedFilter.Split("PartitionKey")[1].Split("'")[1]);
        }
    }

    public class AppendAsyncMethod : TableDataStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = CreateSut();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.AppendAsync(null!, CreateTableJsonEvent(0)));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_no_events_provided()
        {
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");

            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.AppendAsync(document));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_events_array_is_empty()
        {
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");

            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.AppendAsync(document, Array.Empty<IEvent>()));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_no_valid_events_converted()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");

            // Create a non-JsonEvent that can't be converted
            var nonJsonEvent = Substitute.For<IEvent>();
            nonJsonEvent.EventType.Returns("TestEvent");
            nonJsonEvent.EventVersion.Returns(0);

            SetupEmptyStreamQuery();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.AppendAsync(document, nonJsonEvent));
        }

        [Fact]
        public async Task Should_submit_batch_transaction()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var events = new IEvent[] { CreateTableJsonEvent(0), CreateTableJsonEvent(1) };

            SetupEmptyStreamQuery();

            // Act
            await sut.AppendAsync(document, events);

            // Assert
            await EventTableClient.Received(1).SubmitTransactionAsync(
                Arg.Is<IEnumerable<TableTransactionAction>>(actions => actions.Count() == 2),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_batch_events_in_groups_of_100()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var events = Enumerable.Range(0, 150).Select(i => (IEvent)CreateTableJsonEvent(i)).ToArray();

            SetupEmptyStreamQuery();

            // Act
            await sut.AppendAsync(document, events);

            // Assert - Should call twice: once for first 100, once for remaining 50
            await EventTableClient.Received(2).SubmitTransactionAsync(
                Arg.Any<IEnumerable<TableTransactionAction>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_processing_exception_on_conflict()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var events = new IEvent[] { CreateTableJsonEvent(0) };

            SetupEmptyStreamQuery();
            EventTableClient.SubmitTransactionAsync(
                Arg.Any<IEnumerable<TableTransactionAction>>(), Arg.Any<CancellationToken>())
                .Throws(new RequestFailedException(409, "Conflict"));

            // Act & Assert
            await Assert.ThrowsAsync<TableDataStoreProcessingException>(() =>
                sut.AppendAsync(document, events));
        }

        [Fact]
        public async Task Should_throw_table_not_found_exception_when_table_missing()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var events = new IEvent[] { CreateTableJsonEvent(0) };

            SetupEmptyStreamQuery();
            EventTableClient.SubmitTransactionAsync(
                Arg.Any<IEnumerable<TableTransactionAction>>(), Arg.Any<CancellationToken>())
                .Throws(new RequestFailedException(404, "Table not found"));

            // Act & Assert
            await Assert.ThrowsAsync<TableDocumentStoreTableNotFoundException>(() =>
                sut.AppendAsync(document, events));
        }

        [Fact]
        public async Task Should_throw_stream_closed_exception_when_stream_is_closed()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var events = new IEvent[] { CreateTableJsonEvent(0) };

            // Setup stream with closed event
            var closedEvent = CreateEventEntity(document.Active.StreamIdentifier, 5, "EventStream.Closed");
            var streamEvents = new List<TableEventEntity> { closedEvent };

            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(CreateAsyncPageable(streamEvents));

            // Act & Assert
            await Assert.ThrowsAsync<EventStreamClosedException>(() =>
                sut.AppendAsync(document, events));
        }

        [Fact]
        public async Task Should_use_chunk_identifier_when_chunking_enabled()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id", chunkingEnabled: true);
            var events = new IEvent[] { CreateTableJsonEvent(0) };

            SetupEmptyStreamQuery();

            TableTransactionAction? capturedAction = null;
            EventTableClient.SubmitTransactionAsync(
                Arg.Do<IEnumerable<TableTransactionAction>>(actions => capturedAction = actions.FirstOrDefault()),
                Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response<IReadOnlyList<Response>>>());

            // Act
            await sut.AppendAsync(document, events);

            // Assert
            Assert.NotNull(capturedAction);
            var entity = capturedAction!.Entity as TableEventEntity;
            Assert.NotNull(entity);
            Assert.Equal(0, entity.ChunkIdentifier);
        }

        [Fact]
        public async Task Should_include_document_hash_in_entity()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            document.SetTestHash("test-hash-123");
            var events = new IEvent[] { CreateTableJsonEvent(0) };

            SetupEmptyStreamQuery();

            TableTransactionAction? capturedAction = null;
            EventTableClient.SubmitTransactionAsync(
                Arg.Do<IEnumerable<TableTransactionAction>>(actions => capturedAction = actions.FirstOrDefault()),
                Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response<IReadOnlyList<Response>>>());

            // Act
            await sut.AppendAsync(document, events);

            // Assert
            Assert.NotNull(capturedAction);
            var entity = capturedAction!.Entity as TableEventEntity;
            Assert.NotNull(entity);
            Assert.Equal("test-hash-123", entity.LastObjectDocumentHash);
        }

        [Fact]
        public async Task Should_use_default_hash_when_document_hash_is_null()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var events = new IEvent[] { CreateTableJsonEvent(0) };

            SetupEmptyStreamQuery();

            TableTransactionAction? capturedAction = null;
            EventTableClient.SubmitTransactionAsync(
                Arg.Do<IEnumerable<TableTransactionAction>>(actions => capturedAction = actions.FirstOrDefault()),
                Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response<IReadOnlyList<Response>>>());

            // Act
            await sut.AppendAsync(document, events);

            // Assert
            Assert.NotNull(capturedAction);
            var entity = capturedAction!.Entity as TableEventEntity;
            Assert.NotNull(entity);
            Assert.Equal("*", entity.LastObjectDocumentHash);
        }

        [Fact]
        public async Task Should_preserve_event_metadata()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var @event = new TableJsonEvent
            {
                EventVersion = 0,
                EventType = "TestEvent",
                SchemaVersion = 2,
                Payload = "{\"key\":\"value\"}",
                ActionMetadata = new ActionMetadata(CorrelationId: "correlation-123"),
                Metadata = new Dictionary<string, string> { ["key1"] = "value1" }
            };

            SetupEmptyStreamQuery();

            TableTransactionAction? capturedAction = null;
            EventTableClient.SubmitTransactionAsync(
                Arg.Do<IEnumerable<TableTransactionAction>>(actions => capturedAction = actions.FirstOrDefault()),
                Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response<IReadOnlyList<Response>>>());

            // Act
            await sut.AppendAsync(document, @event);

            // Assert
            Assert.NotNull(capturedAction);
            var entity = capturedAction!.Entity as TableEventEntity;
            Assert.NotNull(entity);
            Assert.Equal("TestEvent", entity.EventType);
            Assert.Equal(2, entity.SchemaVersion);
            Assert.Equal("{\"key\":\"value\"}", entity.Payload);
            Assert.Contains("correlation-123", entity.ActionMetadata ?? "");
            Assert.Contains("key1", entity.Metadata ?? "");
        }

        [Fact]
        public async Task Should_format_row_key_with_padded_version()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var @event = CreateTableJsonEvent(12345);

            SetupEmptyStreamQuery();

            TableTransactionAction? capturedAction = null;
            EventTableClient.SubmitTransactionAsync(
                Arg.Do<IEnumerable<TableTransactionAction>>(actions => capturedAction = actions.FirstOrDefault()),
                Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response<IReadOnlyList<Response>>>());

            // Act
            await sut.AppendAsync(document, @event);

            // Assert
            Assert.NotNull(capturedAction);
            var entity = capturedAction!.Entity as TableEventEntity;
            Assert.NotNull(entity);
            Assert.Equal("00000000000000012345", entity.RowKey);
        }

        [Fact]
        public async Task Should_not_throw_when_stream_has_events_but_not_closed()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var events = new IEvent[] { CreateTableJsonEvent(5) };

            // Setup stream with existing non-closed events
            var existingEvent = CreateEventEntity(document.Active.StreamIdentifier, 4, "SomeEvent");
            var streamEvents = new List<TableEventEntity> { existingEvent };

            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(CreateAsyncPageable(streamEvents));

            // Act & Assert - should not throw
            await sut.AppendAsync(document, events);

            Assert.True(true);
        }

        private void SetupEmptyStreamQuery()
        {
            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(Array.Empty<Page<TableEventEntity>>()));
        }
    }

    public class AppendAsyncWithPreserveTimestamp : TableDataStoreTests
    {
        [Fact]
        public async Task Should_call_overload_with_preserve_timestamp_false_by_default()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var events = new IEvent[] { CreateTableJsonEvent(0) };

            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(Array.Empty<Page<TableEventEntity>>()));

            // Act - should not throw
            await sut.AppendAsync(document, events);

            // Assert - verify transaction was submitted
            await EventTableClient.Received(1).SubmitTransactionAsync(
                Arg.Any<IEnumerable<TableTransactionAction>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_accept_preserve_timestamp_parameter()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var events = new IEvent[] { CreateTableJsonEvent(0) };

            EventTableClient.QueryAsync<TableEventEntity>(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(Array.Empty<Page<TableEventEntity>>()));

            // Act - should not throw
            await sut.AppendAsync(document, preserveTimestamp: true, events);

            // Assert - verify transaction was submitted
            await EventTableClient.Received(1).SubmitTransactionAsync(
                Arg.Any<IEnumerable<TableTransactionAction>>(),
                Arg.Any<CancellationToken>());
        }
    }

    #region Helper Methods

    protected static AsyncPageable<T> CreateAsyncPageable<T>(IEnumerable<T> items) where T : notnull
    {
        var page = Page<T>.FromValues(items.ToList(), null, Substitute.For<Response>());
        return AsyncPageable<T>.FromPages(new[] { page });
    }

    protected static AsyncPageable<T> CreateThrowingAsyncPageable<T>(Exception ex) where T : notnull
    {
        return new ThrowingAsyncPageable<T>(ex);
    }

    private class ThrowingAsyncPageable<T> : AsyncPageable<T> where T : notnull
    {
        private readonly Exception _exception;

        public ThrowingAsyncPageable(Exception exception)
        {
            _exception = exception;
        }

        public override IAsyncEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null)
        {
            return ThrowingAsyncEnumerable();
        }

        private async IAsyncEnumerable<Page<T>> ThrowingAsyncEnumerable()
        {
            await Task.CompletedTask;
            throw _exception;
#pragma warning disable CS0162 // Unreachable code detected
            yield break;
#pragma warning restore CS0162
        }
    }

    #endregion
}
