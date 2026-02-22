using Azure.Storage.Blobs;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);

var persistStorage = builder.Configuration["PersistStorage"]?.ToLowerInvariant() == "true";
var storage = builder.AddAzureStorage("Store")
                     .RunAsEmulator(configureContainer =>
                     {
                         // Set custom ports to avoid conflicts with other services
                         configureContainer.WithEndpoint("blob", endpoint =>
                         {
                             endpoint.Port = 10010;
                             endpoint.TargetPort = 10000;
                         });
                         configureContainer.WithEndpoint("queue", endpoint =>
                         {
                             endpoint.Port = 10011;
                             endpoint.TargetPort = 10001;
                         });
                         configureContainer.WithEndpoint("table", endpoint =>
                         {
                             endpoint.Port = 10012;
                             endpoint.TargetPort = 10002;
                         });

                         // Enable data persistence if configured
                         if (persistStorage)
                         {
                             configureContainer.WithDataBindMount("azurite-data");
                         }
                     });


var userDataStorage = builder.AddAzureStorage("userdataStore")
    .RunAsEmulator(configureContainer =>
    {
        // Set custom ports to avoid conflicts with the first storage emulator
        configureContainer.WithEndpoint("blob", endpoint =>
        {
            endpoint.Port = 10020;
            endpoint.TargetPort = 10000;
        });
        configureContainer.WithEndpoint("queue", endpoint =>
        {
            endpoint.Port = 10021;
            endpoint.TargetPort = 10001;
        });
        configureContainer.WithEndpoint("table", endpoint =>
        {
            endpoint.Port = 10022;
            endpoint.TargetPort = 10002;
        });

        // Enable data persistence if configured
        if (persistStorage)
        {
            configureContainer.WithDataBindMount("azurite-userdata");
        }
    });

// CosmosDB emulator for demonstrating CosmosDB-backed event streams
// Configuration options:
//   - PersistStorage=true  : Data persists across restarts, container stays running
//   - PersistStorage=false : Fresh database each time (default)
//
// Requirements: Docker Desktop must be running
// Note: First startup can take 1-2 minutes while the emulator initializes

var enableCosmosDb = builder.Configuration["EnableCosmosDb"]?.ToLowerInvariant() != "false";

IResourceBuilder<AzureCosmosDBResource>? cosmosDb = null;

if (enableCosmosDb)
{
#pragma warning disable ASPIRECOSMOSDB001 // WithDataExplorer is experimental
    cosmosDb = builder.AddAzureCosmosDB("CosmosDb")
        .RunAsPreviewEmulator(configureContainer =>
        {
            // Note: WithPartitionCount is not supported with preview emulator
            // The preview emulator uses a fixed partition configuration

            // Fix: Override IP address to prevent emulator from using internal container IP
            // Without this, the client connects to container's internal IP (172.x.x.x) which is unreachable
            // See: https://github.com/dotnet/aspire/issues/6349
            configureContainer.WithEnvironment("AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE", "127.0.0.1");

            // Use fixed port 8081 to avoid proxy issues
            configureContainer.WithGatewayPort(8081);

            // Enable the Data Explorer UI for easy debugging
            // Access at: https://localhost:8081/_explorer/index.html
            configureContainer.WithDataExplorer();

            if (persistStorage)
            {
                // Persist data across container restarts
                configureContainer.WithDataVolume("cosmosdb-data");

                // Keep container running between app restarts (avoids slow cold start)
                configureContainer.WithLifetime(ContainerLifetime.Persistent);
            }
            // When persistStorage is false, container starts fresh each time
            // (no WithDataVolume = ephemeral data)
        });
#pragma warning restore ASPIRECOSMOSDB001

    // Note: We don't use AddCosmosDatabase/AddContainer here because Aspire
    // tries to provision them before the emulator is fully ready.
    // Instead, the API creates containers lazily with AutoCreateContainers = true
}

// MinIO S3-compatible storage for demonstrating S3 storage provider
var minio = builder.AddContainer("minio", "minio/minio")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEndpoint(port: 9000, targetPort: 9000, name: "s3", scheme: "http")
    .WithEndpoint(port: 9001, targetPort: 9001, name: "console", scheme: "http")
    .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin");

if (persistStorage)
{
    minio = minio.WithBindMount("minio-data", "/data");
}

// Use AddBlobContainer to provision specific containers
var eventsContainer = storage.AddBlobContainer("blob-events", "events");
var projectContainer = storage.AddBlobContainer("project");
var workItemContainer = storage.AddBlobContainer("workitem");
var storeObjectDocuments = storage.AddBlobContainer("store-object-document-store", "object-document-store");
var projectionsContainer = storage.AddBlobContainer("blob-projections", "projections");

// Add Table Storage for Epic aggregates
var tableStorage = storage.AddTables("tables");

