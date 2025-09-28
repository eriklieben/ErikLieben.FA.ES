using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

public class BlobDocumentTagStoreTests
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly BlobServiceClient blobServiceClient;
    private readonly BlobContainerClient blobContainerClient;
    private readonly BlobClient blobClient;
    private readonly IObjectDocument objectDocument;
    private readonly StreamInformation streamInformation;
    private readonly string defaultDocumentTagType = "test-tag-type";
    private readonly string defaultConnectionName = "test-connection";
    private readonly bool autoCreateContainer = true;

    protected BlobDocumentTagStoreTests()
    {
        clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
        blobServiceClient = Substitute.For<BlobServiceClient>();
        blobContainerClient = Substitute.For<BlobContainerClient>();
        blobClient = Substitute.For<BlobClient>();
        objectDocument = Substitute.For<IObjectDocument>();
        streamInformation = Substitute.For<StreamInformation>();

        clientFactory.CreateClient(Arg.Any<string>()).Returns(blobServiceClient);
        blobServiceClient.GetBlobContainerClient(Arg.Any<string>()).Returns(blobContainerClient);
        blobContainerClient.GetBlobClient(Arg.Any<string>()).Returns(blobClient);

        objectDocument.ObjectId.Returns("test-object-id");
        objectDocument.ObjectName.Returns("test-object-name");
        objectDocument.Active.Returns(streamInformation);
        streamInformation.StreamIdentifier = "test-stream-id";
        streamInformation.DocumentTagConnectionName = "test-connection";
    }

    public class Constructor : BlobDocumentTagStoreTests
    {
        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Act
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_client_factory_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BlobDocumentTagStore(
                null!,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer));
        }
    }

    public class SetAsync : BlobDocumentTagStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(null!, "test-tag"));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_stream_identifier_is_null()
        {
            // Arrange
            streamInformation.StreamIdentifier = null!;
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(objectDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_stream_identifier_is_empty()
        {
            // Arrange
            streamInformation.StreamIdentifier = string.Empty;
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.SetAsync(objectDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_stream_identifier_is_whitespace()
        {
            // Arrange
            streamInformation.StreamIdentifier = "   ";
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.SetAsync(objectDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_object_name_is_null()
        {
            // Arrange
            objectDocument.ObjectName.Returns((string)null!);
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(objectDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_object_name_is_empty()
        {
            // Arrange
            objectDocument.ObjectName.Returns(string.Empty);
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.SetAsync(objectDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_object_name_is_whitespace()
        {
            // Arrange
            objectDocument.ObjectName.Returns("   ");
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.SetAsync(objectDocument, "test-tag"));
        }

        [Fact]
        public async Task Should_create_new_document_when_blob_does_not_exist()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            blobClient.ExistsAsync().Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));

            // Mock UploadAsync for saving new document
            var contentInfo = BlobsModelFactory.BlobContentInfo(
                new ETag("new-etag"),
                DateTimeOffset.UtcNow,
                [],
                "1.0",
                null,
                null,
                0
            );
            var uploadResponse = Substitute.For<Response<BlobContentInfo>>();
            uploadResponse.Value.Returns(contentInfo);
            blobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(uploadResponse);

            // Act
            await sut.SetAsync(objectDocument, "test-tag");

            // Assert - Just verify UploadAsync was called with correct conditions
            await blobClient.Received(1).UploadAsync(
                Arg.Any<Stream>(),
                Arg.Is<BlobUploadOptions>(opts =>
                    opts.Conditions != null &&
                    opts.Conditions.IfNoneMatch.HasValue &&
                    opts.HttpHeaders.ContentType == "application/json"));
        }


        [Fact]
        public async Task Should_sanitize_tag_for_filename()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            blobClient.ExistsAsync()
                .Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));

            // Mock UploadAsync for saving new document
            var contentInfo = BlobsModelFactory.BlobContentInfo(
                new ETag("new-etag"),
                DateTimeOffset.UtcNow,
                [],
                "1.0",
                null,
                null,
                0
            );
            var uploadResponse = Substitute.For<Response<BlobContentInfo>>();
            uploadResponse.Value.Returns(contentInfo);
            blobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(uploadResponse);

            // Act
            await sut.SetAsync(objectDocument, "test/tag\\with*invalid?chars");

            // Assert - Verify the correct sanitized filename was used
            blobContainerClient.Received(1).GetBlobClient("tags/document/testtagwithinvalidchars.json");
        }



        [Fact]
        public async Task Should_not_duplicate_object_id_when_already_exists()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            var existingDoc = new BlobDocumentTagStoreDocument
            {
                Tag = "test-tag",
                ObjectIds = ["test-object-id"]
            };

            var blobProperties = BlobsModelFactory.BlobProperties(eTag: new ETag("test-etag"));
            var response = Response.FromValue(blobProperties, Substitute.For<Response>());

            blobClient.ExistsAsync().Returns(Task.FromResult(Response.FromValue(true, Substitute.For<Response>())));
            blobClient.GetPropertiesAsync().Returns(Task.FromResult(response));

            // Mock DownloadToAsync instead of AsEntityAsync
            var jsonData = JsonSerializer.Serialize(existingDoc, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true
            });
            var jsonBytes = Encoding.UTF8.GetBytes(jsonData);

            blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns(Task.FromResult(Substitute.For<Response>()))
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<MemoryStream>();
                    stream.Write(jsonBytes, 0, jsonBytes.Length);
                    stream.Position = 0;
                });

            var contentInfo = BlobsModelFactory.BlobContentInfo(
                new ETag("etag"),
                DateTimeOffset.UtcNow,
                [],
                "1.0",
                null,
                null,
                0
            );
            var responseUpload = Substitute.For<Response<BlobContentInfo>>();
            responseUpload.Value.Returns(contentInfo);

            blobClient
                .UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(responseUpload);

            // Act
            await sut.SetAsync(objectDocument, "test-tag");

            // Assert
            await blobClient.Received(1).UploadAsync(
                Arg.Is<Stream>(s => VerifyUploadedJson(s, "test-tag", "test-object-id")),
                Arg.Is<BlobUploadOptions>(opts =>
                    opts.Conditions != null &&
                    opts.Conditions.IfMatch.HasValue &&
                    opts.HttpHeaders.ContentType == "application/json"));
        }

        private bool VerifyUploadedJson(Stream stream, string expectedTag, string expectedObjectId)
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var doc = JsonSerializer.Deserialize<BlobDocumentTagStoreDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return doc != null &&
                   doc.Tag == expectedTag &&
                   doc.ObjectIds.Count == 1 &&
                   doc.ObjectIds[0] == expectedObjectId;
        }

        [Fact]
        public async Task Should_throw_blob_data_store_processing_exception_when_document_not_found()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            // first return that it exists
            blobClient.ExistsAsync().Returns(Task.FromResult(Response.FromValue(true, Substitute.For<Response>())));

            // then in next calls it won't exist anymore
            var blobProperties = BlobsModelFactory.BlobProperties(eTag: new ETag("test-etag"));
            blobClient.GetPropertiesAsync().Returns(
                Task.FromResult(Response.FromValue(blobProperties, Substitute.For<Response>())));

            blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns<Task<Response>>(_ =>
                    throw new RequestFailedException(404, "BlobNotFound", BlobErrorCode.BlobNotFound.ToString(),
                        null));

            // Act & Assert
            var exception =
                await Assert.ThrowsAsync<BlobDataStoreProcessingException>(() =>
                    sut.SetAsync(objectDocument, "test-tag"));
            Assert.Equal(
                "[ELFAES-EXT-0001] Unable to find tag document 'test-object-name/tags/document/test-tag.json' while processing save.",
                exception.Message);
        }

        [Fact]
        public async Task Should_create_container_when_auto_create_is_true()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                true);

            blobClient.ExistsAsync().Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));
            var contentInfo = BlobsModelFactory.BlobContentInfo(
                new ETag("etag"),
                DateTimeOffset.UtcNow,
                [],
                "1.0",
                null,
                null,
                0
            );
            var responseUpload = Substitute.For<Response<BlobContentInfo>>();
            responseUpload.Value.Returns(contentInfo);

            byte[]? capturedStreamData = null;
            blobClient
                .UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(responseUpload)
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<Stream>();
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        capturedStreamData = memoryStream.ToArray();
                    }

                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                    }
                });

            blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns(Task.FromResult(Substitute.For<Response>()))
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<MemoryStream>();
                    stream.Write(capturedStreamData!, 0, capturedStreamData!.Length);
                    stream.Position = 0;
                });

            // Act
            await sut.SetAsync(objectDocument, "test-tag");

            // Assert
            blobContainerClient.Received(1).CreateIfNotExists();
        }

        [Fact]
        public async Task Should_not_create_container_when_auto_create_is_false()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                false);

            blobClient.ExistsAsync().Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));
            var contentInfo = BlobsModelFactory.BlobContentInfo(
                new ETag("etag"),
                DateTimeOffset.UtcNow,
                [],
                "1.0",
                null,
                null,
                0
            );
            var responseUpload = Substitute.For<Response<BlobContentInfo>>();
            responseUpload.Value.Returns(contentInfo);

            byte[]? capturedStreamData = null;
            blobClient
                .UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(responseUpload)
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<Stream>();
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        capturedStreamData = memoryStream.ToArray();
                    }

                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                    }
                });

            blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns(Task.FromResult(Substitute.For<Response>()))
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<MemoryStream>();
                    stream.Write(capturedStreamData!, 0, capturedStreamData!.Length);
                    stream.Position = 0;
                });


            // Act
            await sut.SetAsync(objectDocument, "test-tag");

            // Assert
            blobContainerClient.DidNotReceive().CreateIfNotExists();
        }

        [Fact]
        public async Task Should_use_document_tag_connection_name()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            blobClient.ExistsAsync().Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));

            var contentInfo = BlobsModelFactory.BlobContentInfo(
                new ETag("etag"),
                DateTimeOffset.UtcNow,
                [],
                "1.0",
                null,
                null,
                0
            );
            var responseUpload = Substitute.For<Response<BlobContentInfo>>();
            responseUpload.Value.Returns(contentInfo);

            byte[]? capturedStreamData = null;
            blobClient
                .UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(responseUpload)
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<Stream>();
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        capturedStreamData = memoryStream.ToArray();
                    }

                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                    }
                });

            blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns(Task.FromResult(Substitute.For<Response>()))
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<MemoryStream>();
                    stream.Write(capturedStreamData!, 0, capturedStreamData!.Length);
                    stream.Position = 0;
                });


            // Act
            await sut.SetAsync(objectDocument, "test-tag");

            // Assert
            clientFactory.Received(1).CreateClient("test-connection");
        }

        [Fact]
        public async Task Should_use_lowercase_object_name_for_container()
        {
            // Arrange
            objectDocument.ObjectName.Returns("TEST-OBJECT-NAME");
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            blobClient.ExistsAsync().Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));

            var contentInfo = BlobsModelFactory.BlobContentInfo(
                new ETag("etag"),
                DateTimeOffset.UtcNow,
                [],
                "1.0",
                null,
                null,
                0
            );
            var responseUpload = Substitute.For<Response<BlobContentInfo>>();
            responseUpload.Value.Returns(contentInfo);

            byte[]? capturedStreamData = null;
            blobClient
                .UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(responseUpload)
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<Stream>();
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        capturedStreamData = memoryStream.ToArray();
                    }

                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                    }
                });

            blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns(Task.FromResult(Substitute.For<Response>()))
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<MemoryStream>();
                    stream.Write(capturedStreamData!, 0, capturedStreamData!.Length);
                    stream.Position = 0;
                });

            // Act
            await sut.SetAsync(objectDocument, "test-tag");

            // Assert
            blobServiceClient.Received(1).GetBlobContainerClient("test-object-name");
        }
    }

    public class GetAsync : BlobDocumentTagStoreTests
    {
        [Fact]
        public async Task Should_return_object_ids_when_document_exists()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            var capturedStreamData =
                "{ \"tag\": \"test\", \"objectIds\": [\"object-id-1\",\"object-id-2\"]}"u8.ToArray();
            blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns(Task.FromResult(Substitute.For<Response>()))
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<MemoryStream>();
                    stream.Write(capturedStreamData, 0, capturedStreamData.Length);
                    stream.Position = 0;
                });

            // Act
            var result = (await sut.GetAsync("test-object-name", "test-tag")).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains("object-id-1", result);
            Assert.Contains("object-id-2", result);
        }

        [Fact]
        public async Task Should_return_empty_collection_when_document_does_not_exist()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns<Task<Response>>(_ =>
                    throw new RequestFailedException(404, "BlobNotFound", BlobErrorCode.BlobNotFound.ToString(), null));

            // Act
            var result = await sut.GetAsync("test-object-name", "test-tag");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_sanitize_tag_for_filename()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            var capturedStreamData = "{ \"tag\": \"test\"}"u8.ToArray();
            blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns(Task.FromResult(Substitute.For<Response>()))
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<MemoryStream>();
                    stream.Write(capturedStreamData, 0, capturedStreamData.Length);
                    stream.Position = 0;
                });

            // Act
            await sut.GetAsync("test-object-name", "test/tag\\with*invalid?chars");

            // Assert
            blobContainerClient.Received(1).GetBlobClient("tags/document/testtagwithinvalidchars.json");
        }

        [Fact]
        public async Task Should_use_lowercase_object_name_for_container()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            var capturedStreamData = "{ \"tag\": \"test\"}"u8.ToArray();
            blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns(Task.FromResult(Substitute.For<Response>()))
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<MemoryStream>();
                    stream.Write(capturedStreamData, 0, capturedStreamData.Length);
                    stream.Position = 0;
                });

            // Act
            await sut.GetAsync("TEST-OBJECT-NAME", "test-tag");

            // Assert
            blobServiceClient.Received(1).GetBlobContainerClient("test-object-name");
        }

        [Fact]
        public async Task Should_use_default_connection_name()
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            var capturedStreamData = "{ \"tag\": \"test\"}"u8.ToArray();
            blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Any<BlobRequestConditions>())
                .Returns(Task.FromResult(Substitute.For<Response>()))
                .AndDoes(callInfo =>
                {
                    var stream = callInfo.Arg<MemoryStream>();
                    stream.Write(capturedStreamData, 0, capturedStreamData.Length);
                    stream.Position = 0;
                });

            // Act
            await sut.GetAsync("test-object-name", "test-tag");

            // Assert
            clientFactory.Received(1).CreateClient(defaultConnectionName);
        }





        [Fact]
        public async Task Should_throw_document_configuration_exception_when_blob_client_is_null()
        {
            // Arrange
            blobContainerClient.GetBlobClient(Arg.Any<string>()).Returns((BlobClient)null!);
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            // Act & Assert
            var exception =
                await Assert.ThrowsAsync<DocumentConfigurationException>(() =>
                    sut.GetAsync("test-object-name", "test-tag"));
            Assert.Contains("Unable to create blobClient", exception.Message);
        }
    }

    public class ValidBlobFilenameRegex : BlobDocumentTagStoreTests
    {
        [Theory]
        [InlineData("test\\tag", "testtag")]
        [InlineData("test/tag", "testtag")]
        [InlineData("test*tag", "testtag")]
        [InlineData("test?tag", "testtag")]
        [InlineData("test<tag", "testtag")]
        [InlineData("test>tag", "testtag")]
        [InlineData("test|tag", "testtag")]
        [InlineData("test\"tag", "testtag")]
        [InlineData("test\rtag", "testtag")]
        [InlineData("test\ntag", "testtag")]
        [InlineData("test\\/*?<>|\"tag\r\n", "testtag")]
        [InlineData("valid-tag_name", "valid-tag_name")]
        [InlineData("UPPERCASE", "uppercase")]
        public async Task Should_sanitize_filename_correctly(string input, string expected)
        {
            // Arrange
            var sut = new BlobDocumentTagStore(
                clientFactory,
                defaultDocumentTagType,
                defaultConnectionName,
                autoCreateContainer);

            var etag = new ETag("test-etag");
            var contentInfo = BlobsModelFactory.BlobContentInfo(
                etag,
                DateTimeOffset.UtcNow,
                [],
                "1.0",
                null,
                null,
                0
            );
            var responseUpload = Substitute.For<Response<BlobContentInfo>>();
            responseUpload.Value.Returns(contentInfo);

            blobClient.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                .Returns(responseUpload);

            blobClient.ExistsAsync().Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));

            // Act
            await sut.SetAsync(objectDocument, input);

            // Assert
            blobContainerClient.Received(1).GetBlobClient($"tags/document/{expected}.json");
        }
    }
}
