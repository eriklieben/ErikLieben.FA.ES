using DotNet.Testcontainers.Containers;
using Microsoft.Azure.Cosmos;
using Testcontainers.CosmosDb;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Integration;

/// <summary>
/// Shared fixture for CosmosDB emulator container.
/// This fixture is shared across all tests in the collection to avoid starting
/// a new container for each test class (which would be slow).
/// Uses the vnext-preview image which works correctly in GitHub Actions.
/// </summary>
public class CosmosDbContainerFixture : IAsyncLifetime
{
    private readonly CosmosDbContainer _cosmosDbContainer;

    public CosmosDbContainerFixture()
    {
        // Use vnext-preview image which works in GitHub Actions CI
        // See: https://github.com/AzureCosmosDB/cosmosdb-linux-emulator-github-actions
        _cosmosDbContainer = new CosmosDbBuilder()
            .WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview")
            .Build();
    }

    public CosmosClient? CosmosClient { get; private set; }
    public string ConnectionString => _cosmosDbContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _cosmosDbContainer.StartAsync();

        // Small delay to ensure emulator is fully ready
        await Task.Delay(2000);

        var cosmosClientOptions = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            HttpClientFactory = () => _cosmosDbContainer.HttpClient
        };

        CosmosClient = new CosmosClient(ConnectionString, cosmosClientOptions);
    }

    public async Task DisposeAsync()
    {
        CosmosClient?.Dispose();
        await _cosmosDbContainer.DisposeAsync();
    }
}

/// <summary>
/// Collection definition for sharing the CosmosDB container across test classes.
/// </summary>
[CollectionDefinition("CosmosDb")]
public class CosmosDbCollection : ICollectionFixture<CosmosDbContainerFixture>
{
}
