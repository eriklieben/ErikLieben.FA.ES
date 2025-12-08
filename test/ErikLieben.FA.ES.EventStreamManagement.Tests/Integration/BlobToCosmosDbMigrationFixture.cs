namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Integration;

/// <summary>
/// Combined fixture for Blob to CosmosDB migration tests.
/// Manages both Azurite (source) and CosmosDB (target) containers.
/// </summary>
public class BlobToCosmosDbMigrationFixture : IAsyncLifetime
{
    public AzuriteContainerFixture Azurite { get; } = new();
    public CosmosDbContainerFixture CosmosDb { get; } = new();

    public async Task InitializeAsync()
    {
        // Start both containers in parallel for faster initialization
        await Task.WhenAll(
            Azurite.InitializeAsync(),
            CosmosDb.InitializeAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            Azurite.DisposeAsync(),
            CosmosDb.DisposeAsync());
    }
}

/// <summary>
/// Collection definition for sharing the migration fixtures across test classes.
/// </summary>
[CollectionDefinition("BlobToCosmosDbMigration")]
public class BlobToCosmosDbMigrationCollection : ICollectionFixture<BlobToCosmosDbMigrationFixture>
{
}
