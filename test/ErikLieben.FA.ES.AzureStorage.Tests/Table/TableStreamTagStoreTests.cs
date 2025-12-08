using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableStreamTagStoreTests
{
    private readonly IAzureClientFactory<TableServiceClient> mockClientFactory;
    private readonly EventStreamTableSettings settings;

    public TableStreamTagStoreTests()
    {
        mockClientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();

        settings = new EventStreamTableSettings(
            defaultDataStore: "defaultConnection",
            autoCreateTable: true);
    }

    public class Constructor : TableStreamTagStoreTests
    {
        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Act
            var sut = new TableStreamTagStore(mockClientFactory, settings);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_clientFactory_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TableStreamTagStore(null!, settings));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_settings_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TableStreamTagStore(mockClientFactory, null!));
        }
    }

    public class SetAsyncMethod : TableStreamTagStoreTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_document_is_null()
        {
            // Arrange
            var sut = new TableStreamTagStore(mockClientFactory, settings);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SetAsync(null!, "tag"));
        }

        [Fact]
        public async Task Should_throw_ArgumentNullException_when_stream_identifier_is_null()
        {
            // Arrange
            var sut = new TableStreamTagStore(mockClientFactory, settings);
            var mockDocument = Substitute.For<IObjectDocument>();
            var mockActive = Substitute.For<StreamInformation>();

            mockDocument.Active.Returns(mockActive);
            mockActive.StreamIdentifier = null!;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SetAsync(mockDocument, "tag"));
        }

        [Fact]
        public async Task Should_call_table_client_upsert()
        {
            // Arrange
            var mockTableServiceClient = Substitute.For<TableServiceClient>();
            var mockTableClient = Substitute.For<TableClient>();

            var sut = new TableStreamTagStore(mockClientFactory, settings);
            var mockDocument = Substitute.For<IObjectDocument>();
            var mockActive = Substitute.For<StreamInformation>();

            mockDocument.Active.Returns(mockActive);
            mockActive.StreamIdentifier = "stream-123";
            mockDocument.ObjectName.Returns("TestObject");
            mockDocument.ObjectId.Returns("obj-456");
            mockActive.StreamTagStore = "defaultConnection";

            mockClientFactory.CreateClient("defaultConnection").Returns(mockTableServiceClient);
            mockTableServiceClient.GetTableClient(settings.DefaultStreamTagTableName).Returns(mockTableClient);
            mockTableClient.CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Response.FromValue(
                    TableModelFactory.TableItem(settings.DefaultStreamTagTableName),
                    Substitute.For<Response>())));

            // Act
            await sut.SetAsync(mockDocument, "my-tag");

            // Assert - verify CreateClient was called
            mockClientFactory.Received(1).CreateClient("defaultConnection");
        }

        [Fact]
        public async Task Should_use_default_store_when_stream_tag_store_is_empty()
        {
            // Arrange
            var mockTableServiceClient = Substitute.For<TableServiceClient>();
            var mockTableClient = Substitute.For<TableClient>();

            var sut = new TableStreamTagStore(mockClientFactory, settings);
            var mockDocument = Substitute.For<IObjectDocument>();
            var mockActive = Substitute.For<StreamInformation>();

            mockDocument.Active.Returns(mockActive);
            mockActive.StreamIdentifier = "stream-123";
            mockDocument.ObjectName.Returns("TestObject");
            mockDocument.ObjectId.Returns("obj-456");
            mockActive.StreamTagStore = null;
            mockActive.StreamTagConnectionName = null;

            mockClientFactory.CreateClient(settings.DefaultDocumentTagStore).Returns(mockTableServiceClient);
            mockTableServiceClient.GetTableClient(settings.DefaultStreamTagTableName).Returns(mockTableClient);
            mockTableClient.CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Response.FromValue(
                    TableModelFactory.TableItem(settings.DefaultStreamTagTableName),
                    Substitute.For<Response>())));

            // Act
            await sut.SetAsync(mockDocument, "my-tag");

            // Assert
            mockClientFactory.Received(1).CreateClient(settings.DefaultDocumentTagStore);
        }
    }

    public class GetAsyncMethod : TableStreamTagStoreTests
    {
        [Fact]
        public async Task Should_return_empty_when_table_query_throws_404()
        {
            // Arrange
            var mockTableServiceClient = Substitute.For<TableServiceClient>();
            var mockTableClient = Substitute.For<TableClient>();

            var sut = new TableStreamTagStore(mockClientFactory, settings);

            mockClientFactory.CreateClient(settings.DefaultDocumentTagStore).Returns(mockTableServiceClient);
            mockTableServiceClient.GetTableClient(settings.DefaultStreamTagTableName).Returns(mockTableClient);

            // When QueryAsync is called and enumerated, it throws 404
            var requestFailedException = new RequestFailedException(404, "Table not found");
            mockTableClient.QueryAsync<TableEntity>(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                .Returns(x => throw requestFailedException);

            // Act
            var result = await sut.GetAsync("TestObject", "mytag");

            // Assert
            Assert.Empty(result);
        }

    }
}