var userStoreObjectDocuments = userDataStorage.AddBlobContainer("userstore-object-document-store", "object-document-store");
var userProfiles = userDataStorage.AddBlobContainer("userprofile");

var api = builder.AddProject<Projects.TaskFlow_Api>("api")
                 .WithReference(eventsContainer)
                 .WithReference(storeObjectDocuments)
                 .WithReference(userStoreObjectDocuments)
                 .WithReference(projectContainer)
                 .WithReference(workItemContainer)
                 .WithReference(projectionsContainer)
                 .WithReference(userProfiles)
                 .WithReference(tableStorage)
                 // Wait for storage resources to be ready before starting API
                 .WaitFor(storage)
                 .WaitFor(userDataStorage)
                 .WaitFor(minio)
                 .WithExternalHttpEndpoints();

// Add CosmosDB reference if enabled
if (enableCosmosDb && cosmosDb != null)
{
    api.WithReference(cosmosDb)
       .WaitFor(cosmosDb);
}

// Azure Functions project for demonstrating EventStream and Projection input bindings
var functions = builder.AddAzureFunctionsProject<Projects.TaskFlow_Functions>("functions")
       .WithReference(eventsContainer)
       .WithReference(storeObjectDocuments)
       .WithReference(projectContainer)
       .WithReference(workItemContainer)
       .WithReference(projectionsContainer)
       .WithExternalHttpEndpoints();

var frontend = builder.AddJavaScriptApp("frontend", "../../taskflow-web")
                      .WithHttpsEndpoint(env: "PORT")
                      .WithExternalHttpEndpoints()
                      .WithReference(api);

// Pass Functions endpoint URL to frontend for proxy configuration
frontend.WithEnvironment(ctx =>
{
    var functionsEndpoint = functions.GetEndpoint("http");
    ctx.EnvironmentVariables["services__functions__http__0"] = functionsEndpoint;
});

var app = builder.Build();

// _ = Task.Run(async () =>
// {
//     await Task.Delay(TimeSpan.FromSeconds(5)); // Wait for Azurite to start
//     await InitializeBlobContainersAsync(app.Services);
// });

await app.RunAsync();

// Helper method to initialize blob containers in Azurite with retry logic
// static async Task InitializeBlobContainersAsync(IServiceProvider services)
// {
//     var logger = services.GetRequiredService<ILogger<Program>>();
//     var configuration = services.GetRequiredService<IConfiguration>();
//
//     // Retry logic to wait for Azurite to be ready
//     const int maxRetries = 10;
//     const int delayMs = 2000;
//
//     for (int attempt = 1; attempt <= maxRetries; attempt++)
//     {
//         try
//         {
//             logger.LogInformation("Initializing blob containers (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
//
//             // Initialize containers in main Store account
//             var storeConnectionString = configuration.GetConnectionString("events");
//             if (!string.IsNullOrEmpty(storeConnectionString))
//             {
//                 var storeClient = new BlobServiceClient(storeConnectionString);
//                 var storeContainers = new[] { "events", "project", "workitem", "projections", "object-document-store" };
//
//                 foreach (var containerName in storeContainers)
//                 {
//                     var container = storeClient.GetBlobContainerClient(containerName);
//                     await container.CreateIfNotExistsAsync();
//                     logger.LogInformation("Initialized blob container '{ContainerName}' in Store account", containerName);
//                 }
//             }
//
//             // Initialize containers in UserDataStore account
//             var userDataConnectionString = configuration.GetConnectionString("userdata-object-documents");
//             if (!string.IsNullOrEmpty(userDataConnectionString))
//             {
//                 var userDataClient = new BlobServiceClient(userDataConnectionString);
//                 var userDataContainers = new[] { "userprofiles", "object-document-store" };
//
//                 foreach (var containerName in userDataContainers)
//                 {
//                     var container = userDataClient.GetBlobContainerClient(containerName);
//                     await container.CreateIfNotExistsAsync();
//                     logger.LogInformation("Initialized blob container '{ContainerName}' in UserDataStore account", containerName);
//                 }
//             }
//
//             logger.LogInformation("Successfully initialized all blob containers");
//             return; // Success
//         }
//         catch (Exception ex)
//         {
//             if (attempt == maxRetries)
//             {
//                 logger.LogError(ex, "Failed to initialize blob containers after {MaxRetries} attempts. You may need to manually create the containers or enable autoCreateContainer in the API.", maxRetries);
//                 return; // Don't crash the AppHost
//             }
//
//             logger.LogWarning(ex, "Failed to initialize blob containers (attempt {Attempt}/{MaxRetries}), retrying in {DelayMs}ms", attempt, maxRetries, delayMs);
//             await Task.Delay(delayMs);
//         }
//     }
// }
