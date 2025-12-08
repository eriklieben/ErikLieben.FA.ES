#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8620 // Argument nullability mismatch - NSubstitute mock setup

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableObjectDocumentFactoryTests
{
    public class Constructor
    {
        [Fact]
        public void Should_initialize_with_component_parameters()
        {
            // Arrange
            var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
            var documentTagStore = Substitute.For<IDocumentTagDocumentFactory>();
            var settings = new EventStreamDefaultTypeSettings();
            var tableSettings = new EventStreamTableSettings("table");

            // Act
            var sut = new TableObjectDocumentFactory(clientFactory, documentTagStore, settings, tableSettings);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_initialize_with_tableDocumentStore()
        {
            // Arrange
            var tableDocumentStore = Substitute.For<ITableDocumentStore>();

            // Act
            var sut = new TableObjectDocumentFactory(tableDocumentStore);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_throw_when_tableDocumentStore_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TableObjectDocumentFactory(null!));
        }
    }

    public class GetOrCreateAsyncMethod
    {
        private readonly ITableDocumentStore tableDocumentStore;
        private readonly TableObjectDocumentFactory sut;

        public GetOrCreateAsyncMethod()
        {
            tableDocumentStore = Substitute.For<ITableDocumentStore>();
            sut = new TableObjectDocumentFactory(tableDocumentStore);
        }

        [Fact]
        public async Task Should_call_create_async_with_lowercase_object_name()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "id-123";
            var expectedObjectDocument = Substitute.For<IObjectDocument>();
            tableDocumentStore.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(expectedObjectDocument);

            // Act
            var result = await sut.GetOrCreateAsync(objectName, objectId);

            // Assert
            await tableDocumentStore.Received(1).CreateAsync(objectName.ToLowerInvariant(), objectId, null);
            Assert.Equal(expectedObjectDocument, result);
        }

        [Fact]
        public async Task Should_pass_store_parameter_to_document_store()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "id-123";
            var store = "custom-store";
            var expectedObjectDocument = Substitute.For<IObjectDocument>();
            tableDocumentStore.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(expectedObjectDocument);

            // Act
            var result = await sut.GetOrCreateAsync(objectName, objectId, store);

            // Assert
            await tableDocumentStore.Received(1).CreateAsync(objectName.ToLowerInvariant(), objectId, store);
        }

        [Fact]
        public async Task Should_ignore_documentType_parameter()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "id-123";
            var documentType = "blob"; // Should be ignored for Table factory
            var expectedObjectDocument = Substitute.For<IObjectDocument>();
            tableDocumentStore.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(expectedObjectDocument);

            // Act
            var result = await sut.GetOrCreateAsync(objectName, objectId, null, documentType);

            // Assert
            await tableDocumentStore.Received(1).CreateAsync(objectName.ToLowerInvariant(), objectId, null);
            Assert.Equal(expectedObjectDocument, result);
        }

        [Theory]
        [InlineData(null!)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_object_name_is_invalid(string? objectName)
        {
            // Arrange
            var objectId = "id-123";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetOrCreateAsync(objectName!, objectId));
        }

        [Theory]
        [InlineData(null!)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_object_id_is_invalid(string? objectId)
        {
            // Arrange
            var objectName = "TestObject";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetOrCreateAsync(objectName, objectId!));
        }

        [Fact]
        public async Task Should_throw_when_document_store_returns_null()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "id-123";
            tableDocumentStore.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns((IObjectDocument?)null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetOrCreateAsync(objectName, objectId));
        }
    }

    public class GetAsyncMethod
    {
        private readonly ITableDocumentStore tableDocumentStore;
        private readonly TableObjectDocumentFactory sut;

        public GetAsyncMethod()
        {
            tableDocumentStore = Substitute.For<ITableDocumentStore>();
            sut = new TableObjectDocumentFactory(tableDocumentStore);
        }

        [Fact]
        public async Task Should_call_get_async_with_lowercase_object_name()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "id-123";
            var expectedObjectDocument = Substitute.For<IObjectDocument>();
            tableDocumentStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(expectedObjectDocument);

            // Act
            var result = await sut.GetAsync(objectName, objectId);

            // Assert
            await tableDocumentStore.Received(1).GetAsync(objectName.ToLowerInvariant(), objectId, null);
            Assert.Equal(expectedObjectDocument, result);
        }

        [Fact]
        public async Task Should_pass_store_parameter_to_document_store()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "id-123";
            var store = "custom-store";
            var expectedObjectDocument = Substitute.For<IObjectDocument>();
            tableDocumentStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(expectedObjectDocument);

            // Act
            var result = await sut.GetAsync(objectName, objectId, store);

            // Assert
            await tableDocumentStore.Received(1).GetAsync(objectName.ToLowerInvariant(), objectId, store);
        }

        [Fact]
        public async Task Should_ignore_documentType_parameter()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "id-123";
            var documentType = "blob"; // Should be ignored for Table factory
            var expectedObjectDocument = Substitute.For<IObjectDocument>();
            tableDocumentStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(expectedObjectDocument);

            // Act
            var result = await sut.GetAsync(objectName, objectId, null, documentType);

            // Assert
            await tableDocumentStore.Received(1).GetAsync(objectName.ToLowerInvariant(), objectId, null);
            Assert.Equal(expectedObjectDocument, result);
        }

        [Theory]
        [InlineData(null!)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_object_name_is_invalid(string? objectName)
        {
            // Arrange
            var objectId = "id-123";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetAsync(objectName!, objectId));
        }

        [Theory]
        [InlineData(null!)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_object_id_is_invalid(string? objectId)
        {
            // Arrange
            var objectName = "TestObject";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetAsync(objectName, objectId!));
        }

        [Fact]
        public async Task Should_throw_when_document_store_returns_null()
        {
            // Arrange
            var objectName = "TestObject";
            var objectId = "id-123";
            tableDocumentStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns((IObjectDocument?)null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetAsync(objectName, objectId));
        }
    }

    public class SetAsyncMethod
    {
        private readonly ITableDocumentStore tableDocumentStore;
        private readonly TableObjectDocumentFactory sut;

        public SetAsyncMethod()
        {
            tableDocumentStore = Substitute.For<ITableDocumentStore>();
            sut = new TableObjectDocumentFactory(tableDocumentStore);
        }

        [Fact]
        public async Task Should_call_set_async_on_document_store()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();

            // Act
            await sut.SetAsync(document);

            // Assert
            await tableDocumentStore.Received(1).SetAsync(document);
        }

        [Fact]
        public async Task Should_ignore_store_parameter()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();
            var store = "custom-store";

            // Act
            await sut.SetAsync(document, store);

            // Assert
            await tableDocumentStore.Received(1).SetAsync(document);
        }

        [Fact]
        public async Task Should_ignore_documentType_parameter()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();
            var documentType = "blob"; // Should be ignored for Table factory

            // Act
            await sut.SetAsync(document, null, documentType);

            // Assert
            await tableDocumentStore.Received(1).SetAsync(document);
        }

        [Fact]
        public async Task Should_throw_when_document_is_null()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SetAsync(null!));
        }
    }

    public class GetFirstByObjectDocumentTagMethod
    {
        private readonly ITableDocumentStore tableDocumentStore;
        private readonly TableObjectDocumentFactory sut;

        public GetFirstByObjectDocumentTagMethod()
        {
            tableDocumentStore = Substitute.For<ITableDocumentStore>();
            sut = new TableObjectDocumentFactory(tableDocumentStore);
        }

        [Fact]
        public async Task Should_call_get_first_by_tag_on_document_store()
        {
            // Arrange
            var objectName = "TestObject";
            var tag = "test-tag";
            var expectedDocument = Substitute.For<IObjectDocument>();
            tableDocumentStore.GetFirstByDocumentByTagAsync(objectName, tag, null, null)
                .Returns(expectedDocument);

            // Act
            var result = await sut.GetFirstByObjectDocumentTag(objectName, tag);

            // Assert
            await tableDocumentStore.Received(1).GetFirstByDocumentByTagAsync(objectName, tag, null, null);
            Assert.Equal(expectedDocument, result);
        }

        [Fact]
        public async Task Should_pass_optional_parameters_to_document_store()
        {
            // Arrange
            var objectName = "TestObject";
            var tag = "test-tag";
            var documentTagStore = "custom-tag-store";
            var store = "custom-store";
            var expectedDocument = Substitute.For<IObjectDocument>();
            tableDocumentStore.GetFirstByDocumentByTagAsync(objectName, tag, documentTagStore, store)
                .Returns(expectedDocument);

            // Act
            var result = await sut.GetFirstByObjectDocumentTag(objectName, tag, documentTagStore, store);

            // Assert
            await tableDocumentStore.Received(1).GetFirstByDocumentByTagAsync(objectName, tag, documentTagStore, store);
        }

        [Fact]
        public async Task Should_return_null_when_no_document_found()
        {
            // Arrange
            var objectName = "TestObject";
            var tag = "non-existent-tag";
            tableDocumentStore.GetFirstByDocumentByTagAsync(objectName, tag, null, null)
                .Returns((IObjectDocument?)null);

            // Act
            var result = await sut.GetFirstByObjectDocumentTag(objectName, tag);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData(null!)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_object_name_is_invalid(string? objectName)
        {
            // Arrange
            var tag = "test-tag";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetFirstByObjectDocumentTag(objectName!, tag));
        }

        [Theory]
        [InlineData(null!)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_tag_is_invalid(string? tag)
        {
            // Arrange
            var objectName = "TestObject";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetFirstByObjectDocumentTag(objectName, tag!));
        }
    }

    public class GetByObjectDocumentTagMethod
    {
        private readonly ITableDocumentStore tableDocumentStore;
        private readonly TableObjectDocumentFactory sut;

        public GetByObjectDocumentTagMethod()
        {
            tableDocumentStore = Substitute.For<ITableDocumentStore>();
            sut = new TableObjectDocumentFactory(tableDocumentStore);
        }

        [Fact]
        public async Task Should_call_get_by_tag_on_document_store()
        {
            // Arrange
            var objectName = "TestObject";
            var tag = "test-tag";
            var doc1 = Substitute.For<IObjectDocument>();
            var doc2 = Substitute.For<IObjectDocument>();
            var expectedDocuments = new List<IObjectDocument> { doc1, doc2 };
            tableDocumentStore.GetByDocumentByTagAsync(objectName, tag, null, null)
                .Returns(expectedDocuments);

            // Act
            var result = await sut.GetByObjectDocumentTag(objectName, tag);

            // Assert
            await tableDocumentStore.Received(1).GetByDocumentByTagAsync(objectName, tag, null, null);
            Assert.Equal(expectedDocuments, result);
        }

        [Fact]
        public async Task Should_pass_optional_parameters_to_document_store()
        {
            // Arrange
            var objectName = "TestObject";
            var tag = "test-tag";
            var documentTagStore = "custom-tag-store";
            var store = "custom-store";
            var expectedDocuments = new List<IObjectDocument>();
            tableDocumentStore.GetByDocumentByTagAsync(objectName, tag, documentTagStore, store)
                .Returns(expectedDocuments);

            // Act
            var result = await sut.GetByObjectDocumentTag(objectName, tag, documentTagStore, store);

            // Assert
            await tableDocumentStore.Received(1).GetByDocumentByTagAsync(objectName, tag, documentTagStore, store);
        }

        [Fact]
        public async Task Should_return_empty_collection_when_no_documents_found()
        {
            // Arrange
            var objectName = "TestObject";
            var tag = "non-existent-tag";
            tableDocumentStore.GetByDocumentByTagAsync(objectName, tag, null, null)
                .Returns(new List<IObjectDocument>());

            // Act
            var result = await sut.GetByObjectDocumentTag(objectName, tag);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_return_empty_collection_when_document_store_returns_null()
        {
            // Arrange
            var objectName = "TestObject";
            var tag = "test-tag";
            tableDocumentStore.GetByDocumentByTagAsync(objectName, tag, null, null)
                .Returns((IEnumerable<IObjectDocument>?)null);

            // Act
            var result = await sut.GetByObjectDocumentTag(objectName, tag);

            // Assert
            Assert.Empty(result);
        }

        [Theory]
        [InlineData(null!)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_object_name_is_invalid(string? objectName)
        {
            // Arrange
            var tag = "test-tag";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetByObjectDocumentTag(objectName!, tag));
        }

        [Theory]
        [InlineData(null!)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Should_throw_when_tag_is_invalid(string? tag)
        {
            // Arrange
            var objectName = "TestObject";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetByObjectDocumentTag(objectName, tag!));
        }
    }
}
