using System.Text;
using System.Text.Json.Serialization.Metadata;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using Microsoft.Extensions.Azure;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob
{
    public class BlobSnapShotStoreTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_not_throw_when_provided_valid_parameters()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");

                // Act
                var sut = new BlobSnapShotStore(clientFactory, settings);

                // Assert
                Assert.NotNull(sut);
            }
        }

        public class SetAsync
        {
            [Fact]
            public async Task Should_save_object_to_blob_without_name()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);

                var obj = new TestEntity();
                var document = CreateMockDocument();
                const int version = 123;

                var blobServiceClient = Substitute.For<BlobServiceClient>();
                var containerClient = Substitute.For<BlobContainerClient>();
                var blobClient = Substitute.For<BlobClient>();

                var contentInfo =BlobsModelFactory.BlobContentInfo(
                    new ETag("etag"),
                    DateTimeOffset.UtcNow,
                    [],
                    "1.0",
                    null,
                    null,
                    0
                );
                var response = Substitute.For<Response<BlobContentInfo>>();
                response.Value.Returns(contentInfo);

                blobClient
                    .UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                    .Returns(response);

                SetupBlobClients(clientFactory, blobServiceClient, containerClient, blobClient, document);

                // Act
                await sut.SetAsync(obj, TestJsonContext.Default.TestEntity, document, version);

                // Assert
                await blobClient.Received(1).UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>());
                var expectedPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
                containerClient.Received(1).GetBlobClient(expectedPath);
            }

            [Fact]
            public async Task Should_save_object_to_blob_with_name()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);

                var obj = new TestEntity();
                var document = CreateMockDocument();
                const int version = 123;
                const string name = "custom-name";

                var blobServiceClient = Substitute.For<BlobServiceClient>();
                var containerClient = Substitute.For<BlobContainerClient>();
                var blobClient = Substitute.For<BlobClient>();

                var contentInfo =BlobsModelFactory.BlobContentInfo(
                    new ETag("etag"),
                    DateTimeOffset.UtcNow,
                    [],
                    "1.0",
                    null,
                    null,
                    0
                );
                var response = Substitute.For<Response<BlobContentInfo>>();
                response.Value.Returns(contentInfo);

                blobClient
                    .UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>())
                    .Returns(response);

                SetupBlobClients(clientFactory, blobServiceClient, containerClient, blobClient, document);

                // Act
                await sut.SetAsync(obj, TestJsonContext.Default.TestEntity, document, version, name);

                // Assert
                await blobClient.Received(1).UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>());
                var expectedPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}_{name}.json";
                containerClient.Received(1).GetBlobClient(expectedPath);
            }

            [Fact]
            public async Task Should_throw_when_document_object_name_is_null()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);

                var obj = Substitute.For<IBase>();
                var document = Substitute.For<IObjectDocument>();
                var active = Substitute.For<StreamInformation>();
                active.StreamIdentifier = "stream-123";
                active.SnapShotConnectionName = "snapshot-connection";
                document.Active.Returns(active);
                document.ObjectName.Returns((string?)null);

                var version = 123;

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                    sut.SetAsync(obj, TestJsonContext.Default.TestEntity, document, version));
            }

            [Fact]
            public async Task Should_throw_when_blob_client_creation_fails()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);

                var obj = Substitute.For<IBase>();
                var document = CreateMockDocument();
                var version = 123;

                var blobServiceClient = Substitute.For<BlobServiceClient>();
                var containerClient = Substitute.For<BlobContainerClient>();

                clientFactory.CreateClient(document.Active.SnapShotConnectionName).Returns(blobServiceClient);
                blobServiceClient.GetBlobContainerClient(document.ObjectName!.ToLowerInvariant())
                    .Returns(containerClient);
                containerClient.GetBlobClient(Arg.Any<string>()).Returns((BlobClient?)null);

                // Act & Assert
                await Assert.ThrowsAsync<DocumentConfigurationException>(() =>
                    sut.SetAsync(obj, TestJsonContext.Default.TestEntity, document, version));
            }
        }

        public class GetAsyncGeneric
        {
            [Fact]
            public async Task Should_get_typed_entity_from_blob_without_name()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);

                var document = CreateMockDocument();
                var version = 123;

                var blobServiceClient = Substitute.For<BlobServiceClient>();
                var containerClient = Substitute.For<BlobContainerClient>();
                var blobClient = Substitute.For<BlobClient>();
                var response = Substitute.For<Response>();
                blobClient
                    .DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
                    .Returns(Task.FromResult(response))
                    .AndDoes(callInfo =>
                    {
                        var stream = callInfo.Arg<Stream>();
                        var jsonBytes = "{}"u8.ToArray();
                        stream.Write(jsonBytes, 0, jsonBytes.Length);
                        stream.Position = 0;
                    });

                SetupBlobClients(clientFactory, blobServiceClient, containerClient, blobClient, document);

                // Act
                var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, version);

                // Assert
                Assert.NotNull(result);
                var expectedPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
                containerClient.Received(1).GetBlobClient(expectedPath);
            }

            [Fact]
            public async Task Should_get_typed_entity_from_blob_with_name()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);

                var document = CreateMockDocument();
                var version = 123;
                var name = "custom-name";

                var blobServiceClient = Substitute.For<BlobServiceClient>();
                var containerClient = Substitute.For<BlobContainerClient>();
                var blobClient = Substitute.For<BlobClient>();
                var response = Substitute.For<Response>();
                blobClient
                    .DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
                    .Returns(Task.FromResult(response))
                    .AndDoes(callInfo =>
                    {
                        var stream = callInfo.Arg<Stream>();
                        var jsonBytes = "{}"u8.ToArray();
                        stream.Write(jsonBytes, 0, jsonBytes.Length);
                        stream.Position = 0;
                    });

                SetupBlobClients(clientFactory, blobServiceClient, containerClient, blobClient, document);

                // Act
                var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, version, name);

                // Assert
                Assert.NotNull(result);
                var expectedPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}_{name}.json";
                containerClient.Received(1).GetBlobClient(expectedPath);
            }

            [Fact]
            public async Task Should_return_null_when_entity_not_found()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);

                var document = CreateMockDocument();
                var version = 123;

                var blobServiceClient = Substitute.For<BlobServiceClient>();
                var containerClient = Substitute.For<BlobContainerClient>();
                var blobClient = Substitute.For<BlobClient>();
                blobClient
                    .DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
                    .ThrowsAsync(new RequestFailedException(404, "BlobNotFound", "BlobNotFound", null));

                SetupBlobClients(clientFactory, blobServiceClient, containerClient, blobClient, document);

                // Act
                var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, version);

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public async Task Should_throw_when_document_object_name_is_null()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);
                var document = Substitute.For<IObjectDocument>();
                var active = Substitute.For<StreamInformation>();
                active.StreamIdentifier = "stream-123";
                active.SnapShotConnectionName = "snapshot-connection";
                document.Active.Returns(active);
                document.ObjectName.Returns((string?)null);

                var version = 123;

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                    sut.GetAsync(TestJsonContext.Default.TestEntity, document, version));
            }
        }

        public class GetAsyncNonGeneric
        {
            [Fact]
            public async Task Should_get_object_entity_from_blob_without_name()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);

                var document = CreateMockDocument();
                var version = 123;

                var blobServiceClient = Substitute.For<BlobServiceClient>();
                var containerClient = Substitute.For<BlobContainerClient>();
                var blobClient = Substitute.For<BlobClient>();
                var response = Substitute.For<Response>();
                blobClient
                    .DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
                    .Returns(Task.FromResult(response))
                    .AndDoes(callInfo =>
                    {
                        var stream = callInfo.Arg<Stream>();
                        var jsonBytes = "{}"u8.ToArray();
                        stream.Write(jsonBytes, 0, jsonBytes.Length);
                        stream.Position = 0;
                    });

                SetupBlobClients(clientFactory, blobServiceClient, containerClient, blobClient, document);

                // Act
                var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, version);

                // Assert
                Assert.NotNull(result);
                var expectedPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
                containerClient.Received(1).GetBlobClient(expectedPath);
            }

            [Fact]
            public async Task Should_get_object_entity_from_blob_with_name()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);
                var document = CreateMockDocument();
                var version = 123;
                var name = "custom-name";

                var blobServiceClient = Substitute.For<BlobServiceClient>();
                var containerClient = Substitute.For<BlobContainerClient>();
                var blobClient = Substitute.For<BlobClient>();
                var response = Substitute.For<Response>();
                blobClient
                    .DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
                    .Returns(Task.FromResult(response))
                    .AndDoes(callInfo =>
                    {
                        var stream = callInfo.Arg<Stream>();
                        var jsonBytes = "{}"u8.ToArray();
                        stream.Write(jsonBytes, 0, jsonBytes.Length);
                        stream.Position = 0;
                    });

                SetupBlobClients(clientFactory, blobServiceClient, containerClient, blobClient, document);

                // Act
                var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, version, name);

                // Assert
                Assert.NotNull(result);
                var expectedPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}_{name}.json";
                containerClient.Received(1).GetBlobClient(expectedPath);

            }

            [Fact]
            public async Task Should_return_null_when_object_entity_not_found()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);

                var document = CreateMockDocument();
                var version = 123;

                var blobServiceClient = Substitute.For<BlobServiceClient>();
                var containerClient = Substitute.For<BlobContainerClient>();
                var blobClient = Substitute.For<BlobClient>();
                blobClient
                    .DownloadToAsync(Arg.Any<Stream>(), Arg.Any<BlobRequestConditions>())
                    .ThrowsAsync(new RequestFailedException(404, "BlobNotFound", "BlobNotFound", null));

                SetupBlobClients(clientFactory, blobServiceClient, containerClient, blobClient, document);

                // Act
                var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, version);

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public async Task Should_throw_when_document_object_name_is_null()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var settings = new EventStreamBlobSettings("default-store");
                var sut = new BlobSnapShotStore(clientFactory, settings);
                var document = Substitute.For<IObjectDocument>();
                var active = Substitute.For<StreamInformation>();
                active.StreamIdentifier = "stream-123";
                active.SnapShotConnectionName = "snapshot-connection";
                document.Active.Returns(active);
                document.ObjectName.Returns((string?)null);

                var version = 123;

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                    sut.GetAsync(TestJsonContext.Default.TestEntity, document, version));
            }
        }

        public class AutoCreateContainerTests
        {
            [Fact]
            public async Task Should_create_container_when_auto_create_is_enabled()
            {
                // Arrange
                var settings = new EventStreamBlobSettings("default-store") { AutoCreateContainer = true };

                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var sut = new BlobSnapShotStore(clientFactory, settings);
                var obj = new TestEntity();
                var document = CreateMockDocument();
                var version = 123;
                var blobServiceClient = Substitute.For<BlobServiceClient>();
                var containerClient = Substitute.For<BlobContainerClient>();
                blobServiceClient.GetBlobContainerClient(Arg.Any<string>()).Returns(containerClient);
                var blobClient = Substitute.For<BlobClient>();
                containerClient.GetBlobClient(Arg.Any<string>()).Returns(blobClient);
                var response = Substitute.For<Response<BlobContentInfo>>();
                var blobContentInfo = BlobsModelFactory.BlobContentInfo(
                    new ETag("etag"),
                    DateTimeOffset.UtcNow,
                    [],
                    "1.0",
                    null,
                    null,
                    0
                );
                response.Value.Returns(blobContentInfo);
                blobClient.UploadAsync(
                    Arg.Any<MemoryStream>(),
                    Arg.Any<BlobUploadOptions>()).Returns(Task.FromResult(response));
                SetupBlobClients(clientFactory, blobServiceClient, containerClient, blobClient, document);

                // Act
                await sut.SetAsync(obj, TestJsonContext.Default.TestEntity, document, version);

                // Assert
                await containerClient.Received(1).CreateIfNotExistsAsync();
            }

            [Fact]
            public async Task Should_not_create_container_when_auto_create_is_disabled()
            {
                // Arrange
                var settings = new EventStreamBlobSettings("default-store") { AutoCreateContainer = false };

                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var sut = new BlobSnapShotStore(clientFactory, settings);
                var obj = new TestEntity();
                var document = CreateMockDocument();
                var version = 123;
                var blobServiceClient = Substitute.For<BlobServiceClient>();
                var containerClient = Substitute.For<BlobContainerClient>();
                blobServiceClient.GetBlobContainerClient(Arg.Any<string>()).Returns(containerClient);
                var blobClient = Substitute.For<BlobClient>();
                containerClient.GetBlobClient(Arg.Any<string>()).Returns(blobClient);
                var response = Substitute.For<Response<BlobContentInfo>>();
                var blobContentInfo = BlobsModelFactory.BlobContentInfo(
                    new ETag("etag"),
                    DateTimeOffset.UtcNow,
                    [],
                    "1.0",
                    null,
                    null,
                    0
                );
                response.Value.Returns(blobContentInfo);
                blobClient.UploadAsync(
                    Arg.Any<MemoryStream>(),
                    Arg.Any<BlobUploadOptions>()).Returns(Task.FromResult(response));
                SetupBlobClients(clientFactory, blobServiceClient, containerClient, blobClient, document);

                // Act
                await sut.SetAsync(obj, TestJsonContext.Default.TestEntity, document, version);

                // Assert
                await containerClient.DidNotReceive().CreateIfNotExistsAsync();
            }
        }

        // Helper methods and classes
        private static IObjectDocument CreateMockDocument()
        {
            var document = Substitute.For<IObjectDocument>();
            var active = Substitute.For<StreamInformation>();
            active.StreamIdentifier = "stream-123";
            active.SnapShotConnectionName = "snapshot-connection";
            document.Active.Returns(active);
            document.ObjectName.Returns("TestObject");
            return document;
        }

        private static void SetupBlobClients(IAzureClientFactory<BlobServiceClient> clientFactory,
            BlobServiceClient blobServiceClient, BlobContainerClient containerClient, BlobClient blobClient,
            IObjectDocument document)
        {
            clientFactory.CreateClient(document.Active.SnapShotConnectionName).Returns(blobServiceClient);
            blobServiceClient.GetBlobContainerClient(document.ObjectName!.ToLowerInvariant()).Returns(containerClient);
            containerClient.GetBlobClient(Arg.Any<string>()).Returns(blobClient);
        }
    }
}
