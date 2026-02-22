#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using Microsoft.Extensions.Azure;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

public class BlobObjectIdProviderTests
{
    public class Constructor
    {
        [Fact]
        public void Should_initialize_with_required_parameters()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var blobSettings = new EventStreamBlobSettings("Store");

            // Act
            var sut = new BlobObjectIdProvider(clientFactory, blobSettings);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            // Arrange
            IAzureClientFactory<BlobServiceClient> clientFactory = null!;
            var blobSettings = new EventStreamBlobSettings("Store");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobObjectIdProvider(clientFactory, blobSettings));
        }

        [Fact]
        public void Should_throw_when_blob_settings_is_null()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            EventStreamBlobSettings blobSettings = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BlobObjectIdProvider(clientFactory, blobSettings));
        }
    }

    public class GetObjectIdsAsyncMethod
    {
        private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
        private readonly BlobServiceClient blobServiceClient;
        private readonly BlobContainerClient containerClient;
        private readonly EventStreamBlobSettings blobSettings;
        private readonly BlobObjectIdProvider sut;

        public GetObjectIdsAsyncMethod()
        {
            clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            blobServiceClient = Substitute.For<BlobServiceClient>();
            containerClient = Substitute.For<BlobContainerClient>();
            blobSettings = new EventStreamBlobSettings("Store");

            clientFactory.CreateClient("Store").Returns(blobServiceClient);
            blobServiceClient.GetBlobContainerClient(Arg.Any<string>()).Returns(containerClient);

            sut = new BlobObjectIdProvider(clientFactory, blobSettings);
        }

        [Fact]
        public async Task Should_return_empty_result_when_container_does_not_exist()
        {
            // Arrange
            var objectName = "project";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(false, Substitute.For<Response>()));

            // Act
            var result = await sut.GetObjectIdsAsync(objectName, null, 100);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Items);
            Assert.Null(result.ContinuationToken);
            Assert.Equal(100, result.PageSize);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_object_name_is_empty_or_whitespace(string objectName)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.GetObjectIdsAsync(objectName, null, 100));
        }

        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetObjectIdsAsync(null!, null, 100));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public async Task Should_throw_when_page_size_is_less_than_one(int pageSize)
        {
            // Arrange
            var objectName = "project";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                sut.GetObjectIdsAsync(objectName, null, pageSize));
        }

        [Fact]
        public async Task Should_use_lowercase_object_name_as_prefix()
        {
            // Arrange
            var objectName = "PROJECT";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));

            var mockAsyncPageable = CreateMockAsyncPageable([], null);
            containerClient.GetBlobsAsync(Arg.Any<BlobTraits>(), Arg.Any<BlobStates>(), prefix: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(mockAsyncPageable);

            // Act
            await sut.GetObjectIdsAsync(objectName, null, 100);

            // Assert
            containerClient.Received().GetBlobsAsync(
                Arg.Any<BlobTraits>(), Arg.Any<BlobStates>(),
                prefix: "project/",
                cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_extract_object_ids_from_blob_names()
        {
            // Arrange
            var objectName = "project";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));

            var blobItems = new[]
            {
                CreateBlobItem("project/123-guid-1.json"),
                CreateBlobItem("project/456-guid-2.json"),
                CreateBlobItem("project/789-guid-3.json")
            };

            var mockAsyncPageable = CreateMockAsyncPageable(blobItems, null);
            containerClient.GetBlobsAsync(Arg.Any<BlobTraits>(), Arg.Any<BlobStates>(), prefix: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(mockAsyncPageable);

            // Act
            var result = await sut.GetObjectIdsAsync(objectName, null, 100);

            // Assert
            Assert.Equal(3, result.Items.Count);
            Assert.Contains("123-guid-1", result.Items);
            Assert.Contains("456-guid-2", result.Items);
            Assert.Contains("789-guid-3", result.Items);
        }

        [Fact]
        public async Task Should_remove_duplicates_from_result()
        {
            // Arrange
            var objectName = "project";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));

            var blobItems = new[]
            {
                CreateBlobItem("project/123-guid.json"),
                CreateBlobItem("project/123-guid.json"), // Duplicate
                CreateBlobItem("project/456-guid.json")
            };

            var mockAsyncPageable = CreateMockAsyncPageable(blobItems, null);
            containerClient.GetBlobsAsync(Arg.Any<BlobTraits>(), Arg.Any<BlobStates>(), prefix: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(mockAsyncPageable);

            // Act
            var result = await sut.GetObjectIdsAsync(objectName, null, 100);

            // Assert
            Assert.Equal(2, result.Items.Count);
            Assert.Contains("123-guid", result.Items);
            Assert.Contains("456-guid", result.Items);
        }

        [Fact]
        public async Task Should_return_continuation_token_when_more_pages_exist()
        {
            // Arrange
            var objectName = "project";
            var continuationToken = "next-page-token";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));

            var blobItems = new[]
            {
                CreateBlobItem("project/123-guid.json")
            };

            var mockAsyncPageable = CreateMockAsyncPageable(blobItems, continuationToken);
            containerClient.GetBlobsAsync(Arg.Any<BlobTraits>(), Arg.Any<BlobStates>(), prefix: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(mockAsyncPageable);

            // Act
            var result = await sut.GetObjectIdsAsync(objectName, null, 100);

            // Assert
            Assert.Equal(continuationToken, result.ContinuationToken);
            Assert.True(result.HasNextPage);
        }

        [Fact]
        public async Task Should_return_null_continuation_token_when_no_more_pages()
        {
            // Arrange
            var objectName = "project";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));

            var blobItems = new[]
            {
                CreateBlobItem("project/123-guid.json")
            };

            var mockAsyncPageable = CreateMockAsyncPageable(blobItems, null);
            containerClient.GetBlobsAsync(Arg.Any<BlobTraits>(), Arg.Any<BlobStates>(), prefix: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(mockAsyncPageable);

            // Act
            var result = await sut.GetObjectIdsAsync(objectName, null, 100);

            // Assert
            Assert.Null(result.ContinuationToken);
            Assert.False(result.HasNextPage);
        }

        private static BlobItem CreateBlobItem(string name)
        {
            return BlobsModelFactory.BlobItem(name: name);
        }

        private static AsyncPageable<BlobItem> CreateMockAsyncPageable(BlobItem[] items, string? continuationToken)
        {
            var page = Page<BlobItem>.FromValues(items, continuationToken, Substitute.For<Response>());
            var mockPageable = Substitute.For<AsyncPageable<BlobItem>>();

            mockPageable.AsPages(Arg.Any<string>(), Arg.Any<int?>())
                .Returns(CreateAsyncEnumerable([page]));

            return mockPageable;
        }

        private static async IAsyncEnumerable<Page<T>> CreateAsyncEnumerable<T>(Page<T>[] pages)
        {
            foreach (var page in pages)
            {
                yield return page;
            }
            await Task.CompletedTask;
        }
    }

    public class ExistsAsyncMethod
    {
        private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
        private readonly BlobServiceClient blobServiceClient;
        private readonly BlobContainerClient containerClient;
        private readonly BlobClient blobClient;
        private readonly EventStreamBlobSettings blobSettings;
        private readonly BlobObjectIdProvider sut;

        public ExistsAsyncMethod()
        {
            clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            blobServiceClient = Substitute.For<BlobServiceClient>();
            containerClient = Substitute.For<BlobContainerClient>();
            blobClient = Substitute.For<BlobClient>();
            blobSettings = new EventStreamBlobSettings("Store");

            clientFactory.CreateClient("Store").Returns(blobServiceClient);
            blobServiceClient.GetBlobContainerClient(Arg.Any<string>()).Returns(containerClient);
            containerClient.GetBlobClient(Arg.Any<string>()).Returns(blobClient);

            sut = new BlobObjectIdProvider(clientFactory, blobSettings);
        }

        [Fact]
        public async Task Should_return_false_when_container_does_not_exist()
        {
            // Arrange
            var objectName = "project";
            var objectId = "123-guid";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(false, Substitute.For<Response>()));

            // Act
            var result = await sut.ExistsAsync(objectName, objectId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_true_when_blob_exists()
        {
            // Arrange
            var objectName = "project";
            var objectId = "123-guid";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));
            blobClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));

            // Act
            var result = await sut.ExistsAsync(objectName, objectId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Should_return_false_when_blob_does_not_exist()
        {
            // Arrange
            var objectName = "project";
            var objectId = "123-guid";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));
            blobClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(false, Substitute.For<Response>()));

            // Act
            var result = await sut.ExistsAsync(objectName, objectId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_use_lowercase_object_name_in_path()
        {
            // Arrange
            var objectName = "PROJECT";
            var objectId = "123-guid";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));

            // Act
            await sut.ExistsAsync(objectName, objectId);

            // Assert
            containerClient.Received().GetBlobClient("project/123-guid.json");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_object_name_is_empty_or_whitespace(string objectName)
        {
            // Arrange
            var objectId = "123-guid";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.ExistsAsync(objectName, objectId));
        }

        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            // Arrange
            var objectId = "123-guid";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ExistsAsync(null!, objectId));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_object_id_is_empty_or_whitespace(string objectId)
        {
            // Arrange
            var objectName = "project";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.ExistsAsync(objectName, objectId));
        }

        [Fact]
        public async Task Should_throw_when_object_id_is_null()
        {
            // Arrange
            var objectName = "project";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ExistsAsync(objectName, null!));
        }
    }

    public class CountAsyncMethod
    {
        private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
        private readonly BlobServiceClient blobServiceClient;
        private readonly BlobContainerClient containerClient;
        private readonly EventStreamBlobSettings blobSettings;
        private readonly BlobObjectIdProvider sut;

        public CountAsyncMethod()
        {
            clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            blobServiceClient = Substitute.For<BlobServiceClient>();
            containerClient = Substitute.For<BlobContainerClient>();
            blobSettings = new EventStreamBlobSettings("Store");

            clientFactory.CreateClient("Store").Returns(blobServiceClient);
            blobServiceClient.GetBlobContainerClient(Arg.Any<string>()).Returns(containerClient);

            sut = new BlobObjectIdProvider(clientFactory, blobSettings);
        }

        [Fact]
        public async Task Should_return_zero_when_container_does_not_exist()
        {
            // Arrange
            var objectName = "project";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(false, Substitute.For<Response>()));

            // Act
            var result = await sut.CountAsync(objectName);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_count_unique_object_ids()
        {
            // Arrange
            var objectName = "project";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));

            var blobItems = new[]
            {
                BlobsModelFactory.BlobItem(name: "project/123-guid.json"),
                BlobsModelFactory.BlobItem(name: "project/456-guid.json"),
                BlobsModelFactory.BlobItem(name: "project/789-guid.json")
            };

            var mockAsyncPageable = CreateMockAsyncEnumerable(blobItems);
            containerClient.GetBlobsAsync(Arg.Any<BlobTraits>(), Arg.Any<BlobStates>(), prefix: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(mockAsyncPageable);

            // Act
            var result = await sut.CountAsync(objectName);

            // Assert
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task Should_remove_duplicates_when_counting()
        {
            // Arrange
            var objectName = "project";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));

            var blobItems = new[]
            {
                BlobsModelFactory.BlobItem(name: "project/123-guid.json"),
                BlobsModelFactory.BlobItem(name: "project/123-guid.json"), // Duplicate
                BlobsModelFactory.BlobItem(name: "project/456-guid.json")
            };

            var mockAsyncPageable = CreateMockAsyncEnumerable(blobItems);
            containerClient.GetBlobsAsync(Arg.Any<BlobTraits>(), Arg.Any<BlobStates>(), prefix: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(mockAsyncPageable);

            // Act
            var result = await sut.CountAsync(objectName);

            // Assert
            Assert.Equal(2, result);
        }

        [Fact]
        public async Task Should_use_lowercase_object_name_as_prefix()
        {
            // Arrange
            var objectName = "PROJECT";
            containerClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(true, Substitute.For<Response>()));

            var mockAsyncPageable = CreateMockAsyncEnumerable([]);
            containerClient.GetBlobsAsync(Arg.Any<BlobTraits>(), Arg.Any<BlobStates>(), prefix: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
                .Returns(mockAsyncPageable);

            // Act
            await sut.CountAsync(objectName);

            // Assert
            containerClient.Received().GetBlobsAsync(
                Arg.Any<BlobTraits>(), Arg.Any<BlobStates>(),
                prefix: "project/",
                cancellationToken: Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_object_name_is_empty_or_whitespace(string objectName)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.CountAsync(objectName));
        }

        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.CountAsync(null!));
        }

        private static AsyncPageable<BlobItem> CreateMockAsyncEnumerable(BlobItem[] items)
        {
            var mockPageable = Substitute.For<AsyncPageable<BlobItem>>();
            mockPageable.GetAsyncEnumerator(Arg.Any<CancellationToken>())
                .Returns(CreateAsyncEnumerator(items));
            return mockPageable;
        }

        private static async IAsyncEnumerator<BlobItem> CreateAsyncEnumerator(BlobItem[] items)
        {
            foreach (var item in items)
            {
                yield return item;
            }
            await Task.CompletedTask;
        }
    }
}
