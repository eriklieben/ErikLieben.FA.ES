using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Integration;

[Collection("CosmosDb")]
[Trait("Category", "Integration")]
public class CosmosDbTagStoreIntegrationTests : IAsyncLifetime
{
    private readonly CosmosDbContainerFixture _fixture;
    private readonly EventStreamCosmosDbSettings _settings;
    private Database? _database;

    public CosmosDbTagStoreIntegrationTests(CosmosDbContainerFixture fixture)
    {
        _fixture = fixture;
        _settings = new EventStreamCosmosDbSettings
        {
            DatabaseName = $"tagstore_{Guid.NewGuid():N}",
            TagsContainerName = "tags",
            AutoCreateContainers = true
        };
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

    public class DocumentTagStore : CosmosDbTagStoreIntegrationTests
    {
        public DocumentTagStore(CosmosDbContainerFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Should_set_and_get_document_tag()
        {
            // Arrange
            var sut = new CosmosDbDocumentTagStore(_fixture.CosmosClient!, _settings);
            var document = CreateObjectDocument("doc-001");

            // Act
            await sut.SetAsync(document, "priority-high");
            var result = await sut.GetAsync("TestObject", "priority-high");

            // Assert
            Assert.Contains("doc-001", result);
        }

        [Fact]
        public async Task Should_return_multiple_documents_with_same_tag()
        {
            // Arrange
            var sut = new CosmosDbDocumentTagStore(_fixture.CosmosClient!, _settings);

            var doc1 = CreateObjectDocument("doc-002");
            var doc2 = CreateObjectDocument("doc-003");

            await sut.SetAsync(doc1, "shared-tag");
            await sut.SetAsync(doc2, "shared-tag");

            // Act
            var result = await sut.GetAsync("TestObject", "shared-tag");

            // Assert
            Assert.Equal(2, result.Count());
            Assert.Contains("doc-002", result);
            Assert.Contains("doc-003", result);
        }

        [Fact]
        public async Task Should_return_empty_for_non_existent_tag()
        {
            // Arrange
            var sut = new CosmosDbDocumentTagStore(_fixture.CosmosClient!, _settings);

            // Act
            var result = await sut.GetAsync("TestObject", "non-existent-tag");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_update_existing_tag()
        {
            // Arrange
            var sut = new CosmosDbDocumentTagStore(_fixture.CosmosClient!, _settings);
            var document = CreateObjectDocument("doc-004");

            // Set tag twice (should upsert)
            await sut.SetAsync(document, "update-tag");
            await sut.SetAsync(document, "update-tag");

            // Act
            var result = await sut.GetAsync("TestObject", "update-tag");

            // Assert - should only have one entry, not two
            Assert.Single(result);
        }

        [Fact]
        public async Task Should_handle_tags_with_special_characters()
        {
            // Arrange
            var sut = new CosmosDbDocumentTagStore(_fixture.CosmosClient!, _settings);
            var document = CreateObjectDocument("doc-005");

            // Act
            await sut.SetAsync(document, "tag/with/slashes");
            var result = await sut.GetAsync("TestObject", "tag/with/slashes");

            // Assert
            Assert.Contains("doc-005", result);
        }
    }

    public class StreamTagStore : CosmosDbTagStoreIntegrationTests
    {
        public StreamTagStore(CosmosDbContainerFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Should_set_and_get_stream_tag()
        {
            // Arrange
            var sut = new CosmosDbStreamTagStore(_fixture.CosmosClient!, _settings);
            var document = CreateObjectDocument("stream-001");

            // Act
            await sut.SetAsync(document, "active");
            var result = await sut.GetAsync("TestObject", "active");

            // Assert
            Assert.Contains("stream-001", result);
        }

        [Fact]
        public async Task Should_return_multiple_streams_with_same_tag()
        {
            // Arrange
            var sut = new CosmosDbStreamTagStore(_fixture.CosmosClient!, _settings);

            var stream1 = CreateObjectDocument("stream-002");
            var stream2 = CreateObjectDocument("stream-003");

            await sut.SetAsync(stream1, "pending");
            await sut.SetAsync(stream2, "pending");

            // Act
            var result = await sut.GetAsync("TestObject", "pending");

            // Assert
            Assert.Equal(2, result.Count());
        }
    }

    private static IObjectDocument CreateObjectDocument(string objectId)
    {
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = $"{objectId}-stream",
            StreamType = "cosmosdb",
            CurrentStreamVersion = 0
        };

        var document = Substitute.For<IObjectDocument>();
        document.ObjectName.Returns("TestObject");
        document.ObjectId.Returns(objectId);
        document.Active.Returns(streamInfo);
        document.TerminatedStreams.Returns([]);

        return document;
    }
}
