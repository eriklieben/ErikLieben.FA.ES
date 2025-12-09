using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbEventStreamFactoryTests
{
    private readonly EventStreamCosmosDbSettings settings;
    private readonly CosmosClient cosmosClient;
    private readonly IDocumentTagDocumentFactory documentTagFactory;
    private readonly IObjectDocumentFactory objectDocumentFactory;
    private readonly IAggregateFactory aggregateFactory;
    private readonly IObjectDocument objectDocument;

    public CosmosDbEventStreamFactoryTests()
    {
        settings = new EventStreamCosmosDbSettings();
        cosmosClient = Substitute.For<CosmosClient>();
        documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
        objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
        aggregateFactory = Substitute.For<IAggregateFactory>();
        objectDocument = Substitute.For<IObjectDocument>();

        var streamInfo = new StreamInformation
        {
            StreamIdentifier = "test-stream-0000000000",
            StreamType = "cosmosdb",
            DocumentTagType = "cosmosdb"
        };
        objectDocument.Active.Returns(streamInfo);
        objectDocument.ObjectName.Returns("TestObject");
        objectDocument.ObjectId.Returns("test-id");
        objectDocument.TerminatedStreams.Returns([]);

        var documentTagStore = Substitute.For<IDocumentTagStore>();
        documentTagFactory.CreateDocumentTagStore(Arg.Any<IObjectDocument>()).Returns(documentTagStore);
    }

    public class Constructor : CosmosDbEventStreamFactoryTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbEventStreamFactory(null!, cosmosClient, documentTagFactory, objectDocumentFactory, aggregateFactory));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_cosmos_client_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbEventStreamFactory(settings, null!, documentTagFactory, objectDocumentFactory, aggregateFactory));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_document_tag_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbEventStreamFactory(settings, cosmosClient, null!, objectDocumentFactory, aggregateFactory));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_object_document_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbEventStreamFactory(settings, cosmosClient, documentTagFactory, null!, aggregateFactory));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_aggregate_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbEventStreamFactory(settings, cosmosClient, documentTagFactory, objectDocumentFactory, null!));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            var sut = new CosmosDbEventStreamFactory(settings, cosmosClient, documentTagFactory, objectDocumentFactory, aggregateFactory);
            Assert.NotNull(sut);
        }
    }

    public class Create : CosmosDbEventStreamFactoryTests
    {
        [Fact]
        public void Should_return_cosmos_db_event_stream()
        {
            var sut = new CosmosDbEventStreamFactory(settings, cosmosClient, documentTagFactory, objectDocumentFactory, aggregateFactory);

            var result = sut.Create(objectDocument);

            Assert.NotNull(result);
            Assert.IsType<CosmosDbEventStream>(result);
        }

        [Fact]
        public void Should_update_stream_type_when_default()
        {
            var sut = new CosmosDbEventStreamFactory(settings, cosmosClient, documentTagFactory, objectDocumentFactory, aggregateFactory);

            var doc = Substitute.For<IObjectDocument>();
            var streamInfo = new StreamInformation
            {
                StreamIdentifier = "test-stream",
                StreamType = "default",
                DocumentTagType = "cosmosdb"
            };
            doc.Active.Returns(streamInfo);
            doc.ObjectName.Returns("TestObject");
            doc.ObjectId.Returns("test-id");
            doc.TerminatedStreams.Returns([]);

            var tagStore = Substitute.For<IDocumentTagStore>();
            documentTagFactory.CreateDocumentTagStore(Arg.Any<IObjectDocument>()).Returns(tagStore);

            sut.Create(doc);

            Assert.Equal(settings.DefaultDataStore, streamInfo.StreamType);
        }

        [Fact]
        public void Should_not_update_stream_type_when_not_default()
        {
            var sut = new CosmosDbEventStreamFactory(settings, cosmosClient, documentTagFactory, objectDocumentFactory, aggregateFactory);

            var doc = Substitute.For<IObjectDocument>();
            var streamInfo = new StreamInformation
            {
                StreamIdentifier = "test-stream",
                StreamType = "custom-type",
                DocumentTagType = "cosmosdb"
            };
            doc.Active.Returns(streamInfo);
            doc.ObjectName.Returns("TestObject");
            doc.ObjectId.Returns("test-id");
            doc.TerminatedStreams.Returns([]);

            var tagStore = Substitute.For<IDocumentTagStore>();
            documentTagFactory.CreateDocumentTagStore(Arg.Any<IObjectDocument>()).Returns(tagStore);

            sut.Create(doc);

            Assert.Equal("custom-type", streamInfo.StreamType);
        }
    }
}
