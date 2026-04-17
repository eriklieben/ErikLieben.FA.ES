using Azure.Storage.Blobs;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Integration;

/// <summary>
/// Shared fixture for Azurite (Azure Storage Emulator) container.
/// Provides blob storage for source event streams in migration tests.
/// </summary>
public class AzuriteContainerFixture : IAsyncLifetime
{
    private readonly IContainer _azuriteContainer;
    private const int BlobPort = 10000;

    public AzuriteContainerFixture()
    {
        // Azurite 3.35.0 still rejects the x-ms-version 2026-02-06 that
        // Azure.Storage.Blobs 12.27.0 sends, so pass --skipApiVersionCheck.
        // The full argv mirrors the image's default CMD; passing only the
        // skip flag leaves docker-entrypoint.sh without a target and the
        // container exits with code 9.
        _azuriteContainer = new ContainerBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
            .WithPortBinding(BlobPort, true)
            .WithCommand(
                "azurite",
                "-l", "/data",
                "--blobHost", "0.0.0.0",
                "--queueHost", "0.0.0.0",
                "--tableHost", "0.0.0.0",
                "--skipApiVersionCheck")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .AddCustomWaitStrategy(new AzuriteReadyWaitStrategy(BlobPort)))
            .Build();
    }

    public BlobServiceClient? BlobServiceClient { get; private set; }
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _azuriteContainer.StartAsync();

        var host = _azuriteContainer.Hostname;
        var port = _azuriteContainer.GetMappedPublicPort(BlobPort);

        // Azurite default account name and key
        const string accountName = "devstoreaccount1";
        const string accountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

        ConnectionString = $"DefaultEndpointsProtocol=http;AccountName={accountName};AccountKey={accountKey};BlobEndpoint=http://{host}:{port}/{accountName};";

        BlobServiceClient = new BlobServiceClient(ConnectionString);
    }

    public async ValueTask DisposeAsync()
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
