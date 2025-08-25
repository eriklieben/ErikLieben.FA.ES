using System.Diagnostics;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob
{
    public class BlobObjectDocumentFactoryTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_initialize_with_component_parameters()
            {
                // Arrange
                var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
                var documentTagStore = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings();
                var blobSettings = new EventStreamBlobSettings("blob");

                // Act
                var sut = new BlobObjectDocumentFactory(clientFactory, documentTagStore, settings, blobSettings);

                // Assert
                Assert.NotNull(sut);
            }

            [Fact]
            public void Should_initialize_with_blobDocumentStore()
            {
                // Arrange
                var blobDocumentStore = Substitute.For<IBlobDocumentStore>();

                // Act
                var sut = new BlobObjectDocumentFactory(blobDocumentStore);

                // Assert
                Assert.NotNull(sut);
            }
        }

        public class GetOrCreateAsyncMethod
        {
            private readonly IBlobDocumentStore blobDocumentStore;
            private readonly BlobObjectDocumentFactory sut;

            public GetOrCreateAsyncMethod()
            {
                blobDocumentStore = Substitute.For<IBlobDocumentStore>();
                sut = new BlobObjectDocumentFactory(blobDocumentStore);
            }

            [Fact]
            public async Task Should_call_create_async_with_lowercase_object_name()
            {
                // Arrange
                var objectName = "TestObject";
                var objectId = "id-123";
                var expectedObjectDocument = Substitute.For<IObjectDocument>();
                blobDocumentStore.CreateAsync(Arg.Any<string>(), Arg.Any<string>())
                    .Returns(expectedObjectDocument);

                // Act
                var result = await sut.GetOrCreateAsync(objectName, objectId);

                // Assert
                await blobDocumentStore.Received(1).CreateAsync(objectName.ToLowerInvariant(), objectId);
                Assert.Equal(expectedObjectDocument, result);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void Should_throw_when_object_name_is_invalid(string objectName)
            {
                // Arrange
                var objectId = "id-123";

                // Act & Assert
                var exception = Assert.ThrowsAsync<ArgumentException>(() =>
                    sut.GetOrCreateAsync(objectName, objectId));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void Should_throw_when_object_id_is_invalid(string objectId)
            {
                // Arrange
                var objectName = "TestObject";

                // Act & Assert
                var exception = Assert.ThrowsAsync<ArgumentException>(() =>
                    sut.GetOrCreateAsync(objectName, objectId));
            }
        }

        public class GetAsyncMethod
        {
            private readonly IBlobDocumentStore blobDocumentStore;
            private readonly BlobObjectDocumentFactory sut;

            public GetAsyncMethod()
            {
                blobDocumentStore = Substitute.For<IBlobDocumentStore>();
                sut = new BlobObjectDocumentFactory(blobDocumentStore);
            }

            [Fact]
            public async Task Should_call_get_async_with_lowercase_object_name()
            {
                // Arrange
                var objectName = "TestObject";
                var objectId = "id-123";
                var expectedObjectDocument = Substitute.For<IObjectDocument>();
                blobDocumentStore.GetAsync(Arg.Any<string>(), Arg.Any<string>())
                    .Returns(expectedObjectDocument);

                // Act
                var result = await sut.GetAsync(objectName, objectId);

                // Assert
                await blobDocumentStore.Received(1).GetAsync(objectName.ToLowerInvariant(), objectId);
                Assert.Equal(expectedObjectDocument, result);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void Should_throw_when_object_name_is_invalid(string objectName)
            {
                // Arrange
                var objectId = "id-123";

                // Act & Assert
                var exception = Assert.ThrowsAsync<ArgumentException>(() =>
                    sut.GetAsync(objectName, objectId));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void Should_throw_when_object_id_is_invalid(string objectId)
            {
                // Arrange
                var objectName = "TestObject";

                // Act & Assert
                var exception = Assert.ThrowsAsync<ArgumentException>(() =>
                    sut.GetAsync(objectName, objectId));
            }
        }

        public class GetByDocumentTagAsyncMethod
        {
            private readonly IBlobDocumentStore blobDocumentStore;
            private readonly BlobObjectDocumentFactory sut;

            public GetByDocumentTagAsyncMethod()
            {
                blobDocumentStore = Substitute.For<IBlobDocumentStore>();
                sut = new BlobObjectDocumentFactory(blobDocumentStore);
            }

            [Fact]
            public async Task Should_call_get_by_document_by_tag_async()
            {
                // Arrange
                var objectName = "TestObject";
                var objectDocumentTag = "tag1";
                var expectedDocuments = new List<IObjectDocument> { Substitute.For<IObjectDocument>() };
                blobDocumentStore.GetByDocumentByTagAsync(Arg.Any<string>(), Arg.Any<string>())
                    .Returns(expectedDocuments);

                // Act
                var result = await sut.GetByDocumentTagAsync(objectName, objectDocumentTag);

                // Assert
                await blobDocumentStore.Received(1).GetByDocumentByTagAsync(objectName, objectDocumentTag);
                Assert.Equal(expectedDocuments, result);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void Should_throw_when_object_name_is_invalid(string objectName)
            {
                // Arrange
                var objectDocumentTag = "tag1";

                // Act & Assert
                var exception = Assert.ThrowsAsync<ArgumentException>(() =>
                    sut.GetByDocumentTagAsync(objectName, objectDocumentTag));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void Should_throw_when_document_tag_is_invalid(string objectDocumentTag)
            {
                // Arrange
                var objectName = "TestObject";

                // Act & Assert
                var exception = Assert.ThrowsAsync<ArgumentException>(() =>
                    sut.GetByDocumentTagAsync(objectName, objectDocumentTag));
            }
        }

        public class GetFirstByObjectDocumentTagMethod
        {
            private readonly IBlobDocumentStore blobDocumentStore;
            private readonly BlobObjectDocumentFactory sut;

            public GetFirstByObjectDocumentTagMethod()
            {
                blobDocumentStore = Substitute.For<IBlobDocumentStore>();
                sut = new BlobObjectDocumentFactory(blobDocumentStore);
            }

            [Fact]
            public async Task Should_call_get_first_by_document_by_tag_async()
            {
                // Arrange
                var objectName = "TestObject";
                var objectDocumentTag = "tag1";
                var expectedDocument = Substitute.For<IObjectDocument>();
                blobDocumentStore.GetFirstByDocumentByTagAsync(Arg.Any<string>(), Arg.Any<string>())
                    .Returns(expectedDocument);

                // Act
                var result = await sut.GetFirstByObjectDocumentTag(objectName, objectDocumentTag);

                // Assert
                await blobDocumentStore.Received(1).GetFirstByDocumentByTagAsync(objectName, objectDocumentTag);
                Assert.Equal(expectedDocument, result);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void Should_throw_when_object_name_is_invalid(string objectName)
            {
                // Arrange
                var objectDocumentTag = "tag1";

                // Act & Assert
                var exception = Assert.ThrowsAsync<ArgumentException>(() =>
                    sut.GetFirstByObjectDocumentTag(objectName, objectDocumentTag));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void Should_throw_when_document_tag_is_invalid(string objectDocumentTag)
            {
                // Arrange
                var objectName = "TestObject";

                // Act & Assert
                var exception = Assert.ThrowsAsync<ArgumentException>(() =>
                    sut.GetFirstByObjectDocumentTag(objectName, objectDocumentTag));
            }
        }

        public class GetByObjectDocumentTagMethod
        {
            private readonly IBlobDocumentStore blobDocumentStore;
            private readonly BlobObjectDocumentFactory sut;

            public GetByObjectDocumentTagMethod()
            {
                blobDocumentStore = Substitute.For<IBlobDocumentStore>();
                sut = new BlobObjectDocumentFactory(blobDocumentStore);
            }

            [Fact]
            public async Task Should_call_get_by_document_by_tag_async()
            {
                // Arrange
                var objectName = "TestObject";
                var objectDocumentTag = "tag1";
                var expectedDocuments = new List<IObjectDocument> { Substitute.For<IObjectDocument>() };
                blobDocumentStore.GetByDocumentByTagAsync(Arg.Any<string>(), Arg.Any<string>())
                    .Returns(expectedDocuments);

                // Act
                var result = await sut.GetByObjectDocumentTag(objectName, objectDocumentTag);

                // Assert
                await blobDocumentStore.Received(1).GetByDocumentByTagAsync(objectName, objectDocumentTag);
                Assert.Equal(expectedDocuments, result);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void Should_throw_when_object_name_is_invalid(string objectName)
            {
                // Arrange
                var objectDocumentTag = "tag1";

                // Act & Assert
                var exception = Assert.ThrowsAsync<ArgumentException>(() =>
                    sut.GetByObjectDocumentTag(objectName, objectDocumentTag));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void Should_throw_when_document_tag_is_invalid(string objectDocumentTag)
            {
                // Arrange
                var objectName = "TestObject";

                // Act & Assert
                var exception = Assert.ThrowsAsync<ArgumentException>(() =>
                    sut.GetByObjectDocumentTag(objectName, objectDocumentTag));
            }
        }

        public class SetAsyncMethod
        {
            private readonly IBlobDocumentStore blobDocumentStore;
            private readonly BlobObjectDocumentFactory sut;

            public SetAsyncMethod()
            {
                blobDocumentStore = Substitute.For<IBlobDocumentStore>();
                sut = new BlobObjectDocumentFactory(blobDocumentStore);
            }

            [Fact]
            public async Task Should_call_set_async()
            {
                // Arrange
                var document = Substitute.For<IObjectDocument>();

                // Act
                await sut.SetAsync(document);

                // Assert
                await blobDocumentStore.Received(1).SetAsync(document);
            }

            [Fact]
            public void Should_throw_when_document_is_null()
            {
                // Arrange
                IObjectDocument document = null;

                // Act & Assert
                var exception = Assert.ThrowsAsync<ArgumentNullException>(() =>
                    sut.SetAsync(document));
            }
        }
    }
}
