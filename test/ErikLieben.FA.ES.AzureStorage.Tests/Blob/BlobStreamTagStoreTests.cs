using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

[JsonSerializable(typeof(BlobDocumentTagStoreDocument))]
public partial class BlobDocumentTagStoreDocumentJsonContext : JsonSerializerContext
{
}


public class BlobStreamTagStoreTests
{
    private readonly IAzureClientFactory<BlobServiceClient> mockClientFactory;
    private readonly BlobServiceClient mockBlobServiceClient;
    private readonly BlobContainerClient mockContainerClient;
    private readonly BlobClient mockBlobClient;

    public BlobStreamTagStoreTests()
    {
        mockClientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
        mockBlobServiceClient = Substitute.For<BlobServiceClient>();
        mockContainerClient = Substitute.For<BlobContainerClient>();
        mockBlobClient = Substitute.For<BlobClient>();
    }

    public class Constructor : BlobStreamTagStoreTests
    {
        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange & Act
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class SetAsyncMethod : BlobStreamTagStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(null!, "tag"));
        }

        // [Fact]
        // public async Task Should_throw_argument_null_exception_when_document_active_stream_identifier_is_null()
        // {
        //     // Arrange
        //     var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
        //     var mockDocument = Substitute.For<IObjectDocument>();
        //     var mockActive = Substitute.For<StreamInformation>();
        //     mockDocument.Active.Returns(mockActive);
        //     mockActive.StreamIdentifier.Returns((string)null!);
        //
        //     // Act & Assert
        //     await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(mockDocument, "tag"));
        // }
        //
        // [Fact]
        // public async Task Should_throw_argument_null_exception_when_blob_event_stream_document_is_null()
        // {
        //     // Arrange
        //     var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
        //     var mockDocument = Substitute.For<IObjectDocument>();
        //     var mockActive = Substitute.For<StreamInformation>();
        //     mockDocument.Active.Returns(mockActive);
        //     mockActive.StreamIdentifier.Returns("stream-id");
        //     mockDocument.ObjectName.Returns("object-name");
        //     mockActive.StreamConnectionName.Returns("connection");
        //     mockDocument.ObjectId.Returns(Guid.NewGuid().ToString());
        //
        //     mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
        //     mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
        //     mockContainerClient.GetBlobClient("tags/stream/stream-id.json").Returns(mockBlobClient);
        //
        //     // Mock BlobEventStreamDocument.From to return null
        //     // This will require mocking the static method, which might need a wrapper or different approach
        //
        //     // Act & Assert
        //     await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(mockDocument, "tag"));
        // }

        [Fact]
        public async Task Should_create_new_document_when_blob_does_not_exist()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", false);
            var mockDocument = Substitute.For<IObjectDocument>();
            var mockActive = Substitute.For<StreamInformation>();
            var objectId = Guid.NewGuid();

            mockDocument.Active.Returns(mockActive);
            mockActive.StreamIdentifier = "stream-id";
            mockDocument.ObjectName.Returns("object-name");
            mockActive.StreamConnectionName = "connection";
            mockDocument.ObjectId.Returns(objectId.ToString());
            mockDocument.TerminatedStreams.Returns([]);

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream/stream-id.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.SetAsync(mockDocument, "test-tag");

            // Assert
            await mockBlobClient.Received(1).UploadAsync(
                Arg.Any<Stream>(),
                Arg.Is<BlobUploadOptions>(options =>
                    options.Conditions == null || options.Conditions.IfMatch == null));
        }

        // [Fact]
        // public async Task Should_update_existing_document_when_blob_exists_and_object_id_not_present()
        // {
        //     // Arrange
        //     var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", false);
        //     var mockDocument = Substitute.For<IObjectDocument>();
        //     var mockActive = Substitute.For<StreamInformation>();
        //     var objectId = Guid.NewGuid();
        //     var existingObjectId = Guid.NewGuid();
        //
        //     mockDocument.Active.Returns(mockActive);
        //     mockActive.StreamIdentifier = "stream-id";
        //     mockDocument.ObjectName.Returns("object-name");
        //     mockActive.StreamConnectionName = "connection";
        //     mockDocument.ObjectId.Returns(objectId.ToString());
        //     mockDocument.TerminatedStreams.Returns([]);
        //
        //     mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
        //     mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
        //     mockContainerClient.GetBlobClient("tags/stream/stream-id.json").Returns(mockBlobClient);
        //
        //     mockBlobClient.ExistsAsync().Returns(Response.FromValue(true, Substitute.For<Response>()));
        //
        //     var etag = new ETag("test-etag");
        //     var mockProperties = Substitute.For<BlobProperties>();
        //     mockProperties.ETag.Returns(etag);
        //     var mockResponse = Substitute.For<Response<BlobProperties>>();
        //     mockResponse.Value.Returns(mockProperties);
        //     mockBlobClient.GetPropertiesAsync().Returns(mockResponse);
        //
        //     var existingDoc = new BlobDocumentTagStoreDocument
        //     {
        //         Tag = "existing-tag",
        //         ObjectIds = [existingObjectId.ToString()]
        //     };
        //
        //     // mockBlobClient.AsEntityAsync(
        //     //     Arg.Any<JsonTypeInfo<BlobDocumentTagStoreDocument>>(),
        //     //     Arg.Any<BlobRequestConditions>())
        //     //     .Returns((existingDoc, Substitute.For<Response>()));
        //
        //     // Act
        //     await sut.SetAsync(mockDocument, "test-tag");
        //
        //     // Assert
        //     // await mockBlobClient.Received(1).SaveEntityAsync(
        //     //     Arg.Is<BlobDocumentTagStoreDocument>(doc =>
        //     //         doc.ObjectIds.Contains(objectId) &&
        //     //         doc.ObjectIds.Contains(existingObjectId) &&
        //     //         doc.ObjectIds.Count == 2),
        //     //     Arg.Any<JsonTypeInfo<BlobDocumentTagStoreDocument>>(),
        //     //     Arg.Is<BlobRequestConditions>(cond => cond.IfMatch == etag));
        //     await mockBlobClient.Received(1).UploadAsync(
        //         Arg.Any<Stream>(),
        //         Arg.Is<BlobUploadOptions>(options =>
        //             options.Conditions == null || options.Conditions.IfMatch == null));
        //
        // }
        //
        // [Fact]
        // public async Task Should_not_add_duplicate_object_id_when_already_present()
        // {
        //     // Arrange
        //     var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", false);
        //     var mockDocument = Substitute.For<IObjectDocument>();
        //     var mockActive = Substitute.For<StreamInformation>();
        //     var objectId = Guid.NewGuid();
        //
        //     mockDocument.Active.Returns(mockActive);
        //     mockActive.StreamIdentifier = "stream-id";
        //     mockDocument.ObjectName.Returns("object-name");
        //     mockActive.StreamConnectionName = "connection";
        //     mockDocument.ObjectId.Returns(objectId.ToString());
        //     mockDocument.TerminatedStreams.Returns([]);
        //
        //     mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
        //     mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
        //     mockContainerClient.GetBlobClient("tags/stream/stream-id.json").Returns(mockBlobClient);
        //
        //     mockBlobClient.ExistsAsync().Returns(Response.FromValue(true, Substitute.For<Response>()));
        //
        //     var etag = new ETag("test-etag");
        //     var responseValue = Response.FromValue(
        //         BlobsModelFactory.BlobProperties(eTag: etag),
        //         Substitute.For<Response>()
        //     );
        //     mockBlobClient.GetPropertiesAsync().Returns(responseValue);
        //
        //     var existingDoc = new BlobDocumentTagStoreDocument
        //     {
        //         Tag = "existing-tag",
        //         ObjectIds = [objectId.ToString()] // Object ID already present
        //     };
        //
        //     // mockBlobClient.AsEntityAsync(
        //     //     Arg.Any<JsonTypeInfo<BlobDocumentTagStoreDocument>>(),
        //     //     Arg.Any<BlobRequestConditions>())
        //     //     .Returns((existingDoc, Substitute.For<Response>()));
        //
        //     // Act
        //     await sut.SetAsync(mockDocument, "test-tag");
        //
        //     // Assert
        //     // await mockBlobClient.Received(1).SaveEntityAsync(
        //     //     Arg.Is<BlobDocumentTagStoreDocument>(doc =>
        //     //         doc.ObjectIds.Contains(objectId) &&
        //     //         doc.ObjectIds.Count == 1), // Should still be 1
        //     //     Arg.Any<JsonTypeInfo<BlobDocumentTagStoreDocument>>(),
        //     //     Arg.Is<BlobRequestConditions>(cond => cond.IfMatch == etag));
        //
        // }
        //
        // [Fact]
        // public async Task Should_throw_blob_data_store_processing_exception_when_document_not_found()
        // {
        //     // Arrange
        //     var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", false);
        //     var mockDocument = Substitute.For<IObjectDocument>();
        //     var mockActive = Substitute.For<StreamInformation>();
        //
        //     mockDocument.Active.Returns(mockActive);
        //     mockActive.StreamIdentifier = "stream-id";
        //     mockDocument.ObjectName.Returns("object-name");
        //     mockActive.StreamConnectionName.Returns("connection");
        //     mockDocument.ObjectId.Returns(Guid.NewGuid().ToString());
        //     mockDocument.TerminatedStreams.Returns([]);
        //
        //     mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
        //     mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
        //     mockContainerClient.GetBlobClient("tags/stream/stream-id.json").Returns(mockBlobClient);
        //
        //     mockBlobClient.ExistsAsync().Returns(Response.FromValue(true, Substitute.For<Response>()));
        //
        //     var etag = new ETag("test-etag");
        //     var mockProperties = Substitute.For<BlobProperties>();
        //     mockProperties.ETag.Returns(etag);
        //     var mockResponse = Substitute.For<Response<BlobProperties>>();
        //     mockResponse.Value.Returns(mockProperties);
        //     mockBlobClient.GetPropertiesAsync().Returns(mockResponse);
        //
        //     // mockBlobClient.AsEntityAsync(
        //     //     Arg.Any<JsonTypeInfo<BlobDocumentTagStoreDocument>>(),
        //     //     Arg.Any<BlobRequestConditions>())
        //     //     .Returns(((BlobDocumentTagStoreDocument)null, Substitute.For<Response>()));
        //
        //     // Act & Assert
        //     var exception = await Assert.ThrowsAsync<BlobDataStoreProcessingException>(() =>
        //         sut.SetAsync(mockDocument, "test-tag"));
        //
        //     Assert.Contains("Unable to find tag document", exception.Message);
        // }

        [Fact]
        public async Task Should_create_container_when_auto_create_container_is_true()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = Substitute.For<IObjectDocument>();
            var mockActive = Substitute.For<StreamInformation>();

            mockDocument.Active.Returns(mockActive);
            mockActive.StreamIdentifier = "stream-id";
            mockDocument.TerminatedStreams.Returns([]);
            mockDocument.ObjectName.Returns("object-name");
            mockActive.StreamConnectionName = "connection";
            mockDocument.ObjectId.Returns(Guid.NewGuid().ToString());

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream/stream-id.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.SetAsync(mockDocument, "test-tag");

            // Assert
            mockContainerClient.Received(1).CreateIfNotExists();
        }

        // [Fact]
        // public async Task Should_not_create_container_when_auto_create_container_is_false()
        // {
        //     // Arrange
        //     var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", false);
        //     var mockDocument = Substitute.For<IObjectDocument>();
        //     var mockActive = Substitute.For<StreamInformation>();
        //
        //     mockDocument.Active.Returns(mockActive);
        //     mockActive.StreamIdentifier.Returns("stream-id");
        //     mockDocument.ObjectName.Returns("object-name");
        //     mockActive.StreamConnectionName.Returns("connection");
        //     mockDocument.ObjectId.Returns(Guid.NewGuid().ToString());
        //
        //     mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
        //     mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
        //     mockContainerClient.GetBlobClient("tags/stream/stream-id.json").Returns(mockBlobClient);
        //
        //     mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
        //
        //     // Act
        //     await sut.SetAsync(mockDocument, "test-tag");
        //
        //     // Assert
        //     mockContainerClient.DidNotReceive().CreateIfNotExists();
        // }
    }

    public class GetAsyncMethod : BlobStreamTagStoreTests
    {
        [Fact]
        public async Task Should_throw_not_implemented_exception()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);

            // Act & Assert
            await Assert.ThrowsAsync<NotImplementedException>(() =>
                sut.GetAsync("objectName", "tag"));
        }
    }

    public class CreateBlobClientMethod : BlobStreamTagStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = Substitute.For<IObjectDocument>();
            var mockActive = Substitute.For<StreamInformation>();

            mockDocument.Active.Returns(mockActive);
            mockActive.StreamIdentifier = "stream-id";
            mockDocument.ObjectName.Returns((string)null!);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SetAsync(mockDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_throw_document_configuration_exception_when_blob_client_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = Substitute.For<IObjectDocument>();
            var mockActive = Substitute.For<StreamInformation>();

            mockDocument.Active.Returns(mockActive);
            mockActive.StreamIdentifier = "stream-id";
            mockDocument.ObjectName.Returns("object-name");
            mockDocument.TerminatedStreams.Returns(new List<TerminatedStream>());
            mockActive.StreamConnectionName = "connection";
            mockDocument.ObjectId.Returns(Guid.NewGuid().ToString());

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream/stream-id.json").Returns((BlobClient)null!);

            // Act & Assert
            await Assert.ThrowsAsync<DocumentConfigurationException>(() =>
                sut.SetAsync(mockDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_use_lowercase_object_name_for_container()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = Substitute.For<IObjectDocument>();
            var mockActive = Substitute.For<StreamInformation>();

            mockDocument.Active.Returns(mockActive);
            mockActive.StreamIdentifier = "stream-id";
            mockDocument.ObjectName.Returns("UPPERCASE-NAME");
            mockDocument.TerminatedStreams.Returns([]);
            mockActive.StreamConnectionName = "connection";
            mockDocument.ObjectId.Returns(Guid.NewGuid().ToString());

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("uppercase-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream/stream-id.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.SetAsync(mockDocument, "test-tag");

            // Assert
            mockBlobServiceClient.Received(1).GetBlobContainerClient("uppercase-name");
        }
    }
}
