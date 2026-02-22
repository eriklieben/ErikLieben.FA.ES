using ErikLieben.FA.ES;
using ErikLieben.FA.ES.AzureStorage;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskFlow.Domain;
using TaskFlow.Domain.Projections;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Configure Azure Blob Storage clients using Aspire-provided connection strings
builder.Services.AddAzureClients(clientBuilder =>
{
    // Get connection string from Aspire (events container)
    var eventsConnectionString = builder.Configuration.GetConnectionString("events");
    var storeConnectionString = BuildFullStorageConnectionString(eventsConnectionString);

    clientBuilder.AddBlobServiceClient(storeConnectionString)
        .WithName("Store");

    // Also register as BlobStorage for compatibility
    clientBuilder.AddBlobServiceClient(storeConnectionString)
        .WithName("BlobStorage");
});

// Configure Event Store services
// Note: ConfigureBlobEventStore must be called BEFORE ConfigureEventStore
// because ConfigureEventStore collects keyed services into dictionaries at registration time
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store", autoCreateContainer: false));
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// Azure Functions-specific bindings:
// - Input converters for [EventStreamInput] and [ProjectionInput]
// - Middleware for [ProjectionOutput<T>] to update projections after function execution
builder.ConfigureEventStoreBindings();

// Register Azure Blob Storage implementations
builder.Services.AddSingleton<IObjectDocumentFactory, BlobObjectDocumentFactory>();
builder.Services.AddSingleton<IEventStreamFactory, BlobEventStreamFactory>();
builder.Services.AddSingleton<IObjectIdProvider, BlobObjectIdProvider>();

// Configure TaskFlow Domain factories
builder.Services.ConfigureTaskFlowDomainFactory();

// Register Projection Factories for [ProjectionInput] binding
builder.Services.AddSingleton<ProjectKanbanBoardFactory>();
builder.Services.AddSingleton<IProjectionFactory<ProjectKanbanBoard>>(sp => sp.GetRequiredService<ProjectKanbanBoardFactory>());
builder.Services.AddSingleton<IProjectionFactory>(sp => sp.GetRequiredService<ProjectKanbanBoardFactory>());

builder.Services.AddSingleton<ActiveWorkItemsFactory>();
builder.Services.AddSingleton<IProjectionFactory<ActiveWorkItems>>(sp => sp.GetRequiredService<ActiveWorkItemsFactory>());
builder.Services.AddSingleton<IProjectionFactory>(sp => sp.GetRequiredService<ActiveWorkItemsFactory>());

builder.Services.AddSingleton<UserProfilesFactory>();
builder.Services.AddSingleton<IProjectionFactory<UserProfiles>>(sp => sp.GetRequiredService<UserProfilesFactory>());
builder.Services.AddSingleton<IProjectionFactory>(sp => sp.GetRequiredService<UserProfilesFactory>());

// Configure Application Insights
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

await builder.Build().RunAsync();

// Helper method to build full Azure Storage connection string
// Aspire provides container-specific connection strings, we need full storage account connection string
static string? BuildFullStorageConnectionString(string? containerConnectionString)
{
    if (string.IsNullOrEmpty(containerConnectionString))
        return containerConnectionString;

    string blobEndpoint = "http://127.0.0.1:10010/devstoreaccount1";
    string queueEndpoint = "http://127.0.0.1:10011/devstoreaccount1";
    string tableEndpoint = "http://127.0.0.1:10012/devstoreaccount1";

    var parts = containerConnectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
    foreach (var part in parts)
    {
        if (part.StartsWith("BlobEndpoint=", StringComparison.OrdinalIgnoreCase))
        {
            blobEndpoint = part.Substring("BlobEndpoint=".Length);

            var uri = new Uri(blobEndpoint);
            var baseUrl = $"{uri.Scheme}://{uri.Host}";
            var accountName = uri.AbsolutePath.TrimStart('/');

            queueEndpoint = $"{baseUrl}:{uri.Port + 1}/{accountName}";
            tableEndpoint = $"{baseUrl}:{uri.Port + 2}/{accountName}";
            break;
        }
    }

    return "DefaultEndpointsProtocol=http;" +
        "AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        $"BlobEndpoint={blobEndpoint};" +
        $"QueueEndpoint={queueEndpoint};" +
        $"TableEndpoint={tableEndpoint};";
}
