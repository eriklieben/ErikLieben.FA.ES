#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS0618 // Type or member is obsolete - tests verify deprecated properties

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

[JsonSerializable(typeof(BlobDocumentTagStoreDocument))]
public partial class BlobStreamTagStoreDocumentJsonContext : JsonSerializerContext
{
}

public class BlobStreamTagStoreTests
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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

        [Fact]
        public async Task Should_throw_argument_exception_when_tag_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(mockDocument, null!));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_tag_is_empty()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.SetAsync(mockDocument, ""));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_stream_identifier_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument(null!, "object-name", "connection");

            // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null values
            await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.SetAsync(mockDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_create_new_document_when_blob_does_not_exist()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", false);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.SetAsync(mockDocument, "test-tag");

            // Assert
            await mockBlobClient.Received(1).UploadAsync(
                Arg.Any<Stream>(),
                Arg.Any<BlobUploadOptions>());
        }

        [Fact]
        public async Task Should_add_stream_identifier_to_existing_document()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", false);
            var mockDocument = CreateMockDocument("new-stream-id", "object-name", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(true, Substitute.For<Response>()));

            var blobProperties = BlobsModelFactory.BlobProperties(eTag: new ETag("test-etag"));
            mockBlobClient.GetPropertiesAsync().Returns(Response.FromValue(blobProperties, Substitute.For<Response>()));

            var existingDoc = new BlobDocumentTagStoreDocument
            {
                Tag = "test-tag",
                ObjectIds = ["existing-stream-id"]
            };
            SetupBlobDownload(existingDoc);

            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.SetAsync(mockDocument, "test-tag");

            // Assert
            await mockBlobClient.Received(1).UploadAsync(
                Arg.Any<Stream>(),
                Arg.Is<BlobUploadOptions>(opts =>
                    opts.Conditions != null && opts.Conditions.IfMatch.HasValue));
        }

        [Fact]
        public async Task Should_handle_race_condition_when_blob_created_between_check_and_save()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", false);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));

            // First upload fails with 409 Conflict, second succeeds (fall through to update)
            var callCount = 0;
            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(_ =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw new RequestFailedException(409, "Conflict");
                    }
                    return Task.FromResult(Response.FromValue(blobContentInfo, Substitute.For<Response>()));
                });

            var blobProperties = BlobsModelFactory.BlobProperties(eTag: new ETag("test-etag"));
            mockBlobClient.GetPropertiesAsync().Returns(Response.FromValue(blobProperties, Substitute.For<Response>()));

            var existingDoc = new BlobDocumentTagStoreDocument
            {
                Tag = "test-tag",
                ObjectIds = []
            };
            SetupBlobDownload(existingDoc);

            // Act - should not throw, should fall through to update
            await sut.SetAsync(mockDocument, "test-tag");

            // Assert - should have called UploadAsync twice (first failed, second succeeded)
            await mockBlobClient.Received(2).UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>());
        }

        [Fact]
        public async Task Should_create_container_when_auto_create_container_is_true()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.SetAsync(mockDocument, "test-tag");

            // Assert
            mockContainerClient.Received(1).CreateIfNotExists();
        }

        [Fact]
        public async Task Should_not_create_container_when_auto_create_container_is_false()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", false);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.SetAsync(mockDocument, "test-tag");

            // Assert
            mockContainerClient.DidNotReceive().CreateIfNotExists();
        }

        [Fact]
        public async Task Should_sanitize_tag_name_for_filename()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", false);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            // Tag with invalid characters should be sanitized
            mockContainerClient.GetBlobClient("tags/stream-by-tag/testtag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.SetAsync(mockDocument, "test/tag");

            // Assert
            mockContainerClient.Received(1).GetBlobClient("tags/stream-by-tag/testtag.json");
        }
    }

    public class GetAsyncMethod : BlobStreamTagStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_exception_when_object_name_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);

            // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null values
            await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetAsync(null!, "tag"));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_tag_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);

            // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null values
            await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetAsync("objectName", null!));
        }

        [Fact]
        public async Task Should_return_empty_when_tag_does_not_exist()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);

            mockClientFactory.CreateClient("defaultConnection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("objectname").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            // Return null document (blob doesn't exist) - use proper BlobNotFound error code
            mockBlobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns<Task<Response>>(x => throw new RequestFailedException(404, "Not Found", BlobErrorCode.BlobNotFound.ToString(), null));

            // Act
            var result = await sut.GetAsync("objectName", "test-tag");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_return_stream_identifiers_when_tag_exists()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);

            mockClientFactory.CreateClient("defaultConnection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("objectname").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            var existingDoc = new BlobDocumentTagStoreDocument
            {
                Tag = "test-tag",
                ObjectIds = ["stream-1", "stream-2", "stream-3"]
            };
            SetupBlobDownload(existingDoc);

            // Act
            var result = await sut.GetAsync("objectName", "test-tag");

            // Assert
            var resultList = result.ToList();
            Assert.Equal(3, resultList.Count);
            Assert.Contains("stream-1", resultList);
            Assert.Contains("stream-2", resultList);
            Assert.Contains("stream-3", resultList);
        }

        [Fact]
        public async Task Should_use_lowercase_object_name()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);

            mockClientFactory.CreateClient("defaultConnection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("uppercase").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/tag.json").Returns(mockBlobClient);

            var existingDoc = new BlobDocumentTagStoreDocument
            {
                Tag = "tag",
                ObjectIds = []
            };
            SetupBlobDownload(existingDoc);

            // Act
            await sut.GetAsync("UPPERCASE", "tag");

            // Assert
            mockBlobServiceClient.Received(1).GetBlobContainerClient("uppercase");
        }
    }

    public class RemoveAsyncMethod : BlobStreamTagStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RemoveAsync(null!, "tag"));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_tag_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RemoveAsync(mockDocument, null!));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_tag_is_empty()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.RemoveAsync(mockDocument, ""));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_stream_identifier_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument(null!, "object-name", "connection");

            // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null values
            await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.RemoveAsync(mockDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_return_early_when_blob_does_not_exist()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));

            // Act
            await sut.RemoveAsync(mockDocument, "test-tag");

            // Assert - should not call any other methods
            await mockBlobClient.DidNotReceive().GetPropertiesAsync();
        }

        [Fact]
        public async Task Should_delete_blob_when_no_streams_remain()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(true, Substitute.For<Response>()));

            var blobProperties = BlobsModelFactory.BlobProperties(eTag: new ETag("test-etag"));
            mockBlobClient.GetPropertiesAsync().Returns(Response.FromValue(blobProperties, Substitute.For<Response>()));

            var existingDoc = new BlobDocumentTagStoreDocument
            {
                Tag = "test-tag",
                ObjectIds = ["stream-id"]
            };
            SetupBlobDownload(existingDoc);

            // Act
            await sut.RemoveAsync(mockDocument, "test-tag");

            // Assert
            await mockBlobClient.Received(1).DeleteIfExistsAsync(
                Arg.Any<DeleteSnapshotsOption>(),
                Arg.Is<BlobRequestConditions>(c => c.IfMatch.HasValue),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_update_blob_when_other_streams_remain()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(true, Substitute.For<Response>()));

            var blobProperties = BlobsModelFactory.BlobProperties(eTag: new ETag("test-etag"));
            mockBlobClient.GetPropertiesAsync().Returns(Response.FromValue(blobProperties, Substitute.For<Response>()));

            var existingDoc = new BlobDocumentTagStoreDocument
            {
                Tag = "test-tag",
                ObjectIds = ["stream-id", "other-stream-id"]
            };
            SetupBlobDownload(existingDoc);

            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.RemoveAsync(mockDocument, "test-tag");

            // Assert
            await mockBlobClient.Received(1).UploadAsync(
                Arg.Any<Stream>(),
                Arg.Is<BlobUploadOptions>(opts =>
                    opts.Conditions != null &&
                    opts.Conditions.IfMatch.HasValue));
        }

        [Fact]
        public async Task Should_return_early_when_document_is_null_after_download()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(true, Substitute.For<Response>()));

            var blobProperties = BlobsModelFactory.BlobProperties(eTag: new ETag("test-etag"));
            mockBlobClient.GetPropertiesAsync().Returns(Response.FromValue(blobProperties, Substitute.For<Response>()));

            // Simulate blob not found during download (race condition or deleted between exists check and download)
            mockBlobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns<Task<Response>>(x => throw new RequestFailedException(404, "Not Found", BlobErrorCode.BlobNotFound.ToString(), null));

            // Act
            await sut.RemoveAsync(mockDocument, "test-tag");

            // Assert - should not try to delete or upload since document was null
            await mockBlobClient.DidNotReceive().DeleteIfExistsAsync(
                Arg.Any<DeleteSnapshotsOption>(),
                Arg.Any<BlobRequestConditions>(),
                Arg.Any<CancellationToken>());
            await mockBlobClient.DidNotReceive().UploadAsync(
                Arg.Any<Stream>(),
                Arg.Any<BlobUploadOptions>());
        }
    }

    public class CreateBlobClientMethod : BlobStreamTagStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_exception_when_object_name_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", null!, "connection");

            // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null values
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                sut.SetAsync(mockDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_throw_document_configuration_exception_when_blob_client_is_null()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", "object-name", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns((BlobClient)null!);

            // Act & Assert
            await Assert.ThrowsAsync<DocumentConfigurationException>(() =>
                sut.SetAsync(mockDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_use_lowercase_object_name_for_container()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = CreateMockDocument("stream-id", "UPPERCASE-NAME", "connection");

            mockClientFactory.CreateClient("connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("uppercase-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.SetAsync(mockDocument, "test-tag");

            // Assert
            mockBlobServiceClient.Received(1).GetBlobContainerClient("uppercase-name");
        }

        [Fact]
        public async Task Should_use_data_store_connection_when_available()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = Substitute.For<IObjectDocument>();
            var mockActive = Substitute.For<StreamInformation>();

            mockDocument.Active.Returns(mockActive);
            mockActive.StreamIdentifier = "stream-id";
            mockDocument.ObjectName.Returns("object-name");
            mockDocument.TerminatedStreams.Returns([]);
            mockActive.DataStore = "data-store-connection";
            mockActive.StreamConnectionName = "stream-connection";
            mockDocument.ObjectId.Returns(Guid.NewGuid().ToString());

            mockClientFactory.CreateClient("data-store-connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.SetAsync(mockDocument, "test-tag");

            // Assert
            mockClientFactory.Received(1).CreateClient("data-store-connection");
        }

        [Fact]
        public async Task Should_fallback_to_stream_connection_name_when_data_store_is_empty()
        {
            // Arrange
            var sut = new BlobStreamTagStore(mockClientFactory, "defaultConnection", true);
            var mockDocument = Substitute.For<IObjectDocument>();
            var mockActive = Substitute.For<StreamInformation>();

            mockDocument.Active.Returns(mockActive);
            mockActive.StreamIdentifier = "stream-id";
            mockDocument.ObjectName.Returns("object-name");
            mockDocument.TerminatedStreams.Returns([]);
            mockActive.DataStore = "";
            mockActive.StreamConnectionName = "stream-connection";
            mockDocument.ObjectId.Returns(Guid.NewGuid().ToString());

            mockClientFactory.CreateClient("stream-connection").Returns(mockBlobServiceClient);
            mockBlobServiceClient.GetBlobContainerClient("object-name").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient("tags/stream-by-tag/test-tag.json").Returns(mockBlobClient);

            mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
            var blobContentInfo = Substitute.For<BlobContentInfo>();
            mockBlobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(Response.FromValue(blobContentInfo, Substitute.For<Response>()));

            // Act
            await sut.SetAsync(mockDocument, "test-tag");

            // Assert
            mockClientFactory.Received(1).CreateClient("stream-connection");
        }
    }

    private static IObjectDocument CreateMockDocument(string streamIdentifier, string objectName, string connectionName)
    {
        var mockDocument = Substitute.For<IObjectDocument>();
        var mockActive = Substitute.For<StreamInformation>();

        mockDocument.Active.Returns(mockActive);
        mockActive.StreamIdentifier = streamIdentifier;
        mockDocument.ObjectName.Returns(objectName);
        mockDocument.TerminatedStreams.Returns([]);
        mockActive.StreamConnectionName = connectionName;
        mockDocument.ObjectId.Returns(Guid.NewGuid().ToString());

        return mockDocument;
    }

    private void SetupBlobDownload(BlobDocumentTagStoreDocument doc)
    {
        var jsonData = JsonSerializer.Serialize(doc, CamelCaseOptions);
        var jsonBytes = Encoding.UTF8.GetBytes(jsonData);

        mockBlobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
            .Returns(Task.FromResult(Substitute.For<Response>()))
            .AndDoes(callInfo =>
            {
                var stream = callInfo.Arg<MemoryStream>();
                stream.Write(jsonBytes, 0, jsonBytes.Length);
                stream.Position = 0;
            });
    }
}
