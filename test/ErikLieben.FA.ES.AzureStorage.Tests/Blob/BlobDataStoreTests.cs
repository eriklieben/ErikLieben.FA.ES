#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS0618 // Type or member is obsolete - testing deprecated API intentionally

using System;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

public class BlobDataStoreTests
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly BlobServiceClient blobServiceClient;
    private readonly BlobContainerClient containerClient;
    private readonly BlobClient blobClient;
    private readonly IObjectDocument objectDocument;
    private readonly IEvent[] events;

    public BlobDataStoreTests()
    {
        clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
        blobServiceClient = Substitute.For<BlobServiceClient>();
        containerClient = Substitute.For<BlobContainerClient>();
        blobClient = Substitute.For<BlobClient>();
        objectDocument = Substitute.For<IObjectDocument>();
        var streamInformation1 = Substitute.For<StreamInformation>();
        events = [Substitute.For<IEvent>()];

        // Setup default stream information
        streamInformation1.StreamIdentifier = "test-stream";
        streamInformation1.StreamConnectionName = "test-connection";
        streamInformation1.ChunkSettings = new StreamChunkSettings
        {
            EnableChunks = false
        };

        // Setup default object document
        objectDocument.Active.Returns(streamInformation1);
        objectDocument.ObjectName.Returns("TestObject");
        objectDocument.ObjectId.Returns("test-id");
        objectDocument.TerminatedStreams.Returns([]);

        // Setup blob client chain
        clientFactory.CreateClient("test-connection").Returns(blobServiceClient);
        blobServiceClient.GetBlobContainerClient("testobject").Returns(containerClient);
        containerClient.GetBlobClient(Arg.Any<string>()).Returns(blobClient);
    }

    public class Constructor : BlobDataStoreTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_client_factory_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BlobDataStore(null!, false));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            // Act
            var sut = new BlobDataStore(clientFactory, true);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class ReadAsync : BlobDataStoreTests
    {
        [Fact]
        public async Task Should_return_null_when_blob_document_is_null()
        {
            // Arrange
            var sut = new BlobDataStore(clientFactory, false);
            var requestFailedException =
                new RequestFailedException(404, "Not found", BlobErrorCode.BlobNotFound.ToString(), null);
            blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
                .ThrowsAsync(requestFailedException);

            // Act
            var result = await sut.ReadAsync(objectDocument);

            // Assert
            Assert.Null(result);
        }

        // [Fact]
        // public async Task Should_throw_blob_document_store_container_not_found_exception_when_container_not_found()
        // {
        //     // Arrange
        //     var sut = new BlobDataStore(clientFactory, false);
        //     var requestFailedException =
        //         new RequestFailedException(404, "Container not found", "ContainerNotFound", null);
        //     blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
        //         .ThrowsAsync(requestFailedException);
        //
        //     // Act & Assert
        //     await Assert.ThrowsAsync<BlobDocumentStoreContainerNotFoundException>(() => sut.ReadAsync(objectDocument));
        // }

        // [Fact]
        // public async Task Should_return_filtered_events_when_blob_document_exists()
        // {
        //     // Arrange
        //     var sut = new BlobDataStore(clientFactory, false);
        //     var testDocument = new BlobDataStoreDocument
        //     {
        //         ObjectId = "test-id",
        //         ObjectName = "TestObject",
        //         LastObjectDocumentHash = "*"
        //     };
        //     testDocument.Events.AddRange(new[]
        //     {
        //         new BlobJsonEvent { EventVersion = 1, Timestamp = DateTime.Now, EventType = ""},
        //         new BlobJsonEvent { EventVersion = 2, Timestamp = DateTime.Now, EventType = ""},
        //         new BlobJsonEvent { EventVersion = 3, Timestamp = DateTime.Now, EventType = ""}
        //     });
        //
        //     var json = JsonSerializer.Serialize(testDocument);
        //     var jsonBytes = Encoding.UTF8.GetBytes(json);
        //
        //     blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
        //         .Returns(Response.FromValue(BlobsModelFactory.BlobDownloadInfo(), Substitute.For<Response>()))
        //         .AndDoes(callInfo =>
        //         {
        //             var stream = callInfo.Arg<Stream>();
        //             stream.Write(jsonBytes, 0, jsonBytes.Length);
        //             stream.Position = 0;
        //         });
        //
        //     // Act
        //     var result = await sut.ReadAsync(objectDocument, 1, 2);
        //
        //     // Assert
        //     Assert.NotNull(result);
        //     var eventsList = result.ToList();
        //     Assert.Equal(2, eventsList.Count);
        //     Assert.All(eventsList, e => Assert.True(e.EventVersion >= 1 && e.EventVersion <= 2));
        // }

        // [Fact]
        // public async Task Should_use_chunked_path_when_chunking_enabled()
        // {
        //     // Arrange
        //     var sut = new BlobDataStore(clientFactory, false);
        //     streamInformation.ChunkingEnabled().Returns(true);
        //
        //     var requestFailedException =
        //         new RequestFailedException(404, "Not found", BlobErrorCode.BlobNotFound.ToString(), null);
        //     blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
        //         .ThrowsAsync(requestFailedException);
        //
        //     // Act
        //     await sut.ReadAsync(objectDocument, chunk: 5);
        //
        //     // Assert
        //     containerClient.Received(1).GetBlobClient("test-stream-0000000005.json");
        // }

        [Fact]
        public async Task Should_use_non_chunked_path_when_chunking_disabled()
        {
            // Arrange
            var sut = new BlobDataStore(clientFactory, false);
            var requestFailedException =
                new RequestFailedException(404, "Not found", BlobErrorCode.BlobNotFound.ToString(), null);
            blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
                .ThrowsAsync(requestFailedException);

            // Act
            await sut.ReadAsync(objectDocument);

            // Assert
            containerClient.Received(1).GetBlobClient("test-stream.json");
        }

        // [Fact]
        // public async Task Should_return_all_events_when_no_version_filters_specified()
        // {
        //     // Arrange
        //     var sut = new BlobDataStore(clientFactory, false);
        //     var testDocument = new BlobDataStoreDocument();
        //     testDocument.Events.AddRange(new[]
        //     {
        //         new BlobJsonEvent { EventVersion = 1 },
        //         new BlobJsonEvent { EventVersion = 2 },
        //         new BlobJsonEvent { EventVersion = 3 }
        //     });
        //
        //     var json = JsonSerializer.Serialize(testDocument,
        //         BlobDataStoreDocumentContext.Default.BlobDataStoreDocument);
        //     var jsonBytes = Encoding.UTF8.GetBytes(json);
        //
        //     blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
        //         .Returns(Response.FromValue(BlobsModelFactory.BlobDownloadInfo(), Substitute.For<Response>()))
        //         .AndDoes(callInfo =>
        //         {
        //             var stream = callInfo.Arg<Stream>();
        //             stream.Write(jsonBytes, 0, jsonBytes.Length);
        //             stream.Position = 0;
        //         });
        //
        //     // Act
        //     var result = await sut.ReadAsync(objectDocument);
        //
        //     // Assert
        //     Assert.NotNull(result);
        //     var eventsList = result.ToList();
        //     Assert.Equal(3, eventsList.Count);
        // }

        // [Fact]
        // public async Task Should_filter_events_by_start_version_only()
        // {
        //     // Arrange
        //     var sut = new BlobDataStore(clientFactory, false);
        //     var testDocument = new BlobDataStoreDocument();
        //     testDocument.Events.AddRange(new[]
        //     {
        //         new BlobJsonEvent { EventVersion = 1 },
        //         new BlobJsonEvent { EventVersion = 2 },
        //         new BlobJsonEvent { EventVersion = 3 }
        //     });
        //
        //     var json = JsonSerializer.Serialize(testDocument,
        //         BlobDataStoreDocumentContext.Default.BlobDataStoreDocument);
        //     var jsonBytes = Encoding.UTF8.GetBytes(json);
        //
        //     blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
        //         .Returns(Response.FromValue(BlobsModelFactory.BlobDownloadInfo(), Substitute.For<Response>()))
        //         .AndDoes(callInfo =>
        //         {
        //             var stream = callInfo.Arg<Stream>();
        //             stream.Write(jsonBytes, 0, jsonBytes.Length);
        //             stream.Position = 0;
        //         });
        //
        //     // Act
        //     var result = await sut.ReadAsync(objectDocument, 2);
        //
        //     // Assert
        //     Assert.NotNull(result);
        //     var eventsList = result.ToList();
        //     Assert.Equal(2, eventsList.Count);
        //     Assert.All(eventsList, e => Assert.True(e.EventVersion >= 2));
        // }
    }

    public class AppendAsync : BlobDataStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            // Arrange
            var sut = new BlobDataStore(clientFactory, false);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.AppendAsync(null!, events));
        }

        // [Fact]
        // public async Task Should_throw_argument_null_exception_when_stream_identifier_is_null()
        // {
        //     // Arrange
        //     var sut = new BlobDataStore(clientFactory, false);
        //     streamInformation.StreamIdentifier.Returns((string?)null);
        //
        //     // Act & Assert
        //     await Assert.ThrowsAsync<ArgumentNullException>(() => sut.AppendAsync(objectDocument, events));
        // }

        [Fact]
        public async Task Should_throw_argument_exception_when_no_events_provided()
        {
            // Arrange
            var sut = new BlobDataStore(clientFactory, false);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.AppendAsync(objectDocument, Array.Empty<IEvent>()));
        }

        [Fact]
        public async Task Should_create_new_document_when_blob_does_not_exist()
        {
            // Arrange
            var sut = new BlobDataStore(clientFactory, false);
            blobClient.ExistsAsync().Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));

            var uploadResponse = Response.FromValue(
                BlobsModelFactory.BlobContentInfo(new ETag("test-etag"), DateTimeOffset.Now, null, null, "test-hash",
                    1),
                Substitute.For<Response>());
            blobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(uploadResponse);

            // Act
            await sut.AppendAsync(objectDocument, events);

            // Assert
            await blobClient.Received(1).UploadAsync(
                Arg.Any<Stream>(),
                Arg.Is<BlobUploadOptions>(o => o.Conditions != null && o.Conditions.IfNoneMatch.HasValue));
        }

        // [Fact]
        // public async Task Should_update_existing_document_when_blob_exists()
        // {
        //     // Arrange
        //     var sut = new BlobDataStore(clientFactory, false);
        //     var existingDocument = new BlobDataStoreDocument
        //     {
        //         ObjectId = "test-id",
        //         ObjectName = "TestObject",
        //         LastObjectDocumentHash = "*"
        //     };
        //
        //     var etag = new ETag("test-etag");
        //     var properties = BlobsModelFactory.BlobProperties(etag: etag);
        //     var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
        //
        //     blobClient.ExistsAsync().Returns(true);
        //     blobClient.GetPropertiesAsync().Returns(propertiesResponse);
        //
        //     var json = JsonSerializer.Serialize(existingDocument,
        //         BlobDataStoreDocumentContext.Default.BlobDataStoreDocument);
        //     var jsonBytes = Encoding.UTF8.GetBytes(json);
        //
        //     blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Is<BlobRequestConditions>(c => c.IfMatch == etag))
        //         .Returns(Response.FromValue(BlobsModelFactory.BlobDownloadInfo(), Substitute.For<Response>()))
        //         .AndDoes(callInfo =>
        //         {
        //             var stream = callInfo.Arg<Stream>();
        //             stream.Write(jsonBytes, 0, jsonBytes.Length);
        //             stream.Position = 0;
        //         });
        //
        //     var uploadResponse = Response.FromValue(
        //         BlobsModelFactory.BlobContentInfo(new ETag("new-etag"), DateTimeOffset.Now, null, null, "test-hash", 1),
        //         Substitute.For<Response>());
        //     blobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
        //         .Returns(uploadResponse);
        //
        //     // Act
        //     await sut.AppendAsync(objectDocument, events);
        //
        //     // Assert
        //     await blobClient.Received(1).UploadAsync(
        //         Arg.Any<Stream>(),
        //         Arg.Is<BlobUploadOptions>(o => o.Conditions != null && o.Conditions.IfMatch == etag));
        // }

        // [Fact]
        // public async Task
        //     Should_throw_blob_data_store_processing_exception_when_document_not_found_after_properties_check()
        // {
        //     // Arrange
        //     var sut = new BlobDataStore(clientFactory, false);
        //     var etag = new ETag("test-etag");
        //     var properties = BlobsModelFactory.BlobProperties(etag: etag);
        //     var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
        //
        //     blobClient.ExistsAsync().Returns(true);
        //     blobClient.GetPropertiesAsync().Returns(propertiesResponse);
        //
        //     var requestFailedException =
        //         new RequestFailedException(404, "Not found", BlobErrorCode.BlobNotFound.ToString(), null);
        //     blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Is<BlobRequestConditions>(c => c.IfMatch == etag))
        //         .ThrowsAsync(requestFailedException);
        //
        //     // Act & Assert
        //     await Assert.ThrowsAsync<BlobDataStoreProcessingException>(() => sut.AppendAsync(objectDocument, events));
        // }
        //
        // [Fact]
        // public async Task Should_use_chunked_path_when_chunking_enabled()
        // {
        //     // Arrange
        //     var sut = new BlobDataStore(clientFactory, false);
        //     streamInformation.ChunkingEnabled().Returns(true);
        //
        //     var chunk = Substitute.For<IStreamChunk>();
        //     chunk.ChunkIdentifier.Returns(10);
        //     var chunks = new List<IStreamChunk> { chunk };
        //     streamInformation.StreamChunks.Returns(chunks);
        //
        //     blobClient.ExistsAsync().Returns(false);
        //
        //     var uploadResponse = Response.FromValue(
        //         BlobsModelFactory.BlobContentInfo(new ETag("test-etag"), DateTimeOffset.Now, null, null, "test-hash",
        //             1),
        //         Substitute.For<Response>());
        //     blobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
        //         .Returns(uploadResponse);
        //
        //     // Act
        //     await sut.AppendAsync(objectDocument, events);
        //
        //     // Assert
        //     containerClient.Received(1).GetBlobClient("test-stream-0000000010.json");
        // }

        [Fact]
        public async Task Should_use_non_chunked_path_when_chunking_disabled()
        {
            // Arrange
            var sut = new BlobDataStore(clientFactory, false);
            blobClient.ExistsAsync().Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));;

            var uploadResponse = Response.FromValue(
                BlobsModelFactory.BlobContentInfo(new ETag("test-etag"), DateTimeOffset.Now, null, null, "test-hash",
                    1),
                Substitute.For<Response>());
            blobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(uploadResponse);

            // Act
            await sut.AppendAsync(objectDocument, events);

            // Assert
            containerClient.Received(1).GetBlobClient("test-stream.json");
        }
    }

    public class CreateBlobClient : BlobDataStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            // Arrange
            var sut = new BlobDataStore(clientFactory, false);
            objectDocument.ObjectName.Returns((string?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReadAsync(objectDocument));
        }

        [Fact]
        public async Task Should_create_container_when_auto_create_is_enabled()
        {
            // Arrange
            var sut = new BlobDataStore(clientFactory, true);
            var requestFailedException =
                new RequestFailedException(404, "Not found", BlobErrorCode.BlobNotFound.ToString(), null);
            blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
                .ThrowsAsync(requestFailedException);

            // Act
            await sut.ReadAsync(objectDocument);

            // Assert
            containerClient.Received(1).CreateIfNotExists();
        }

        [Fact]
        public async Task Should_not_create_container_when_auto_create_is_disabled()
        {
            // Arrange
            var sut = new BlobDataStore(clientFactory, false);
            var requestFailedException =
                new RequestFailedException(404, "Not found", BlobErrorCode.BlobNotFound.ToString(), null);
            blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
                .ThrowsAsync(requestFailedException);

            // Act
            await sut.ReadAsync(objectDocument);

            // Assert
            containerClient.DidNotReceive().CreateIfNotExists();
        }

        [Fact]
        public async Task Should_throw_document_configuration_exception_when_blob_client_is_null()
        {
            // Arrange
            var sut = new BlobDataStore(clientFactory, false);
            containerClient.GetBlobClient(Arg.Any<string>()).Returns((BlobClient?)null);

            // Act & Assert
            await Assert.ThrowsAsync<DocumentConfigurationException>(() => sut.ReadAsync(objectDocument));
        }

        [Fact]
        public async Task Should_use_lowercase_object_name_for_container()
        {
            // Arrange
            var sut = new BlobDataStore(clientFactory, false);
            objectDocument.ObjectName.Returns("TestObject");
            var requestFailedException =
                new RequestFailedException(404, "Not found", BlobErrorCode.BlobNotFound.ToString(), null);
            blobClient.DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
                .ThrowsAsync(requestFailedException);

            // Act
            await sut.ReadAsync(objectDocument);

            // Assert
            blobServiceClient.Received(1).GetBlobContainerClient("testobject");
        }
    }
}
