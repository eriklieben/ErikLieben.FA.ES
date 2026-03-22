namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Integration;

/// <summary>
/// Combined fixture for Blob to CosmosDB migration tests.
/// Manages both Azurite (source) and CosmosDB (target) containers.
/// </summary>
public class BlobToCosmosDbMigrationFixture : IAsyncLifetime
{
    public AzuriteContainerFixture Azurite { get; } = new();
    public CosmosDbContainerFixture CosmosDb { get; } = new();

    public async ValueTask InitializeAsync()
    {
        // Start both containers in parallel for faster initialization
        await Task.WhenAll(
            Azurite.InitializeAsync().AsTask(),
            CosmosDb.InitializeAsync().AsTask());
    }

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(
            Azurite.DisposeAsync().AsTask(),
            CosmosDb.DisposeAsync().AsTask());
    }
}

/// <summary>
/// Collection definition for sharing the migration fixtures across test classes.
/// </summary>
[CollectionDefinition("BlobToCosmosDbMigration")]
public class BlobToCosmosDbMigrationCollection : ICollectionFixture<BlobToCosmosDbMigrationFixture>
{
}
