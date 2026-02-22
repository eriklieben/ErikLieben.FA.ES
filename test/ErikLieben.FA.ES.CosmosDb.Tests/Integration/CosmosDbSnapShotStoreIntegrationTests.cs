using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Integration;

[Collection("CosmosDb")]
[Trait("Category", "Integration")]
public class CosmosDbSnapShotStoreIntegrationTests : IAsyncLifetime
{
    private readonly CosmosDbContainerFixture _fixture;
    private readonly EventStreamCosmosDbSettings _settings;
    private Database? _database;

    public CosmosDbSnapShotStoreIntegrationTests(CosmosDbContainerFixture fixture)
    {
        _fixture = fixture;
        _settings = new EventStreamCosmosDbSettings
        {
            DatabaseName = $"snapshots_{Guid.NewGuid():N}",
            SnapshotsContainerName = "snapshots",
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

    [Fact]
    public async Task Should_set_and_get_snapshot()
    {
        // Arrange
        var sut = new CosmosDbSnapShotStore(_fixture.CosmosClient!, _settings);
        var document = CreateObjectDocument("snapshot-001");
        var entity = new TestEntity { Name = "Test", Value = 42 };

        // Act
        await sut.SetAsync(entity, TestJsonContext.Default.TestEntity, document, 5);
        var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 5);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task Should_return_null_for_non_existent_snapshot()
    {
        // Arrange
        var sut = new CosmosDbSnapShotStore(_fixture.CosmosClient!, _settings);
        var document = CreateObjectDocument("snapshot-non-existent");

        // Act
        var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Should_get_snapshot_at_specific_version()
    {
        // Arrange
        var sut = new CosmosDbSnapShotStore(_fixture.CosmosClient!, _settings);
        var document = CreateObjectDocument("snapshot-002");

        // Create snapshots at different versions
        await sut.SetAsync(new TestEntity { Name = "V5", Value = 5 }, TestJsonContext.Default.TestEntity, document, 5);
        await sut.SetAsync(new TestEntity { Name = "V10", Value = 10 }, TestJsonContext.Default.TestEntity, document, 10);

        // Act
        var v5Result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 5);
        var v10Result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 10);

        // Assert
        Assert.Equal("V5", v5Result?.Name);
        Assert.Equal("V10", v10Result?.Name);
    }

    [Fact]
    public async Task Should_update_existing_snapshot()
    {
        // Arrange
        var sut = new CosmosDbSnapShotStore(_fixture.CosmosClient!, _settings);
        var document = CreateObjectDocument("snapshot-003");

        await sut.SetAsync(new TestEntity { Name = "Original", Value = 1 }, TestJsonContext.Default.TestEntity, document, 5);

        // Act - update the same version
        await sut.SetAsync(new TestEntity { Name = "Updated", Value = 2 }, TestJsonContext.Default.TestEntity, document, 5);
        var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 5);

        // Assert
        Assert.Equal("Updated", result?.Name);
        Assert.Equal(2, result?.Value);
    }

    [Fact]
    public async Task Should_set_and_get_named_snapshot()
    {
        // Arrange
        var sut = new CosmosDbSnapShotStore(_fixture.CosmosClient!, _settings);
        var document = CreateObjectDocument("snapshot-004");
        var entity = new TestEntity { Name = "Named", Value = 100 };

        // Act
        await sut.SetAsync(entity, TestJsonContext.Default.TestEntity, document, 5, "aggregate-v2");
        var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 5, "aggregate-v2");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Named", result.Name);
    }

    [Fact]
    public async Task Should_differentiate_named_and_unnamed_snapshots()
    {
        // Arrange
        var sut = new CosmosDbSnapShotStore(_fixture.CosmosClient!, _settings);
        var document = CreateObjectDocument("snapshot-005");

        await sut.SetAsync(new TestEntity { Name = "Default", Value = 1 }, TestJsonContext.Default.TestEntity, document, 5);
        await sut.SetAsync(new TestEntity { Name = "Named", Value = 2 }, TestJsonContext.Default.TestEntity, document, 5, "v2");

        // Act
        var defaultResult = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 5);
        var namedResult = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 5, "v2");

        // Assert
        Assert.Equal("Default", defaultResult?.Name);
        Assert.Equal("Named", namedResult?.Name);
    }

    private static IObjectDocument CreateObjectDocument(string streamId)
    {
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = streamId,
            StreamType = "cosmosdb",
            CurrentStreamVersion = 0
        };

        var document = Substitute.For<IObjectDocument>();
        document.ObjectName.Returns("TestObject");
        document.ObjectId.Returns(streamId);
        document.Active.Returns(streamInfo);
        document.TerminatedStreams.Returns([]);

        return document;
    }
}
