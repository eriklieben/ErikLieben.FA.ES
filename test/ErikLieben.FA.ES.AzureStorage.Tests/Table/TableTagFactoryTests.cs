using System;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableTagFactoryTests
{
    private readonly IAzureClientFactory<TableServiceClient> clientFactory;
    private readonly EventStreamTableSettings tableSettings;

    public TableTagFactoryTests()
    {
        clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
        tableSettings = new EventStreamTableSettings("test-connection");
    }

    public class CreateDocumentTagStore : TableTagFactoryTests
    {
        [Fact]
        public void Should_return_table_document_tag_store_with_default_settings()
        {
            // Arrange
            var sut = new TableTagFactory(clientFactory, tableSettings);

            // Act
            var result = sut.CreateDocumentTagStore();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<TableDocumentTagStore>(result);
        }

        [Fact]
        public void Should_return_table_document_tag_store_for_document()
        {
            // Arrange
            var sut = new TableTagFactory(clientFactory, tableSettings);
            var document = Substitute.For<IObjectDocument>();
            var streamInfo = Substitute.For<StreamInformation>();
            streamInfo.DocumentTagType = "table";
            document.Active.Returns(streamInfo);

            // Act
            var result = sut.CreateDocumentTagStore(document);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<TableDocumentTagStore>(result);
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_document_is_null()
        {
            // Arrange
            var sut = new TableTagFactory(clientFactory, tableSettings);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.CreateDocumentTagStore((IObjectDocument)null!));
        }

        [Fact]
        public void Should_return_table_document_tag_store_for_type()
        {
            // Arrange
            var sut = new TableTagFactory(clientFactory, tableSettings);

            // Act
            var result = sut.CreateDocumentTagStore("custom-type");

            // Assert
            Assert.NotNull(result);
            Assert.IsType<TableDocumentTagStore>(result);
        }
    }

    public class CreateStreamTagStore : TableTagFactoryTests
    {
        [Fact]
        public void Should_return_table_stream_tag_store_with_default_settings()
        {
            // Arrange
            var sut = new TableTagFactory(clientFactory, tableSettings);

            // Act
            var result = sut.CreateStreamTagStore();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<TableStreamTagStore>(result);
        }

        [Fact]
        public void Should_return_table_stream_tag_store_for_document()
        {
            // Arrange
            var sut = new TableTagFactory(clientFactory, tableSettings);
            var document = Substitute.For<IObjectDocument>();
            var streamInfo = Substitute.For<StreamInformation>();
            streamInfo.EventStreamTagType = "table";
            document.Active.Returns(streamInfo);

            // Act
            var result = sut.CreateStreamTagStore(document);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<TableStreamTagStore>(result);
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_document_is_null()
        {
            // Arrange
            var sut = new TableTagFactory(clientFactory, tableSettings);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.CreateStreamTagStore(null!));
        }
    }
}
