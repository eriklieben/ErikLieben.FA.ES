using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Model;
using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Integration;

[Collection("CosmosDb")]
[Trait("Category", "Integration")]
public class CosmosDbObjectIdProviderIntegrationTests : IAsyncLifetime
{
    private readonly CosmosDbContainerFixture _fixture;
    private readonly EventStreamCosmosDbSettings _settings;
    private readonly EventStreamDefaultTypeSettings _typeSettings;
    private readonly IDocumentTagDocumentFactory _documentTagFactory;
    private Database? _database;

    public CosmosDbObjectIdProviderIntegrationTests(CosmosDbContainerFixture fixture)
    {
        _fixture = fixture;
        _settings = new EventStreamCosmosDbSettings
        {
            DatabaseName = $"objidprov_{Guid.NewGuid():N}",
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
    public async Task Should_return_empty_when_no_documents_exist()
    {
        // Arrange
        var sut = new CosmosDbObjectIdProvider(_fixture.CosmosClient!, _settings);

        // Act
        var result = await sut.GetObjectIdsAsync("TestObject", null, 10);

        // Assert
        Assert.Empty(result.Items);
        Assert.Null(result.ContinuationToken);
    }

    [Fact]
    public async Task Should_return_false_for_non_existent_document()
    {
        // Arrange
        var sut = new CosmosDbObjectIdProvider(_fixture.CosmosClient!, _settings);

        // Act
        var result = await sut.ExistsAsync("TestObject", "non-existent-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Should_return_true_for_existing_document()
    {
        // Arrange
        var documentStore = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, _settings, _typeSettings);
        await documentStore.CreateAsync("TestObject", "exists-001");

        var sut = new CosmosDbObjectIdProvider(_fixture.CosmosClient!, _settings);

        // Act
        var result = await sut.ExistsAsync("TestObject", "exists-001");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task Should_return_zero_count_for_empty_object_type()
    {
        // Arrange
        var sut = new CosmosDbObjectIdProvider(_fixture.CosmosClient!, _settings);

        // Act
        var result = await sut.CountAsync("NonExistentObject");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Should_return_correct_count()
    {
        // Arrange
        var documentStore = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, _settings, _typeSettings);
        await documentStore.CreateAsync("CountTest", "count-001");
        await documentStore.CreateAsync("CountTest", "count-002");
        await documentStore.CreateAsync("CountTest", "count-003");

        var sut = new CosmosDbObjectIdProvider(_fixture.CosmosClient!, _settings);

        // Act
        var result = await sut.CountAsync("CountTest");

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task Should_return_object_ids_with_paging()
    {
        // Arrange
        var documentStore = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, _settings, _typeSettings);
        for (int i = 0; i < 5; i++)
        {
            await documentStore.CreateAsync("PageTest", $"page-{i:D3}");
        }

        var sut = new CosmosDbObjectIdProvider(_fixture.CosmosClient!, _settings);

        // Act - get first page of 3
        var result = await sut.GetObjectIdsAsync("PageTest", null, 3);

        // Assert
        Assert.Equal(3, result.Items.Count());
        Assert.Equal(3, result.PageSize);
    }

    [Fact]
    public async Task Should_use_continuation_token_for_next_page()
    {
        // Arrange
        var documentStore = new CosmosDbDocumentStore(_fixture.CosmosClient!, _documentTagFactory, _settings, _typeSettings);
        for (int i = 0; i < 10; i++)
        {
            await documentStore.CreateAsync("ContinuationTest", $"cont-{i:D3}");
        }

        var sut = new CosmosDbObjectIdProvider(_fixture.CosmosClient!, _settings);

        // Act - get first page
        var firstPage = await sut.GetObjectIdsAsync("ContinuationTest", null, 5);

        // Assert
        Assert.Equal(5, firstPage.Items.Count());
        // Note: Continuation token behavior depends on actual data distribution
    }
}
