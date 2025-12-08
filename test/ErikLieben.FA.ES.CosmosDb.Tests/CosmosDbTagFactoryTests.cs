using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbTagFactoryTests
{
    private readonly CosmosClient cosmosClient;
    private readonly EventStreamCosmosDbSettings cosmosDbSettings;
    private readonly IObjectDocument objectDocument;

    public CosmosDbTagFactoryTests()
    {
        cosmosClient = Substitute.For<CosmosClient>();
        cosmosDbSettings = new EventStreamCosmosDbSettings();
        objectDocument = Substitute.For<IObjectDocument>();

        var streamInfo = new StreamInformation
        {
            DocumentTagType = "cosmosdb",
            EventStreamTagType = "cosmosdb"
        };
        objectDocument.Active.Returns(streamInfo);
    }

    public class Constructor : CosmosDbTagFactoryTests
    {
        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var sut = new CosmosDbTagFactory(cosmosClient, cosmosDbSettings);
            Assert.NotNull(sut);
        }
    }

    public class CreateDocumentTagStoreWithDocument : CosmosDbTagFactoryTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = new CosmosDbTagFactory(cosmosClient, cosmosDbSettings);
            Assert.Throws<ArgumentNullException>(() => sut.CreateDocumentTagStore((IObjectDocument)null!));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_document_tag_type_is_null()
        {
            var sut = new CosmosDbTagFactory(cosmosClient, cosmosDbSettings);
            var doc = Substitute.For<IObjectDocument>();
            doc.Active.Returns(new StreamInformation { DocumentTagType = null });

            Assert.Throws<ArgumentNullException>(() => sut.CreateDocumentTagStore(doc));
        }

        [Fact]
        public void Should_return_cosmos_db_document_tag_store()
        {
            var sut = new CosmosDbTagFactory(cosmosClient, cosmosDbSettings);

            var result = sut.CreateDocumentTagStore(objectDocument);

            Assert.NotNull(result);
            Assert.IsType<CosmosDbDocumentTagStore>(result);
        }
    }

    public class CreateDocumentTagStoreDefault : CosmosDbTagFactoryTests
    {
        [Fact]
        public void Should_return_cosmos_db_document_tag_store()
        {
            var sut = new CosmosDbTagFactory(cosmosClient, cosmosDbSettings);

            var result = sut.CreateDocumentTagStore();

            Assert.NotNull(result);
            Assert.IsType<CosmosDbDocumentTagStore>(result);
        }
    }

    public class CreateDocumentTagStoreWithType : CosmosDbTagFactoryTests
    {
        [Fact]
        public void Should_return_cosmos_db_document_tag_store_for_any_type()
        {
            var sut = new CosmosDbTagFactory(cosmosClient, cosmosDbSettings);

            var result = sut.CreateDocumentTagStore("cosmosdb");

            Assert.NotNull(result);
            Assert.IsType<CosmosDbDocumentTagStore>(result);
        }
    }

    public class CreateStreamTagStoreWithDocument : CosmosDbTagFactoryTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = new CosmosDbTagFactory(cosmosClient, cosmosDbSettings);
            Assert.Throws<ArgumentNullException>(() => sut.CreateStreamTagStore(null!));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_event_stream_tag_type_is_null()
        {
            var sut = new CosmosDbTagFactory(cosmosClient, cosmosDbSettings);
            var doc = Substitute.For<IObjectDocument>();
            doc.Active.Returns(new StreamInformation { EventStreamTagType = null });

            Assert.Throws<ArgumentNullException>(() => sut.CreateStreamTagStore(doc));
        }

        [Fact]
        public void Should_return_cosmos_db_stream_tag_store()
        {
            var sut = new CosmosDbTagFactory(cosmosClient, cosmosDbSettings);

            var result = sut.CreateStreamTagStore(objectDocument);

            Assert.NotNull(result);
            Assert.IsType<CosmosDbStreamTagStore>(result);
        }
    }

    public class CreateStreamTagStoreDefault : CosmosDbTagFactoryTests
    {
        [Fact]
        public void Should_return_cosmos_db_stream_tag_store()
        {
            var sut = new CosmosDbTagFactory(cosmosClient, cosmosDbSettings);

            var result = sut.CreateStreamTagStore();

            Assert.NotNull(result);
            Assert.IsType<CosmosDbStreamTagStore>(result);
        }
    }
}
