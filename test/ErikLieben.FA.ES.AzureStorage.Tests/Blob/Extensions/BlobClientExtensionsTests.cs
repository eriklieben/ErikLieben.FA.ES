using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using NSubstitute;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob.Extensions
{
    public class BlobExtensionsTests
    {
        public class AsEntityAsyncGeneric
        {
            [Fact]
            public async Task Should_return_deserialized_entity_and_hash_when_blob_exists()
            {
                // Arrange
                var blobClient = Substitute.For<BlobClient>();
                var requestOptions = new BlobRequestConditions();
                var testDocument = new TestDocument();
                var json = "{\"property\":\"value\"}";
                var expectedHash = ComputeSha256HashForTest(json);
                var response = Substitute.For<Response>();
                response.Status.Returns(200);

                blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), requestOptions)
                    .Returns(Task.FromResult(response))
                    .AndDoes(callInfo =>
                    {
                        var stream = callInfo.Arg<MemoryStream>();
                        var bytes = Encoding.UTF8.GetBytes(json);
                        stream.Write(bytes, 0, bytes.Length);
                    });

                // Act
                var result = await blobClient.AsEntityAsync(
                    TestDocumentContext.Default.TestDocument,
                    requestOptions);

                // Assert
                Assert.Equal(testDocument.Property, result.Item1?.Property);
                Assert.Equal(expectedHash, result.Item2);
            }

            [Fact]
            public async Task Should_return_null_when_blob_not_found()
            {
                // Arrange
                var blobClient = Substitute.For<BlobClient>();
                var requestOptions = new BlobRequestConditions();

                var ex = new RequestFailedException(404, "Not found", BlobErrorCode.BlobNotFound.ToString(), null);
                blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), requestOptions)
                    .Returns(Task.FromException<Response>(ex));

                // Act
                var result = await blobClient.AsEntityAsync(
                    TestDocumentContext.Default.TestDocument, requestOptions);

                // Assert
                Assert.Null(result.Item1);
                Assert.Null(result.Item2);
            }

            [Fact]
            public async Task Should_return_null_when_container_not_found()
            {
                // Arrange
                var blobClient = Substitute.For<BlobClient>();
                var requestOptions = new BlobRequestConditions();

                var ex = new RequestFailedException(404, "Not found", BlobErrorCode.ContainerNotFound.ToString(), null);
                blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), requestOptions)
                    .Returns(Task.FromException<Response>(ex));

                // Act
                var result = await blobClient.AsEntityAsync(
                    TestDocumentContext.Default.TestDocument,
                    requestOptions);

                // Assert
                Assert.Null(result.Item1);
                Assert.Null(result.Item2);
            }

            [Fact]
            public async Task Should_throw_when_request_fails_with_unexpected_error()
            {
                // Arrange
                var blobClient = Substitute.For<BlobClient>();
                var requestOptions = new BlobRequestConditions();

                var ex = new RequestFailedException(500, "Internal server error", "InternalError", null);
                blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), requestOptions)
                    .Returns(Task.FromException<Response>(ex));

                // Act & Assert
                await Assert.ThrowsAsync<RequestFailedException>(() =>
                    blobClient.AsEntityAsync(
                        TestDocumentContext.Default.TestDocument,
                        requestOptions));
            }
        }

        public class AsEntityAsyncNonGeneric
        {
            [Fact]
            public async Task Should_return_deserialized_entity_when_blob_exists()
            {
                // Arrange
                var blobClient = Substitute.For<BlobClient>();
                var requestOptions = Substitute.For<BlobRequestConditions>();

                var stream = new MemoryStream();
                var testDocument = new TestDocument { Property = "value" };
                var json = JsonSerializer.Serialize(testDocument, TestDocumentContext.Default.TestDocument);
                var bytes = Encoding.UTF8.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Position = 0;

                var response = Substitute.For<Response>();
                response.Status.Returns(200);
                blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Is(requestOptions))
                    .Returns(Task.FromResult(response))
                    .AndDoes(callInfo =>
                    {
                        var memoryStream = callInfo.Arg<MemoryStream>();
                        memoryStream.Write(bytes, 0, bytes.Length);
                        memoryStream.Position = 0;
                    });

                // Act
                var result = await blobClient.AsEntityAsync(
                    TestDocumentContext.Default.TestDocument,
                    requestOptions);

                // Assert
                Assert.Equal(testDocument.Property, result.Item1?.Property);
            }

            [Fact]
            public async Task Should_throw_when_unknown_exception_occurs()
            {
                // Arrange
                var blobClient = Substitute.For<BlobClient>();
                var requestOptions = new BlobRequestConditions();

                blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), Arg.Is(requestOptions))
                    .Returns(Task.FromException<Response>(new Exception("Test exception")));

                // Act & Assert
                await Assert.ThrowsAsync<Exception>(() =>
                    blobClient.AsEntityAsync(
                    TestDocumentContext.Default.TestDocument,
                    requestOptions));
            }
        }

        public class AsString
        {
            [Fact]
            public async Task Should_return_blob_content_as_string_when_blob_exists()
            {
                // Arrange
                var blobClient = Substitute.For<BlobClient>();
                var requestOptions = new BlobRequestConditions();
                var expectedContent = "test content";
                var response = Substitute.For<Response>();
                response.Status.Returns(200);

                blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), requestOptions)
                    .Returns(Task.FromResult(response))
                    .AndDoes(callInfo =>
                    {
                        var stream = callInfo.Arg<MemoryStream>();
                        var bytes = Encoding.UTF8.GetBytes(expectedContent);
                        stream.Write(bytes, 0, bytes.Length);
                    });

                // Act
                var result = await blobClient.AsString(requestOptions);

                // Assert
                Assert.Equal(expectedContent, result);
            }

            [Fact]
            public async Task Should_return_null_when_blob_not_found()
            {
                // Arrange
                var blobClient = Substitute.For<BlobClient>();
                var requestOptions = new BlobRequestConditions();

                var ex = new RequestFailedException(404, "Not found", BlobErrorCode.BlobNotFound.ToString(), null);
                blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), requestOptions)
                    .Returns(Task.FromException<Response>(ex));

                // Act
                var result = await blobClient.AsString(requestOptions);

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public async Task Should_throw_when_request_fails_with_unexpected_error()
            {
                // Arrange
                var blobClient = Substitute.For<BlobClient>();
                var requestOptions = new BlobRequestConditions();

                var ex = new RequestFailedException(500, "Internal server error", "InternalError", null);
                blobClient.DownloadToAsync(Arg.Any<MemoryStream>(), requestOptions)
                    .Returns(Task.FromException<Response>(ex));

                // Act & Assert
                await Assert.ThrowsAsync<RequestFailedException>(() =>
                    blobClient.AsString(requestOptions));
            }
        }

        public class Save
        {
            [Fact]
            public async Task Should_upload_serialized_object_and_return_etag()
            {
                // Arrange
                var blobClient = Substitute.For<BlobClient>();
                var testObject = new TestDocument();
                var requestOptions = new BlobRequestConditions();
                var metadata = new Dictionary<string, string> { { "key", "value" } };
                var tags = new Dictionary<string, string> { { "tag", "value" } };
                const string expectedETag = "\"00000000-0000-0000-0000-000000000000\"";

                var response = Substitute.For<Response<BlobContentInfo>>();
                var contentInfo = BlobsModelFactory.BlobContentInfo(
                    new ETag(expectedETag),
                    DateTimeOffset.UtcNow,
                    [],
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0);

                response.Value.Returns(contentInfo);

                blobClient.UploadAsync(
                    Arg.Any<MemoryStream>(),
                    Arg.Is<BlobUploadOptions>(options =>
                        options.HttpHeaders.ContentType == "application/json" &&
                        options.Conditions == requestOptions &&
                        options.Tags == tags &&
                        options.Metadata == metadata))
                    .Returns(response);

                // Act
                var result = await blobClient.Save(
                    testObject,
                    TestDocumentContext.Default.TestDocument,
                    requestOptions,
                    metadata, tags);

                // Assert
                Assert.Equal(expectedETag, result);
            }
        }

        public class SaveEntityAsync
        {
            [Fact]
            public async Task Should_upload_serialized_entity_and_return_etag_and_hash()
            {
                // Arrange
                var blobClient = Substitute.For<BlobClient>();
                var entity = new TestDocument();
                var requestOptions = new BlobRequestConditions();
                var metadata = new Dictionary<string, string> { { "key", "value" } };
                var tags = new Dictionary<string, string> { { "tag", "value" } };
                const string expectedETag = "\"00000000-0000-0000-0000-000000000000\"";
                var expectedHash = ComputeSha256HashForTest(
                    JsonSerializer.Serialize(entity, TestDocumentContext.Default.TestDocument));

                var response = Substitute.For<Response<BlobContentInfo>>();
                var contentInfo = BlobsModelFactory.BlobContentInfo(
                    new ETag(expectedETag),
                    DateTimeOffset.UtcNow,
                    [],
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0);

                response.Value.Returns(contentInfo);

                blobClient.UploadAsync(
                    Arg.Any<MemoryStream>(),
                    Arg.Is<BlobUploadOptions>(options =>
                        options.HttpHeaders.ContentType == "application/json" &&
                        options.Conditions == requestOptions &&
                        options.Tags == tags &&
                        options.Metadata == metadata))
                    .Returns(response);

                // Act
                var result = await blobClient.SaveEntityAsync(
                    entity,
                    TestDocumentContext.Default.TestDocument,
                    requestOptions,
                    metadata,
                    tags);

                // Assert
                Assert.Equal(expectedETag, result.Item1);
                Assert.Equal(expectedHash, result.Item2);
            }
        }

        private static string ComputeSha256HashForTest(string rawData)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
            StringBuilder builder = new();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }


    public class TestDocument
    {
        public string Property { get; set; } = string.Empty;
    }


    [JsonSerializable(typeof(TestDocument))]
    internal partial class TestDocumentContext : JsonSerializerContext
    {
    }
}
