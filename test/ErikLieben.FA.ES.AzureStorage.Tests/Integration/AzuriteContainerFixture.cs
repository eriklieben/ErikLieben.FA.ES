using Azure.Data.Tables;
using Azure.Storage.Blobs;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Integration;

/// <summary>
/// Shared fixture for Azurite (Azure Storage Emulator) container.
/// Provides blob and table storage for integration tests.
/// </summary>
public class AzuriteContainerFixture : IAsyncLifetime
{
    private readonly IContainer _azuriteContainer;
    private const int BlobPort = 10000;
    private const int QueuePort = 10001;
    private const int TablePort = 10002;

    public AzuriteContainerFixture()
    {
        _azuriteContainer = new ContainerBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithPortBinding(BlobPort, true)
            .WithPortBinding(QueuePort, true)
            .WithPortBinding(TablePort, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .AddCustomWaitStrategy(new AzuriteReadyWaitStrategy(BlobPort)))
            .Build();
    }

    public BlobServiceClient? BlobServiceClient { get; private set; }
    public TableServiceClient? TableServiceClient { get; private set; }
    public string BlobConnectionString { get; private set; } = string.Empty;
    public string TableConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Azurite default account name.
    /// </summary>
    public const string AccountName = "devstoreaccount1";

    /// <summary>
    /// Azurite default account key.
    /// </summary>
    public const string AccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    public async Task InitializeAsync()
    {
        await _azuriteContainer.StartAsync();

        var host = _azuriteContainer.Hostname;
        var blobPort = _azuriteContainer.GetMappedPublicPort(BlobPort);
        var tablePort = _azuriteContainer.GetMappedPublicPort(TablePort);

        BlobConnectionString = $"DefaultEndpointsProtocol=http;AccountName={AccountName};AccountKey={AccountKey};BlobEndpoint=http://{host}:{blobPort}/{AccountName};";
        TableConnectionString = $"DefaultEndpointsProtocol=http;AccountName={AccountName};AccountKey={AccountKey};TableEndpoint=http://{host}:{tablePort}/{AccountName};";

        BlobServiceClient = new BlobServiceClient(BlobConnectionString);
        TableServiceClient = new TableServiceClient(TableConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _azuriteContainer.DisposeAsync();
    }
}

/// <summary>
/// Custom wait strategy for Azurite container.
/// Waits until the blob service is ready to accept connections.
/// </summary>
internal sealed class AzuriteReadyWaitStrategy : IWaitUntil
{
    private readonly int _port;

    public AzuriteReadyWaitStrategy(int port)
    {
        _port = port;
    }

    public async Task<bool> UntilAsync(IContainer container)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(_port)}")
        };

        try
        {
            // Try to get the blob service properties - this will succeed when Azurite is ready
            using var response = await client.GetAsync("/devstoreaccount1?comp=list").ConfigureAwait(false);
            // Azurite returns 403 for unauthorized requests, but that means it's running
            return response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                   response.IsSuccessStatusCode;
        }
        catch
        {
            // Not ready yet
        }

        return false;
    }
}

/// <summary>
/// Collection definition for sharing the Azurite fixture across test classes.
/// </summary>
[CollectionDefinition("AzuriteIntegration")]
public class AzuriteIntegrationCollection : ICollectionFixture<AzuriteContainerFixture>
{
}
