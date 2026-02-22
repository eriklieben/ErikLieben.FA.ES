#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.AzureStorage.Table.Model;
using Microsoft.Extensions.Azure;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableStreamMetadataProviderTests
{
    private static EventStreamTableSettings CreateSettings() =>
        new("test-connection");

    public class Constructor
    {
        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableStreamMetadataProvider(null!, CreateSettings()));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            Assert.Throws<ArgumentNullException>(() =>
                new TableStreamMetadataProvider(clientFactory, null!));
        }
    }

    public class GetStreamMetadataAsync
    {
        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var sut = new TableStreamMetadataProvider(clientFactory, CreateSettings());

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetStreamMetadataAsync(null!, "123"));
        }

        [Fact]
        public async Task Should_throw_when_object_id_is_null()
        {
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var sut = new TableStreamMetadataProvider(clientFactory, CreateSettings());

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetStreamMetadataAsync("test", null!));
        }

        [Fact]
        public async Task Should_return_null_when_document_not_found()
        {
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var tableServiceClient = Substitute.For<TableServiceClient>();
            var documentTableClient = Substitute.For<TableClient>();

            clientFactory.CreateClient(Arg.Any<string>()).Returns(tableServiceClient);
            tableServiceClient.GetTableClient("objectdocumentstore").Returns(documentTableClient);

            var notExistsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            notExistsResponse.HasValue.Returns(false);

            documentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                    "test", "123",
                    select: Arg.Any<IEnumerable<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>())
                .Returns(notExistsResponse);

            var sut = new TableStreamMetadataProvider(clientFactory, CreateSettings());
            var result = await sut.GetStreamMetadataAsync("test", "123");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_null_when_document_table_returns_404()
        {
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var tableServiceClient = Substitute.For<TableServiceClient>();
            var documentTableClient = Substitute.For<TableClient>();

            clientFactory.CreateClient(Arg.Any<string>()).Returns(tableServiceClient);
            tableServiceClient.GetTableClient("objectdocumentstore").Returns(documentTableClient);

            documentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                    Arg.Any<string>(), Arg.Any<string>(),
                    select: Arg.Any<IEnumerable<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>())
                .Throws(new RequestFailedException(404, "Not found"));

            var sut = new TableStreamMetadataProvider(clientFactory, CreateSettings());
            var result = await sut.GetStreamMetadataAsync("test", "123");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_null_when_no_events_exist()
        {
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var tableServiceClient = Substitute.For<TableServiceClient>();
            var documentTableClient = Substitute.For<TableClient>();
            var eventTableClient = Substitute.For<TableClient>();

            clientFactory.CreateClient(Arg.Any<string>()).Returns(tableServiceClient);
            tableServiceClient.GetTableClient("objectdocumentstore").Returns(documentTableClient);
            tableServiceClient.GetTableClient("eventstream").Returns(eventTableClient);

            var docEntity = new TableDocumentEntity
            {
                PartitionKey = "test",
                RowKey = "123",
                ActiveStreamIdentifier = "123-0000000000"
            };
            var existsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            existsResponse.HasValue.Returns(true);
            existsResponse.Value.Returns(docEntity);

            documentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                    "test", "123",
                    select: Arg.Any<IEnumerable<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>())
                .Returns(existsResponse);

            eventTableClient.QueryAsync<TableEventEntity>(
                    Arg.Any<string>(),
                    select: Arg.Any<IEnumerable<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(Array.Empty<Page<TableEventEntity>>()));

            var sut = new TableStreamMetadataProvider(clientFactory, CreateSettings());
            var result = await sut.GetStreamMetadataAsync("test", "123");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_metadata_with_event_count_and_dates()
        {
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var tableServiceClient = Substitute.For<TableServiceClient>();
            var documentTableClient = Substitute.For<TableClient>();
            var eventTableClient = Substitute.For<TableClient>();

            clientFactory.CreateClient(Arg.Any<string>()).Returns(tableServiceClient);
            tableServiceClient.GetTableClient("objectdocumentstore").Returns(documentTableClient);
            tableServiceClient.GetTableClient("eventstream").Returns(eventTableClient);

            var docEntity = new TableDocumentEntity
            {
                PartitionKey = "order",
                RowKey = "order-1",
                ActiveStreamIdentifier = "order1-0000000000"
            };
            var existsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            existsResponse.HasValue.Returns(true);
            existsResponse.Value.Returns(docEntity);

            documentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                    "order", "order-1",
                    select: Arg.Any<IEnumerable<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>())
                .Returns(existsResponse);

            var oldDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var newDate = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

            var events = new[]
            {
                new TableEventEntity { PartitionKey = "order1-0000000000", RowKey = "00000000000000000000", Timestamp = oldDate },
                new TableEventEntity { PartitionKey = "order1-0000000000", RowKey = "00000000000000000001", Timestamp = newDate },
            };

            var page = Page<TableEventEntity>.FromValues(events, null, Substitute.For<Response>());
            eventTableClient.QueryAsync<TableEventEntity>(
                    Arg.Any<string>(),
                    select: Arg.Any<IEnumerable<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(new[] { page }));

            var sut = new TableStreamMetadataProvider(clientFactory, CreateSettings());
            var result = await sut.GetStreamMetadataAsync("Order", "order-1");

            Assert.NotNull(result);
            Assert.Equal("Order", result.ObjectName);
            Assert.Equal("order-1", result.ObjectId);
            Assert.Equal(2, result.EventCount);
            Assert.Equal(oldDate, result.OldestEventDate);
            Assert.Equal(newDate, result.NewestEventDate);
        }

        [Fact]
        public async Task Should_skip_payload_chunk_rows()
        {
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var tableServiceClient = Substitute.For<TableServiceClient>();
            var documentTableClient = Substitute.For<TableClient>();
            var eventTableClient = Substitute.For<TableClient>();

            clientFactory.CreateClient(Arg.Any<string>()).Returns(tableServiceClient);
            tableServiceClient.GetTableClient("objectdocumentstore").Returns(documentTableClient);
            tableServiceClient.GetTableClient("eventstream").Returns(eventTableClient);

            var docEntity = new TableDocumentEntity
            {
                PartitionKey = "order",
                RowKey = "order-1",
                ActiveStreamIdentifier = "order1-0000000000"
            };
            var existsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            existsResponse.HasValue.Returns(true);
            existsResponse.Value.Returns(docEntity);

            documentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                    "order", "order-1",
                    select: Arg.Any<IEnumerable<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>())
                .Returns(existsResponse);

            var now = DateTimeOffset.UtcNow;
            var events = new[]
            {
                new TableEventEntity { PartitionKey = "order1-0000000000", RowKey = "00000000000000000000", Timestamp = now },
                new TableEventEntity { PartitionKey = "order1-0000000000", RowKey = "00000000000000000000_p1", Timestamp = now, PayloadChunkIndex = 1 },
                new TableEventEntity { PartitionKey = "order1-0000000000", RowKey = "00000000000000000000_p2", Timestamp = now, PayloadChunkIndex = 2 },
            };

            var page = Page<TableEventEntity>.FromValues(events, null, Substitute.For<Response>());
            eventTableClient.QueryAsync<TableEventEntity>(
                    Arg.Any<string>(),
                    select: Arg.Any<IEnumerable<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>())
                .Returns(AsyncPageable<TableEventEntity>.FromPages(new[] { page }));

            var sut = new TableStreamMetadataProvider(clientFactory, CreateSettings());
            var result = await sut.GetStreamMetadataAsync("Order", "order-1");

            Assert.NotNull(result);
            Assert.Equal(1, result.EventCount);
        }

        [Fact]
        public async Task Should_return_null_when_event_table_returns_404()
        {
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var tableServiceClient = Substitute.For<TableServiceClient>();
            var documentTableClient = Substitute.For<TableClient>();
            var eventTableClient = Substitute.For<TableClient>();

            clientFactory.CreateClient(Arg.Any<string>()).Returns(tableServiceClient);
            tableServiceClient.GetTableClient("objectdocumentstore").Returns(documentTableClient);
            tableServiceClient.GetTableClient("eventstream").Returns(eventTableClient);

            var docEntity = new TableDocumentEntity
            {
                PartitionKey = "test",
                RowKey = "123",
                ActiveStreamIdentifier = "123-0000000000"
            };
            var existsResponse = Substitute.For<NullableResponse<TableDocumentEntity>>();
            existsResponse.HasValue.Returns(true);
            existsResponse.Value.Returns(docEntity);

            documentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                    "test", "123",
                    select: Arg.Any<IEnumerable<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>())
                .Returns(existsResponse);

            eventTableClient.QueryAsync<TableEventEntity>(
                    Arg.Any<string>(),
                    select: Arg.Any<IEnumerable<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>())
                .Returns(x => { throw new RequestFailedException(404, "Table not found"); });

            var sut = new TableStreamMetadataProvider(clientFactory, CreateSettings());
            var result = await sut.GetStreamMetadataAsync("test", "123");

            Assert.Null(result);
        }
    }
}
