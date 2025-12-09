#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using System.Collections.Generic;
using Azure.Data.Tables;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableEventStreamFactoryTests
{
    protected readonly EventStreamTableSettings Settings;
    protected readonly IAzureClientFactory<TableServiceClient> ClientFactory;
    protected readonly IDocumentTagDocumentFactory DocumentTagFactory;
    protected readonly IObjectDocumentFactory ObjectDocumentFactory;
    protected readonly IAggregateFactory AggregateFactory;
    protected readonly TableServiceClient TableServiceClient;
    protected readonly TableClient TableClient;

    public TableEventStreamFactoryTests()
    {
        Settings = new EventStreamTableSettings("test-connection");
        ClientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
        DocumentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
        ObjectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
        AggregateFactory = Substitute.For<IAggregateFactory>();
        TableServiceClient = Substitute.For<TableServiceClient>();
        TableClient = Substitute.For<TableClient>();

        ClientFactory.CreateClient(Arg.Any<string>()).Returns(TableServiceClient);
        TableServiceClient.GetTableClient(Arg.Any<string>()).Returns(TableClient);
    }

    protected TableEventStreamFactory CreateSut() =>
        new(Settings, ClientFactory, DocumentTagFactory, ObjectDocumentFactory, AggregateFactory);

    protected static IObjectDocument CreateMockDocument(string objectName, string objectId, string streamType = "table")
    {
        var document = Substitute.For<IObjectDocument>();
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000",
            StreamType = streamType,
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

        document.Active.Returns(streamInfo);
        document.ObjectName.Returns(objectName);
        document.ObjectId.Returns(objectId);
        document.TerminatedStreams.Returns(new List<TerminatedStream>());

        return document;
    }

    public class Constructor : TableEventStreamFactoryTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableEventStreamFactory(null!, ClientFactory, DocumentTagFactory, ObjectDocumentFactory, AggregateFactory));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableEventStreamFactory(Settings, null!, DocumentTagFactory, ObjectDocumentFactory, AggregateFactory));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_document_tag_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableEventStreamFactory(Settings, ClientFactory, null!, ObjectDocumentFactory, AggregateFactory));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_object_document_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableEventStreamFactory(Settings, ClientFactory, DocumentTagFactory, null!, AggregateFactory));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_aggregate_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableEventStreamFactory(Settings, ClientFactory, DocumentTagFactory, ObjectDocumentFactory, null!));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            var sut = CreateSut();
            Assert.NotNull(sut);
        }
    }

    public class CreateMethod : TableEventStreamFactoryTests
    {
        [Fact]
        public void Should_return_table_event_stream()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var documentTagStore = Substitute.For<IDocumentTagStore>();
            DocumentTagFactory.CreateDocumentTagStore(document).Returns(documentTagStore);

            // Act
            var result = sut.Create(document);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<TableEventStream>(result);
        }

        [Fact]
        public void Should_create_document_tag_store_for_document()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var documentTagStore = Substitute.For<IDocumentTagStore>();
            DocumentTagFactory.CreateDocumentTagStore(document).Returns(documentTagStore);

            // Act
            sut.Create(document);

            // Assert
            DocumentTagFactory.Received(1).CreateDocumentTagStore(document);
        }

        [Fact]
        public void Should_set_default_stream_type_when_document_has_default()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id", streamType: "default");
            var documentTagStore = Substitute.For<IDocumentTagStore>();
            DocumentTagFactory.CreateDocumentTagStore(document).Returns(documentTagStore);

            // Act
            sut.Create(document);

            // Assert
            Assert.Equal(Settings.DefaultDataStore, document.Active.StreamType);
        }

        [Fact]
        public void Should_not_modify_stream_type_when_not_default()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id", streamType: "table");
            var documentTagStore = Substitute.For<IDocumentTagStore>();
            DocumentTagFactory.CreateDocumentTagStore(document).Returns(documentTagStore);

            // Act
            sut.Create(document);

            // Assert
            Assert.Equal("table", document.Active.StreamType);
        }

        [Fact]
        public void Should_create_event_stream_with_dependencies()
        {
            // Arrange
            var sut = CreateSut();
            var document = CreateMockDocument("TestObject", "test-id");
            var documentTagStore = Substitute.For<IDocumentTagStore>();
            DocumentTagFactory.CreateDocumentTagStore(document).Returns(documentTagStore);

            // Act
            var result = sut.Create(document) as TableEventStream;

            // Assert
            Assert.NotNull(result);
        }
    }
}
