using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

public class BlobTagFactoryTests
{
    public class Constructor
    {
        [Fact]
        public void Should_initialize_properly()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamDefaultTypeSettings { DocumentTagType = "test-type", EventStreamTagType = "stream-type" };
            var blobSettings = new EventStreamBlobSettings("test-store") { AutoCreateContainer = true };

            // Act
            var sut = new BlobTagFactory(clientFactory, settings, blobSettings);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class CreateDocumentTagStoreWithDocument
    {
        [Fact]
        public void Should_throw_when_document_is_null()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamDefaultTypeSettings { DocumentTagType = "test-type" };
            var blobSettings = new EventStreamBlobSettings("test-store") { AutoCreateContainer = true };
            var sut = new BlobTagFactory(clientFactory, settings, blobSettings);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.CreateDocumentTagStore((IObjectDocument)null!));
        }

        [Fact]
        public void Should_throw_when_document_tag_type_is_null()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamDefaultTypeSettings { DocumentTagType = "test-type" };
            var blobSettings = new EventStreamBlobSettings("test-store") { AutoCreateContainer = true };
            var sut = new BlobTagFactory(clientFactory, settings, blobSettings);

            var document = Substitute.For<IObjectDocument>();
            var documentState = Substitute.For<StreamInformation>();
            documentState.DocumentTagType = null!;
            document.Active.Returns(documentState);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.CreateDocumentTagStore(document));
        }

        [Fact]
        public void Should_create_blob_document_tag_store_with_document_tag_type()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamDefaultTypeSettings { DocumentTagType = "test-type" };
            var blobSettings = new EventStreamBlobSettings("test-store") { AutoCreateContainer = true };
            var sut = new BlobTagFactory(clientFactory, settings, blobSettings);

            var document = Substitute.For<IObjectDocument>();
            var documentState = Substitute.For<StreamInformation>();
            documentState.DocumentTagType = "document-tag-type";
            document.Active.Returns(documentState);

            // Act
            var result = sut.CreateDocumentTagStore(document);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<BlobDocumentTagStore>(result);
        }
    }

    public class CreateDocumentTagStoreNoParameters
    {
        [Fact]
        public void Should_create_blob_document_tag_store_with_default_tag_type()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamDefaultTypeSettings { DocumentTagType = "test-type" };
            var blobSettings = new EventStreamBlobSettings("test-store") { AutoCreateContainer = true };
            var sut = new BlobTagFactory(clientFactory, settings, blobSettings);

            // Act
            var result = sut.CreateDocumentTagStore();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<BlobDocumentTagStore>(result);
        }
    }

    public class CreateDocumentTagStoreWithType
    {
        [Fact]
        public void Should_create_blob_document_tag_store_with_specified_type()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamDefaultTypeSettings { DocumentTagType = "test-type" };
            var blobSettings = new EventStreamBlobSettings("test-store") { AutoCreateContainer = true };
            var sut = new BlobTagFactory(clientFactory, settings, blobSettings);
            var customType = "custom-type";

            // Act
            var result = sut.CreateDocumentTagStore(customType);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<BlobDocumentTagStore>(result);
        }
    }

    public class CreateStreamTagStore
    {
        [Fact]
        public void Should_throw_when_document_is_null()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamDefaultTypeSettings { EventStreamTagType = "stream-type" };
            var blobSettings = new EventStreamBlobSettings("test-store") { AutoCreateContainer = true };
            var sut = new BlobTagFactory(clientFactory, settings, blobSettings);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.CreateStreamTagStore(null!));
        }

        [Fact]
        public void Should_throw_when_document_tag_type_is_null()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamDefaultTypeSettings { EventStreamTagType = "stream-type" };
            var blobSettings = new EventStreamBlobSettings("test-store") { AutoCreateContainer = true };
            var sut = new BlobTagFactory(clientFactory, settings, blobSettings);

            var document = Substitute.For<IObjectDocument>();
            var documentState = Substitute.For<StreamInformation>();
            documentState.DocumentTagType = null!;
            document.Active.Returns(documentState);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.CreateStreamTagStore(document));
        }

        [Fact]
        public void Should_create_blob_document_tag_store_with_event_stream_tag_type()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var settings = new EventStreamDefaultTypeSettings { EventStreamTagType = "stream-type" };
            var blobSettings = new EventStreamBlobSettings("test-store") { AutoCreateContainer = true };
            var sut = new BlobTagFactory(clientFactory, settings, blobSettings);

            var document = Substitute.For<IObjectDocument>();
            var documentState = Substitute.For<StreamInformation>();
            documentState.DocumentTagType = "document-tag-type";
            document.Active.Returns(documentState);

            // Act
            var result = sut.CreateStreamTagStore(document);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<BlobDocumentTagStore>(result);
        }
    }
}
