using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using ErikLieben.FA.ES.CosmosDb.Configuration;
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
        // See: https://github.com/testcontainers/testcontainers-dotnet/discussions/1306
        _cosmosDbContainer = new CosmosDbBuilder()
            .WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview")
            .WithCommand("--protocol", "https")
            .WithEnvironment("ENABLE_EXPLORER", "false")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .AddCustomWaitStrategy(new CosmosDbReadyWaitStrategy()))
            .Build();
    }

    public CosmosClient? CosmosClient { get; private set; }
    public string ConnectionString => _cosmosDbContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _cosmosDbContainer.StartAsync();

        // Use the same AOT-compatible serializer as production via factory
        var cosmosClientOptions = CosmosClientOptionsFactory.CreateForDevelopment(
            () => _cosmosDbContainer.HttpClient);

        CosmosClient = new CosmosClient(ConnectionString, cosmosClientOptions);
    }

    public async Task DisposeAsync()
    {
        CosmosClient?.Dispose();
        await _cosmosDbContainer.DisposeAsync();
    }
}

/// <summary>
/// Custom wait strategy for the CosmosDB vnext-preview emulator.
/// Waits for the emulator to be ready by checking HTTP endpoint and adding stability delay.
/// </summary>
internal sealed class CosmosDbReadyWaitStrategy : IWaitUntil
{
    public async Task<bool> UntilAsync(IContainer container)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri($"https://{container.Hostname}:{container.GetMappedPublicPort(8081)}")
        };

        try
        {
            using var response = await client.GetAsync("/").ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                // Stability delay recommended for vnext-preview - use longer delay
                // to ensure eventual consistency is stable before tests run
                await Task.Delay(3000).ConfigureAwait(false);
                return true;
            }
        }
        catch
        {
            // Emulator not ready yet
        }

        return false;
    }
}

/// <summary>
/// Collection definition for sharing the CosmosDB container across test classes.
/// </summary>
[CollectionDefinition("CosmosDb")]
public class CosmosDbCollection : ICollectionFixture<CosmosDbContainerFixture>
{
}
