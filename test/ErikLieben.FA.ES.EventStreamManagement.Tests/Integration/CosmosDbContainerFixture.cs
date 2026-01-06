using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Testcontainers.CosmosDb;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Integration;

/// <summary>
/// Shared fixture for CosmosDB emulator container.
/// Provides CosmosDB for target event streams in migration tests.
/// </summary>
/// <remarks>
/// Uses the standard CosmosDB emulator image for local testing.
/// For CI environments with limited resources, consider using the vnext-preview image
/// which has lower memory requirements.
/// </remarks>
public class CosmosDbContainerFixture : IAsyncLifetime
{
    private readonly CosmosDbContainer _cosmosDbContainer;

    public CosmosDbContainerFixture()
    {
        // Use standard CosmosDB emulator with default TestContainers configuration
        // This uses the built-in wait strategy and SSL handling
        _cosmosDbContainer = new CosmosDbBuilder()
            .Build();
    }

    public CosmosClient? CosmosClient { get; private set; }
    public string ConnectionString => _cosmosDbContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _cosmosDbContainer.StartAsync();

        var cosmosClientOptions = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            HttpClientFactory = () => _cosmosDbContainer.HttpClient,
            // Use System.Text.Json to recognize [JsonPropertyName] attributes
            UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }
        };

        CosmosClient = new CosmosClient(ConnectionString, cosmosClientOptions);
    }

    public async Task DisposeAsync()
    {
        CosmosClient?.Dispose();
        await _cosmosDbContainer.DisposeAsync();
    }
}
