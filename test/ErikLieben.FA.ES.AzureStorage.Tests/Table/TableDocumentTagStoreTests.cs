#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using System.Threading.Tasks;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableDocumentTagStoreTests
{
    private readonly IAzureClientFactory<TableServiceClient> clientFactory;
    private readonly TableServiceClient tableServiceClient;
    private readonly TableClient tableClient;
    private readonly IObjectDocument objectDocument;
    private readonly EventStreamTableSettings settings;

    public TableDocumentTagStoreTests()
    {
        clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
        tableServiceClient = Substitute.For<TableServiceClient>();
        tableClient = Substitute.For<TableClient>();
        objectDocument = Substitute.For<IObjectDocument>();
        settings = new EventStreamTableSettings("test-connection");

        // Setup default stream information
        var streamInformation = Substitute.For<StreamInformation>();
        streamInformation.StreamIdentifier = "test-stream";
        streamInformation.DocumentTagStore = "test-connection";

        // Setup default object document
        objectDocument.Active.Returns(streamInformation);
        objectDocument.ObjectName.Returns("TestObject");
        objectDocument.ObjectId.Returns("test-id");

        // Setup table client chain
        clientFactory.CreateClient(Arg.Any<string>()).Returns(tableServiceClient);
        tableServiceClient.GetTableClient(Arg.Any<string>()).Returns(tableClient);
    }

    public class Constructor : TableDocumentTagStoreTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_client_factory_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TableDocumentTagStore(null!, settings, "test-connection"));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_settings_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TableDocumentTagStore(clientFactory, null!, "test-connection"));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            // Act
            var sut = new TableDocumentTagStore(clientFactory, settings, "test-connection");

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class SetAsync : TableDocumentTagStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            // Arrange
            var sut = new TableDocumentTagStore(clientFactory, settings, "test-connection");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(null!, "test-tag"));
        }
    }
}
