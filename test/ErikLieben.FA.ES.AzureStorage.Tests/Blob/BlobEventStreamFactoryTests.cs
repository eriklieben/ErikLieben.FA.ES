using System;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob
{
    public class BlobEventStreamFactoryTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_throw_when_settings_is_null()
            {
                // Arrange
                EventStreamBlobSettings settings = null!;
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
                var aggregateFactory = Substitute.For<IAggregateFactory>();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new BlobEventStreamFactory(
                    settings,
                    clientFactory,
                    documentTagFactory,
                    objectDocumentFactory,
                    aggregateFactory));
            }

            [Fact]
            public void Should_throw_when_client_factory_is_null()
            {
                // Arrange
                var settings = new EventStreamBlobSettings("default");
                IAzureClientFactory<BlobServiceClient> clientFactory = null!;
                var documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
                var aggregateFactory = Substitute.For<IAggregateFactory>();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new BlobEventStreamFactory(
                    settings,
                    clientFactory,
                    documentTagFactory,
                    objectDocumentFactory,
                    aggregateFactory));
            }

            [Fact]
            public void Should_throw_when_document_tag_factory_is_null()
            {
                // Arrange
                var settings = new EventStreamBlobSettings("customStore");
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                IDocumentTagDocumentFactory documentTagFactory = null!;
                var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
                var aggregateFactory = Substitute.For<IAggregateFactory>();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new BlobEventStreamFactory(
                    settings,
                    clientFactory,
                    documentTagFactory,
                    objectDocumentFactory,
                    aggregateFactory));
            }

            [Fact]
            public void Should_throw_when_object_document_factory_is_null()
            {
                // Arrange
                var settings = new EventStreamBlobSettings("customStore");
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                IObjectDocumentFactory objectDocumentFactory = null!;
                var aggregateFactory = Substitute.For<IAggregateFactory>();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new BlobEventStreamFactory(
                    settings,
                    clientFactory,
                    documentTagFactory,
                    objectDocumentFactory,
                    aggregateFactory));
            }

            [Fact]
            public void Should_throw_when_aggregate_factory_is_null()
            {
                // Arrange
                var settings = new EventStreamBlobSettings("customStore");
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
                IAggregateFactory aggregateFactory = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new BlobEventStreamFactory(
                    settings,
                    clientFactory,
                    documentTagFactory,
                    objectDocumentFactory,
                    aggregateFactory));
            }

            [Fact]
            public void Should_initialize_all_properties_when_all_parameters_are_valid()
            {
                // Arrange
                var settings = new EventStreamBlobSettings("customStore");
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
                var aggregateFactory = Substitute.For<IAggregateFactory>();

                // Act
                var sut = new BlobEventStreamFactory(
                    settings,
                    clientFactory,
                    documentTagFactory,
                    objectDocumentFactory,
                    aggregateFactory);

                // Assert
                Assert.NotNull(sut);
            }
        }

        public class Create
        {
            [Fact]
            public void Should_set_stream_type_to_default_data_store_when_stream_type_is_default()
            {
                // Arrange
                var settings = new EventStreamBlobSettings("customStore");
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
                var aggregateFactory = Substitute.For<IAggregateFactory>();

                var sut = new BlobEventStreamFactory(
                    settings,
                    clientFactory,
                    documentTagFactory,
                    objectDocumentFactory,
                    aggregateFactory);

                var streamInfo = Substitute.For<StreamInformation>();
                streamInfo.StreamType = "default";
                var document = new BlobEventStreamDocument(
                    "object-id", "object-name",
                    streamInfo, []);

                var documentTagStore = Substitute.For<IDocumentTagStore>();
                documentTagFactory.CreateDocumentTagStore(document).Returns(documentTagStore);

                // Act
                var result = sut.Create(document);

                // Assert
                Assert.Equal("customStore", document.Active.StreamType);
                Assert.NotNull(result);
                Assert.IsType<BlobEventStream>(result);
            }

            [Fact]
            public void Should_not_change_stream_type_when_not_default()
            {
                // Arrange
                var settings = new EventStreamBlobSettings("customStore")
                {
                    AutoCreateContainer = true
                };
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
                var aggregateFactory = Substitute.For<IAggregateFactory>();

                var sut = new BlobEventStreamFactory(
                    settings,
                    clientFactory,
                    documentTagFactory,
                    objectDocumentFactory,
                    aggregateFactory);

                var streamInfo = Substitute.For<StreamInformation>();
                streamInfo.StreamType = "blob";
                var document = new BlobEventStreamDocument(
                    "object-id2", "object-name2",
                    streamInfo, []);

                // Act
                var result = sut.Create(document);

                // Assert
                document.Active.DidNotReceive().StreamType = "customStore";
                Assert.NotNull(result);
                Assert.IsType<BlobEventStream>(result);
            }

            [Fact]
            public void Should_create_document_tag_store_from_factory()
            {
                // Arrange
                var settings = new EventStreamBlobSettings("customStore");
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
                var aggregateFactory = Substitute.For<IAggregateFactory>();

                var sut = new BlobEventStreamFactory(
                    settings,
                    clientFactory,
                    documentTagFactory,
                    objectDocumentFactory,
                    aggregateFactory);

                var document = new BlobEventStreamDocument(
                    "object-id", "object-name",
                    Substitute.For<StreamInformation>(), []);
                document.Active.StreamType = "blob";

                // Act
                var result = sut.Create(document);

                // Assert
                documentTagFactory.Received(1).CreateDocumentTagStore(document);
                Assert.NotNull(result);
            }

            [Fact]
            public void Should_create_blob_event_stream_with_correct_dependencies()
            {
                // Arrange
                var settings = new EventStreamBlobSettings("customStore")
                {
                    AutoCreateContainer = true
                };
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
                var aggregateFactory = Substitute.For<IAggregateFactory>();

                var sut = new BlobEventStreamFactory(
                    settings,
                    clientFactory,
                    documentTagFactory,
                    objectDocumentFactory,
                    aggregateFactory);

                var document = new BlobEventStreamDocument(
                    "object-id", "object-name",
                    Substitute.For<StreamInformation>(), []);
                document.Active.StreamType = "blob";

                var documentTagStore = Substitute.For<IDocumentTagStore>();
                documentTagFactory.CreateDocumentTagStore(document).Returns(documentTagStore);

                // Act
                var result = sut.Create(document);

                // Assert
                Assert.NotNull(result);
                Assert.IsType<BlobEventStream>(result);
            }
        }
    }
}
