using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Exceptions;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Integration;

[Collection("CosmosDb")]
[Trait("Category", "Integration")]
public class CosmosDbDocumentStoreIntegrationTests : IAsyncLifetime
{
    private readonly CosmosDbContainerFixture _fixture;
    private readonly EventStreamCosmosDbSettings _settings;
    private readonly EventStreamDefaultTypeSettings _typeSettings;
    private readonly IDocumentTagDocumentFactory _documentTagFactory;
    private Database? _database;

    public CosmosDbDocumentStoreIntegrationTests(CosmosDbContainerFixture fixture)
    {
        _fixture = fixture;
        _settings = new EventStreamCosmosDbSettings
        {
            DatabaseName = $"docstore_{Guid.NewGuid():N}",
            DocumentsContainerName = "documents",
            AutoCreateContainers = true
        };
        _typeSettings = new EventStreamDefaultTypeSettings();
        _documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
    }

    public async Task InitializeAsync()
    {
        _database = (await _fixture.CosmosClient!.CreateDatabaseIfNotExistsAsync(_settings.DatabaseName)).Database;
    }

    public async Task DisposeAsync()
    {
        if (_database != null)
        {
            try
            {
                await _database.DeleteAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task Should_create_new_document()
    {
        // Arrange
        var sut = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, _settings, _typeSettings);

        // Act
        var result = await sut.CreateAsync("TestObject", "test-id-001");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-id-001", result.ObjectId);
        Assert.Equal("testobject", result.ObjectName); // ObjectName is stored lowercase for partition key consistency
        Assert.NotNull(result.Active);
        Assert.Contains("testid001", result.Active.StreamIdentifier);
    }

    [Fact]
    public async Task Should_return_existing_document_on_create()
    {
        // Arrange
        var sut = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, _settings, _typeSettings);

        // Create first
        var first = await sut.CreateAsync("TestObject", "test-id-002");

        // Act - try to create again
        var second = await sut.CreateAsync("TestObject", "test-id-002");

        // Assert
        Assert.NotNull(second);
        Assert.Equal(first!.Active.StreamIdentifier, second!.Active.StreamIdentifier);
    }

    [Fact]
    public async Task Should_get_existing_document()
    {
        // Arrange
        var sut = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, _settings, _typeSettings);
        await sut.CreateAsync("TestObject", "test-id-003");

        // Act
        var result = await sut.GetAsync("TestObject", "test-id-003");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-id-003", result.ObjectId);
    }

    [Fact]
    public async Task Should_throw_when_document_not_found()
    {
        // Arrange
        var sut = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, _settings, _typeSettings);

        // Act & Assert
        await Assert.ThrowsAsync<CosmosDbDocumentNotFoundException>(
            () => sut.GetAsync("TestObject", "non-existent-id"));
    }

    [Fact]
    public async Task Should_update_document_with_set()
    {
        // Arrange
        var sut = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, _settings, _typeSettings);
        var created = await sut.CreateAsync("TestObject", "test-id-004");

        // Modify the stream version
        created!.Active.CurrentStreamVersion = 10;

        // Act
        await sut.SetAsync(created);

        // Assert
        var retrieved = await sut.GetAsync("TestObject", "test-id-004");
        Assert.Equal(10, retrieved.Active.CurrentStreamVersion);
    }

    [Fact]
    public async Task Should_create_document_with_correct_stream_info()
    {
        // Arrange
        var sut = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, _settings, _typeSettings);

        // Act
        var result = await sut.CreateAsync("Order", "order-123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("cosmosdb", result.Active.StreamType);
        Assert.Equal("cosmosdb", result.Active.DocumentType);
        Assert.Equal(-1, result.Active.CurrentStreamVersion);
    }

    [Fact]
    public async Task Should_handle_optimistic_concurrency()
    {
        // Arrange
        var settingsWithConcurrency = new EventStreamCosmosDbSettings
        {
            DatabaseName = _settings.DatabaseName,
            DocumentsContainerName = _settings.DocumentsContainerName,
            AutoCreateContainers = true,
            UseOptimisticConcurrency = true
        };

        var sut = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, settingsWithConcurrency, _typeSettings);
        var created = await sut.CreateAsync("TestObject", "test-id-005");

        // Update with hash
        created!.Active.CurrentStreamVersion = 5;
        await sut.SetAsync(created);

        // Act - update again
        var retrieved = await sut.GetAsync("TestObject", "test-id-005");
        retrieved.Active.CurrentStreamVersion = 10;
        await sut.SetAsync(retrieved);

        // Assert
        var final = await sut.GetAsync("TestObject", "test-id-005");
        Assert.Equal(10, final.Active.CurrentStreamVersion);
    }

    [Fact]
    public async Task Should_preserve_terminated_streams()
    {
        // Arrange
        var sut = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, _settings, _typeSettings);
        var created = await sut.CreateAsync("TestObject", "test-id-006");

        // Add a terminated stream
        var terminatedStreams = new List<TerminatedStream>
        {
            new TerminatedStream
            {
                StreamIdentifier = "old-stream",
                StreamVersion = 100,
                TerminationDate = DateTimeOffset.UtcNow,
                Reason = "Migrated"
            }
        };

        // Create a document mock with terminated streams
        // Note: ObjectName must be lowercase to match the stored document
        var document = Substitute.For<IObjectDocument>();
        document.ObjectName.Returns("testobject");
        document.ObjectId.Returns("test-id-006");
        document.Hash.Returns((string?)null);
        document.SchemaVersion.Returns("1.0.0");
        document.Active.Returns(created!.Active);
        document.TerminatedStreams.Returns(terminatedStreams);

        // Act
        await sut.SetAsync(document);

        // Assert
        var retrieved = await sut.GetAsync("TestObject", "test-id-006");
        Assert.Single(retrieved.TerminatedStreams);
        Assert.Equal("old-stream", retrieved.TerminatedStreams.First().StreamIdentifier);
    }
}
