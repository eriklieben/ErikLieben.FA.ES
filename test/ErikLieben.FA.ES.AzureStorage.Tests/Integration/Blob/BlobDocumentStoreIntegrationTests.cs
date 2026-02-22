#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks

using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using NSubstitute;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Integration.Blob;

/// <summary>
/// Integration tests for BlobDocumentStore using Azurite TestContainer.
/// Tests object document CRUD operations against real blob storage.
/// </summary>
[Collection("AzuriteIntegration")]
[Trait("Category", "Integration")]
[Trait("Feature", "BlobStorage")]
public class BlobDocumentStoreIntegrationTests : IAsyncLifetime
{
    private readonly AzuriteContainerFixture _fixture;
    private readonly string _testId;
    private BlobDocumentStore? _documentStore;
    private EventStreamBlobSettings? _blobSettings;

    public BlobDocumentStoreIntegrationTests(AzuriteContainerFixture fixture)
    {
        _fixture = fixture;
        _testId = Guid.NewGuid().ToString("N")[..8];
    }

    public async Task InitializeAsync()
    {
        var blobClientFactory = CreateBlobClientFactory(_fixture.BlobServiceClient!);

        _blobSettings = new EventStreamBlobSettings(
            defaultDataStore: "default",
            defaultDocumentStore: "default",
            autoCreateContainer: true,
            defaultDocumentContainerName: "object-document-store");

        var typeSettings = new EventStreamDefaultTypeSettings("blob");

        // Create a mock document tag store factory
        var documentTagStoreFactory = Substitute.For<IDocumentTagDocumentFactory>();

        _documentStore = new BlobDocumentStore(
            blobClientFactory,
            documentTagStoreFactory,
            _blobSettings,
            typeSettings);

        // Pre-create the container
        var container = _fixture.BlobServiceClient!.GetBlobContainerClient("object-document-store");
        await container.CreateIfNotExistsAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Should_create_new_document()
    {
        // Arrange
        var objectName = "Order";
        var objectId = $"order-{_testId}-new";

        // Act
        var document = await _documentStore!.CreateAsync(objectName, objectId);

        // Assert
        Assert.NotNull(document);
        Assert.Equal(objectName, document.ObjectName);
        Assert.Equal(objectId, document.ObjectId);
        Assert.NotNull(document.Active);
        Assert.Contains(objectId.Replace("-", string.Empty), document.Active.StreamIdentifier);
    }

    [Fact]
    public async Task Should_return_existing_document_on_create_if_exists()
    {
        // Arrange
        var objectName = "Customer";
        var objectId = $"customer-{_testId}-exists";

        // Create document first
        var originalDoc = await _documentStore!.CreateAsync(objectName, objectId);

        // Act - create again with same id
        var existingDoc = await _documentStore.CreateAsync(objectName, objectId);

        // Assert
        Assert.NotNull(existingDoc);
        Assert.Equal(originalDoc.ObjectId, existingDoc.ObjectId);
        Assert.Equal(originalDoc.Active.StreamIdentifier, existingDoc.Active.StreamIdentifier);
    }

    [Fact]
    public async Task Should_get_existing_document()
    {
        // Arrange
        var objectName = "Product";
        var objectId = $"product-{_testId}-get";

        await _documentStore!.CreateAsync(objectName, objectId);

        // Act
        var document = await _documentStore.GetAsync(objectName, objectId);

        // Assert
        Assert.NotNull(document);
        Assert.Equal(objectName, document.ObjectName);
        Assert.Equal(objectId, document.ObjectId);
    }

    [Fact]
    public async Task Should_throw_when_document_not_found()
    {
        // Arrange
        var objectName = "Inventory";
        var objectId = $"inventory-{_testId}-notfound";

        // Act & Assert
        await Assert.ThrowsAsync<BlobDocumentNotFoundException>(async () =>
        {
            await _documentStore!.GetAsync(objectName, objectId);
        });
    }

    [Fact]
    public async Task Should_set_and_update_document()
    {
        // Arrange
        var objectName = "Invoice";
        var objectId = $"invoice-{_testId}-set";

        var document = await _documentStore!.CreateAsync(objectName, objectId);
        var originalVersion = document.Active.CurrentStreamVersion;

        // Modify the document
        document.Active.CurrentStreamVersion = 5;

        // Act
        await _documentStore.SetAsync(document);

        // Retrieve the updated document
        var updatedDoc = await _documentStore.GetAsync(objectName, objectId);

        // Assert
        Assert.NotNull(updatedDoc);
        Assert.Equal(5, updatedDoc.Active.CurrentStreamVersion);
    }

    [Fact]
    public async Task Should_initialize_document_with_correct_type_settings()
    {
        // Arrange
        var objectName = "Shipment";
        var objectId = $"shipment-{_testId}-types";

        // Act
        var document = await _documentStore!.CreateAsync(objectName, objectId);

        // Assert - verify type settings are applied
        Assert.Equal("blob", document.Active.StreamType);
        Assert.Equal("blob", document.Active.DocumentType);
        Assert.Equal("blob", document.Active.DocumentTagType);
        Assert.Equal("blob", document.Active.EventStreamTagType);
    }

    [Fact]
    public async Task Should_initialize_document_with_correct_store_settings()
    {
        // Arrange
        var objectName = "Payment";
        var objectId = $"payment-{_testId}-stores";

        // Act
        var document = await _documentStore!.CreateAsync(objectName, objectId);

        // Assert - verify store settings are applied
        Assert.Equal("default", document.Active.DataStore);
        Assert.Equal("default", document.Active.DocumentStore);
    }

    [Fact]
    public async Task Should_handle_special_characters_in_object_id()
    {
        // Arrange
        var objectName = "Batch";
        var objectId = $"batch-{_testId}-special-chars";

        // Act
        var document = await _documentStore!.CreateAsync(objectName, objectId);
        var retrieved = await _documentStore.GetAsync(objectName, objectId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(objectId, retrieved.ObjectId);
    }

    [Fact]
    public async Task Should_start_with_stream_version_minus_one()
    {
        // Arrange
        var objectName = "Account";
        var objectId = $"account-{_testId}-version";

        // Act
        var document = await _documentStore!.CreateAsync(objectName, objectId);

        // Assert - new streams start at version -1 (no events)
        Assert.Equal(-1, document.Active.CurrentStreamVersion);
    }

    [Fact]
    public async Task Should_generate_valid_stream_identifier()
    {
        // Arrange
        var objectName = "Transaction";
        var objectId = $"trans-{_testId}";

        // Act
        var document = await _documentStore!.CreateAsync(objectName, objectId);

        // Assert - stream identifier should be based on object id without dashes, plus suffix
        Assert.NotNull(document.Active.StreamIdentifier);
        Assert.EndsWith("-0000000000", document.Active.StreamIdentifier);
    }

    [Fact]
    public async Task Should_preserve_hash_after_set()
    {
        // Arrange
        var objectName = "Refund";
        var objectId = $"refund-{_testId}-hash";

        var document = await _documentStore!.CreateAsync(objectName, objectId);

        // Modify and save
        document.Active.CurrentStreamVersion = 10;
        await _documentStore.SetAsync(document);

        // Act - get the document again
        var retrieved = await _documentStore.GetAsync(objectName, objectId);

        // Assert - hash should be set (not null or empty)
        Assert.NotNull(retrieved.Hash);
        Assert.NotEmpty(retrieved.Hash);
    }

    private static IAzureClientFactory<BlobServiceClient> CreateBlobClientFactory(BlobServiceClient client)
    {
        var factory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
        factory.CreateClient(Arg.Any<string>()).Returns(client);
        return factory;
    }
}
