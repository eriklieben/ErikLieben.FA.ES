using Azure.Data.Tables;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.AzureStorage;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.CosmosDb;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.Core;
using TaskFlow.Domain;
using TaskFlow.Domain.Projections;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Endpoints;
using TaskFlow.Api.Services;
using TaskFlow.Api.Middleware;
using Scalar.AspNetCore;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization to use string enums
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Debug: Log all available connection strings
var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");
logger.LogInformation("Available connection strings:");
foreach (var section in builder.Configuration.GetSection("ConnectionStrings").GetChildren())
{
    logger.LogInformation($"  - {section.Key}");
}

// Add Azure Blob Storage clients using AddAzureClients
// Register both storage accounts as Azure SDK clients for Aspire integration
// Aspire provides connection strings using the resource name defined in AppHost (e.g., "blob-events", "userprofile")
// We register them with the names our code expects ("Store", "UserDataStore")
builder.Services.AddAzureClients(clientBuilder =>
{
    // Main storage account (for events and projections)
    // Use "blob-events" resource connection string (defined in AppHost as storage.AddBlobContainer("blob-events", "events"))
    var eventsConnectionString = builder.Configuration.GetConnectionString("blob-events");
    logger.LogInformation($"blob-events connection string: {(string.IsNullOrEmpty(eventsConnectionString) ? "NULL/EMPTY" : "Found")}");

    if (string.IsNullOrEmpty(eventsConnectionString))
    {
        logger.LogError("CRITICAL: 'blob-events' connection string is not available. Blob storage will not work!");
        logger.LogError("Available connection strings: {ConnectionStrings}",
            string.Join(", ", builder.Configuration.GetSection("ConnectionStrings").GetChildren().Select(c => c.Key)));
    }

    var storeConnectionString = BuildFullStorageConnectionString(eventsConnectionString);
    logger.LogInformation($"Store connection string: {storeConnectionString?.Substring(0, Math.Min(50, storeConnectionString?.Length ?? 0))}...");

    if (!string.IsNullOrEmpty(storeConnectionString))
    {
        clientBuilder.AddBlobServiceClient(storeConnectionString)
            .WithName("Store");
    }
    else
    {
        logger.LogError("CRITICAL: Store connection string could not be built. Blob storage will not work!");
    }

    // User data storage account (for user profiles and object documents)
    // Use "userprofile" container connection string and build full storage connection string
    var userProfileConnectionString = builder.Configuration.GetConnectionString("userprofile");
    logger.LogInformation($"userprofile connection string: {(string.IsNullOrEmpty(userProfileConnectionString) ? "NULL/EMPTY" : "Found")}");
    var userDataConnectionString = BuildFullStorageConnectionString(userProfileConnectionString);
    logger.LogInformation($"UserDataStore connection string: {userDataConnectionString}");

    if (!string.IsNullOrEmpty(userDataConnectionString))
    {
        clientBuilder.AddBlobServiceClient(userDataConnectionString)
            .WithName("UserDataStore");
    }
    else
    {
        logger.LogError("CRITICAL: UserDataStore connection string could not be built. User profiles will not work!");
    }

    // Register BlobStorage client alias pointing to main storage
    if (!string.IsNullOrEmpty(storeConnectionString))
    {
        clientBuilder.AddBlobServiceClient(storeConnectionString)
            .WithName("BlobStorage");
    }

    // Register TableServiceClient for Table Storage (used by Epic aggregates)
    // Use the table connection string from Aspire's "tables" resource reference
    var tableConnectionString = builder.Configuration.GetConnectionString("tables");
    logger.LogInformation($"tables connection string: {(string.IsNullOrEmpty(tableConnectionString) ? "NULL/EMPTY" : "Found")}");

    // Determine which connection string to use for Table Storage
    string? tableStorageConnectionString = null;
    if (!string.IsNullOrEmpty(tableConnectionString))
    {
        tableStorageConnectionString = tableConnectionString;
        logger.LogInformation("Using Aspire 'tables' connection string for Table Storage");
    }
    else if (!string.IsNullOrEmpty(storeConnectionString))
    {
        // Fallback: Use the full storage connection string (already includes TableEndpoint)
        tableStorageConnectionString = storeConnectionString;
        logger.LogWarning("Table connection string not found from Aspire, using derived storage connection string");
    }

    if (!string.IsNullOrEmpty(tableStorageConnectionString))
    {
        logger.LogInformation($"Registering TableServiceClient with connection string: {tableStorageConnectionString?.Substring(0, Math.Min(100, tableStorageConnectionString?.Length ?? 0))}...");
        // Register with name "tables" to match Aspire resource name and table settings
        clientBuilder.AddTableServiceClient(tableStorageConnectionString)
            .WithName("tables");
    }
    else
    {
        logger.LogWarning("Table connection string not available in AddAzureClients, will rely on Aspire's AddAzureTableClient");
    }
});

// Add Aspire Azure Table Client integration for proper connection string handling
// This registers TableServiceClient with the connection string from Aspire's "tables" resource
// Only register if the connection string is available to avoid startup failures
var tablesConnectionStringCheck = builder.Configuration.GetConnectionString("tables");
if (!string.IsNullOrEmpty(tablesConnectionStringCheck))
{
    logger.LogInformation("Registering Aspire Azure Table Client with 'tables' connection string");
    builder.AddAzureTableClient("tables", configureSettings: settings =>
    {
        settings.DisableHealthChecks = false;
    });
}
else
{
    logger.LogWarning("Skipping Aspire Azure Table Client registration - 'tables' connection string not available");
}

// WORKAROUND: Also register a custom IAzureClientFactory<TableServiceClient> that creates clients using the Aspire-registered TableServiceClient
// This is needed because the ErikLieben.FA.ES library uses IAzureClientFactory.CreateClient(name)
builder.Services.AddSingleton<IAzureClientFactory<TableServiceClient>>(sp =>
{
    return new TableServiceClientFactory(sp);
});

// Configure TaskFlow domain (registers aggregates, factories, events)
builder.Services.ConfigureTaskFlowDomainFactory();

// Configure Azure Blob Storage as event store
// The defaultDataStore must match the registered BlobServiceClient name ("Store")
// Containers are initialized by the AppHost at startup
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store",
    autoCreateContainer: false));

// Configure Azure Table Storage for Epic aggregates
// This demonstrates using Table Storage as an alternative event store
// Documents use a shared table (objectdocumentstore) since ObjectName is stored in each document
// Note: defaultDataStore is "tables" to match the Aspire resource name registered with AddAzureTableClient
builder.Services.ConfigureTableEventStore(new EventStreamTableSettings(
    defaultDataStore: "tables",
    autoCreateTable: true,
    defaultEventTableName: "epic",
    defaultSnapshotTableName: "epicsnapshots",
    defaultDocumentTagTableName: "epicdocumenttags",
    defaultStreamTagTableName: "epicstreamtags"));

// Configure Azure CosmosDB for Sprint aggregates and SprintDashboard projection
// This demonstrates using CosmosDB as a storage provider for event streams and projections
// CosmosDB is optional - if not available, Sprint features will be disabled
var cosmosDbConnectionString = builder.Configuration.GetConnectionString("CosmosDb");
var cosmosDbEnabled = !string.IsNullOrEmpty(cosmosDbConnectionString);

if (cosmosDbEnabled)
{
    try
    {
        // Use Aspire's AddAzureCosmosClient for proper integration with the AppHost
        builder.AddAzureCosmosClient("CosmosDb", configureClientOptions: options =>
        {
            options.SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            };
        });

        // Configure CosmosDB event store with settings
        var cosmosSettings = new EventStreamCosmosDbSettings
        {
            DatabaseName = "eventstore",
            EventsContainerName = "events",
            DocumentsContainerName = "documents",
            TagsContainerName = "tags",
            ProjectionsContainerName = "projections",
            AutoCreateContainers = true,
            UseOptimisticConcurrency = true
        };
        builder.Services.ConfigureCosmosDbEventStore(cosmosSettings);

        // Override SprintFactory registration to use CosmosDB keyed services
        // The default registration uses blob storage; we need to use CosmosDB for Sprints
        builder.Services.AddSingleton<TaskFlow.Domain.Aggregates.ISprintFactory>(sp =>
        {
            var objectDocumentFactory = sp.GetRequiredKeyedService<ErikLieben.FA.ES.IObjectDocumentFactory>("cosmosdb");
            var eventStreamFactory = sp.GetRequiredKeyedService<ErikLieben.FA.ES.IEventStreamFactory>("cosmosdb");
            return new TaskFlow.Domain.Aggregates.SprintFactory(sp, eventStreamFactory, objectDocumentFactory);
        });

        // Register SprintDashboard projection factory (uses CosmosDB)
        builder.Services.AddSingleton<SprintDashboardFactory>(sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            var settings = sp.GetRequiredService<EventStreamCosmosDbSettings>();
            var objectDocumentFactory = sp.GetRequiredKeyedService<ErikLieben.FA.ES.IObjectDocumentFactory>("cosmosdb");
            var eventStreamFactory = sp.GetRequiredKeyedService<ErikLieben.FA.ES.IEventStreamFactory>("cosmosdb");
            return new SprintDashboardFactory(cosmosClient, settings, objectDocumentFactory, eventStreamFactory);
        });
        builder.Services.AddSingleton<ISprintDashboardFactory>(sp => sp.GetRequiredService<SprintDashboardFactory>());

        // Register SprintDashboard projection singleton (loaded from CosmosDB or created new)
        builder.Services.AddSingleton<SprintDashboard>(sp =>
        {
            var factory = sp.GetRequiredService<SprintDashboardFactory>();
            try
            {
                return factory.GetAsync().GetAwaiter().GetResult();
            }
            catch
            {
                var objectDocumentFactory = sp.GetRequiredKeyedService<ErikLieben.FA.ES.IObjectDocumentFactory>("cosmosdb");
                var eventStreamFactory = sp.GetRequiredKeyedService<ErikLieben.FA.ES.IEventStreamFactory>("cosmosdb");
                return new SprintDashboard(objectDocumentFactory, eventStreamFactory);
            }
        });

        // Register SprintDashboard projection handler (only when CosmosDB is available)
        builder.Services.AddSingleton<TaskFlow.Api.Projections.IProjectionHandler, TaskFlow.Api.Projections.SprintDashboardProjectionHandler>();

        Console.WriteLine("[STARTUP] CosmosDB configured successfully for Sprint aggregates");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[STARTUP] WARNING: CosmosDB configuration failed: {ex.Message}");
        Console.WriteLine("[STARTUP] Sprint features will be disabled");
        cosmosDbEnabled = false;
    }
}
else
{
    Console.WriteLine("[STARTUP] CosmosDB connection string not found - Sprint features disabled");
}

// Register storage provider status for UI feedback
builder.Services.AddSingleton(new StorageProviderStatus(
    BlobEnabled: true,  // Always enabled - required for core functionality
    TableEnabled: true, // Always enabled - required for epics
    CosmosDbEnabled: cosmosDbEnabled
));

// Override EpicFactory registration to use Table Storage keyed services
// The default registration uses blob storage; we need to use table storage for Epics
builder.Services.AddSingleton<TaskFlow.Domain.Aggregates.IEpicFactory>(sp =>
{
    var objectDocumentFactory = sp.GetRequiredKeyedService<ErikLieben.FA.ES.IObjectDocumentFactory>("table");
    var eventStreamFactory = sp.GetRequiredKeyedService<ErikLieben.FA.ES.IEventStreamFactory>("table");
    return new TaskFlow.Domain.Aggregates.EpicFactory(sp, eventStreamFactory, objectDocumentFactory);
});

// Override EpicSummaryFactory registration to use Table Storage keyed services for loading Epic documents
// The projection itself is stored in Blob, but it needs to load Epic documents from Table Storage
builder.Services.AddSingleton<EpicSummaryFactory>(sp =>
{
    var blobServiceClientFactory = sp.GetRequiredService<IAzureClientFactory<Azure.Storage.Blobs.BlobServiceClient>>();
    // Use table-keyed services since Epic documents are stored in Table Storage
    var objectDocumentFactory = sp.GetRequiredKeyedService<ErikLieben.FA.ES.IObjectDocumentFactory>("table");
    var eventStreamFactory = sp.GetRequiredKeyedService<ErikLieben.FA.ES.IEventStreamFactory>("table");
    return new EpicSummaryFactory(blobServiceClientFactory, objectDocumentFactory, eventStreamFactory);
});

// Register the mediator/event publisher
builder.Services.AddSingleton<TaskFlow.Domain.Messaging.IProjectionEventPublisher, TaskFlow.Api.Messaging.ProjectionEventDispatcher>();

// Register individual projection handlers (SOLID: each projection has its own handler)
builder.Services.AddSingleton<TaskFlow.Api.Projections.IProjectionHandler, TaskFlow.Api.Projections.UserProfilesProjectionHandler>();
builder.Services.AddSingleton<TaskFlow.Api.Projections.IProjectionHandler, TaskFlow.Api.Projections.ActiveWorkItemsProjectionHandler>();
builder.Services.AddSingleton<TaskFlow.Api.Projections.IProjectionHandler, TaskFlow.Api.Projections.ProjectDashboardProjectionHandler>();
builder.Services.AddSingleton<TaskFlow.Api.Projections.IProjectionHandler, TaskFlow.Api.Projections.EventUpcastingDemonstrationProjectionHandler>();
builder.Services.AddSingleton<TaskFlow.Api.Projections.IProjectionHandler, TaskFlow.Api.Projections.ProjectKanbanBoardHandler>();
builder.Services.AddSingleton<TaskFlow.Api.Projections.IProjectionHandler, TaskFlow.Api.Projections.EpicSummaryProjectionHandler>();
// Note: SprintDashboardProjectionHandler is registered in the CosmosDB block above (only when CosmosDB is available)

// Register projection event handlers
// You can register multiple handlers - they all get called in parallel

// 1. Direct handler - coordinates projection handlers
builder.Services.AddSingleton<TaskFlow.Api.Messaging.DirectProjectionUpdateHandler>();
builder.Services.AddSingleton<TaskFlow.Domain.Messaging.IProjectionEventHandler<TaskFlow.Domain.Messaging.ProjectionUpdateRequested>>(sp =>
    sp.GetRequiredService<TaskFlow.Api.Messaging.DirectProjectionUpdateHandler>());

// 2. Logging handler - logs all projection updates
builder.Services.AddSingleton<TaskFlow.Api.Messaging.LoggingProjectionEventHandler>();
builder.Services.AddSingleton<TaskFlow.Domain.Messaging.IProjectionEventHandler<TaskFlow.Domain.Messaging.ProjectionUpdateRequested>>(sp =>
    sp.GetRequiredService<TaskFlow.Api.Messaging.LoggingProjectionEventHandler>());

// Register wrapper factories that resolve keyed services
builder.Services.AddSingleton<IObjectDocumentFactory, ObjectDocumentFactory>();
builder.Services.AddSingleton<IDocumentTagDocumentFactory, DocumentTagDocumentFactory>();
builder.Services.AddSingleton<IEventStreamFactory, EventStreamFactory>();
builder.Services.AddSingleton<IObjectIdProvider, ObjectIdProvider>();

// Register migration service dependencies
builder.Services.AddSingleton<IDataStore>(sp =>
{
    var clientFactory = sp.GetRequiredService<IAzureClientFactory<Azure.Storage.Blobs.BlobServiceClient>>();
    var settings = sp.GetRequiredService<EventStreamBlobSettings>();
    return new BlobDataStore(clientFactory, settings.AutoCreateContainer);
});

builder.Services.AddSingleton<IDocumentStore>(sp =>
{
    var clientFactory = sp.GetRequiredService<IAzureClientFactory<Azure.Storage.Blobs.BlobServiceClient>>();
    var settings = sp.GetRequiredService<EventStreamBlobSettings>();
    var typeSettings = sp.GetRequiredService<EventStreamDefaultTypeSettings>();
    var documentTagStoreFactory = sp.GetRequiredService<IDocumentTagDocumentFactory>();
    return new BlobDocumentStore(clientFactory, documentTagStoreFactory, settings, typeSettings);
});

builder.Services.AddSingleton<IDistributedLockProvider, NoOpDistributedLockProvider>();
builder.Services.AddSingleton<IEventStreamMigrationService, EventStreamMigrationService>();

// Register dictionaries to map keys to keyed services
RegisterKeyedDictionary<string, IObjectDocumentFactory>(builder.Services);
RegisterKeyedDictionary<string, IDocumentTagDocumentFactory>(builder.Services);
RegisterKeyedDictionary<string, IEventStreamFactory>(builder.Services);
RegisterKeyedDictionary<string, IObjectIdProvider>(builder.Services);

static void RegisterKeyedDictionary<TKey, T>(IServiceCollection services) where TKey : notnull where T : notnull
{
    var keys = services
        .Where(sd => sd.IsKeyedService && sd.ServiceType == typeof(T) && sd.ServiceKey is TKey)
        .Select(sd => (TKey)sd.ServiceKey!)
        .Distinct()
        .ToList();

    services.AddTransient<IDictionary<TKey, T>>(p => keys
        .ToDictionary(k => k, k => p.GetRequiredKeyedService<T>(k)));
}

// Add SignalR for real-time updates
builder.Services.AddSignalR(options =>
{
    // Send keep-alive pings every 10 seconds to prevent timeouts
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    // Allow 30 seconds for client to respond before timing out
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Register current user service (demo mode - in production this would be from authentication)
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register projection parameter value factories
builder.Services.AddSingleton<TaskFlow.Domain.Projections.ObjectIdWhenValueFactory>();

// Projection factories and singletons are now auto-generated by dotnet faes generate
// in TaskFlow.DomainExtensions.Generated.cs via ConfigureTaskFlowDomainFactory()

// Register projection service with explicit dependency resolution
builder.Services.AddSingleton<IProjectionService>(sp =>
{
    var activeWorkItems = sp.GetRequiredService<ActiveWorkItems>();
    var projectDashboard = sp.GetRequiredService<ProjectDashboard>();
    var userProfiles = sp.GetRequiredService<UserProfiles>();
    var eventUpcastingDemo = sp.GetRequiredService<EventUpcastingDemonstration>();
    var projectKanbanBoard = sp.GetRequiredService<ProjectKanbanBoard>();
    var epicSummary = sp.GetRequiredService<EpicSummary>();
    var sprintDashboard = sp.GetService<SprintDashboard>(); // Optional - may be null if CosmosDB not configured
    var cosmosClient = sp.GetService<Microsoft.Azure.Cosmos.CosmosClient>(); // Optional
    var objectDocFactory = sp.GetService<IObjectDocumentFactory>(); // Optional
    var eventStreamFactory = sp.GetService<IEventStreamFactory>(); // Optional

    return new ProjectionService(
        activeWorkItems,
        projectDashboard,
        userProfiles,
        eventUpcastingDemo,
        projectKanbanBoard,
        epicSummary,
        sprintDashboard,
        cosmosClient,
        objectDocFactory,
        eventStreamFactory);
});

// Add CORS for Angular frontend (HTTPS only)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(
                  "https://localhost:4200",
                  "https://taskflow-frontend.dev.localhost")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
    });
});

// Add global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add OpenAPI/Swagger
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "TaskFlow API",
            Version = "v1",
            Description = "Event-sourced project and work item management API built with ErikLieben.FA.ES framework. " +
                         "Demonstrates CQRS and Event Sourcing patterns using Azure Blob Storage as the event store.",
            Contact = new()
            {
                Name = "TaskFlow API Support"
            }
        };
        return Task.CompletedTask;
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the static projection update action publisher
TaskFlow.Domain.Actions.PublishProjectionUpdateAction.Configure(
    app.Services.GetRequiredService<TaskFlow.Domain.Messaging.IProjectionEventPublisher>());

// Configure HTTP request pipeline
app.UseExceptionHandler(); // Enable global exception handling

app.MapDefaultEndpoints(); // Aspire endpoints

if (app.Environment.IsDevelopment())
{
    // Map OpenAPI document endpoint
    app.MapOpenApi();

    // Add Scalar API documentation at /scalar/v1
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("TaskFlow API Documentation")
               .WithTheme(ScalarTheme.Purple)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseCors("AllowAngular");

// Use current user middleware (for demo purposes)
app.UseCurrentUser();

// Map SignalR hub
app.MapHub<TaskFlowHub>("/hub/taskflow");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("Health");

// Map API endpoints
app.MapProjectEndpoints();
app.MapWorkItemEndpoints();
app.MapUserProfileEndpoints();
app.MapQueryEndpoints();
app.MapAdminEndpoints();
app.MapSnapshotEndpoints();
app.MapBackupRestoreEndpoints();
app.MapStreamMigrationDemoEndpoints();
app.MapEpicEndpoints();
app.MapSprintEndpoints();

await app.RunAsync();

// Helper method to build full Azure Storage connection string with blob, queue, and table endpoints
// Aspire provides container-specific connection strings, but we need the full storage account connection string
static string? BuildFullStorageConnectionString(string? containerConnectionString)
{
    if (string.IsNullOrEmpty(containerConnectionString))
        return containerConnectionString;

    // Parse connection string to extract blob endpoint
    string blobEndpoint = "http://127.0.0.1:10010/devstoreaccount1";
    string queueEndpoint = "http://127.0.0.1:10011/devstoreaccount1";
    string tableEndpoint = "http://127.0.0.1:10012/devstoreaccount1";

    var parts = containerConnectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
    foreach (var part in parts)
    {
        if (part.StartsWith("BlobEndpoint=", StringComparison.OrdinalIgnoreCase))
        {
            blobEndpoint = part.Substring("BlobEndpoint=".Length);

            // Extract base URL and construct queue/table endpoints
            var uri = new Uri(blobEndpoint);
            var baseUrl = $"{uri.Scheme}://{uri.Host}";
            var accountName = uri.AbsolutePath.TrimStart('/');

            queueEndpoint = $"{baseUrl}:{uri.Port + 1}/{accountName}";
            tableEndpoint = $"{baseUrl}:{uri.Port + 2}/{accountName}";
            break;
        }
    }

    // Build full connection string without ContainerName
    var fullConnectionString = "DefaultEndpointsProtocol=http;" +
        "AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        $"BlobEndpoint={blobEndpoint};" +
        $"QueueEndpoint={queueEndpoint};" +
        $"TableEndpoint={tableEndpoint};";

    return fullConnectionString;
}

// Custom factory that wraps the Aspire-registered TableServiceClient
// This is needed because the ErikLieben.FA.ES library uses IAzureClientFactory<TableServiceClient>.CreateClient(name)
file class TableServiceClientFactory : IAzureClientFactory<TableServiceClient>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TableServiceClientFactory> _logger;
    private readonly Dictionary<string, TableServiceClient> _clients = new();
    private readonly object _lock = new();

    public TableServiceClientFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _configuration = serviceProvider.GetRequiredService<IConfiguration>();
        _logger = serviceProvider.GetRequiredService<ILogger<TableServiceClientFactory>>();
    }

    public TableServiceClient CreateClient(string name)
    {
        lock (_lock)
        {
            if (_clients.TryGetValue(name, out var existing))
            {
                return existing;
            }

            // Try to get the connection string from configuration
            var connectionString = _configuration.GetConnectionString(name);
            _logger.LogInformation("TableServiceClientFactory: Creating client '{Name}' with connection string: {HasValue}",
                name, !string.IsNullOrEmpty(connectionString) ? "Found" : "NULL");

            if (string.IsNullOrEmpty(connectionString))
            {
                // Fallback: try to get the Aspire-registered singleton TableServiceClient
                var aspireSingleton = _serviceProvider.GetService<TableServiceClient>();
                if (aspireSingleton != null)
                {
                    _logger.LogInformation("TableServiceClientFactory: Using Aspire-registered TableServiceClient singleton for '{Name}'", name);
                    _clients[name] = aspireSingleton;
                    return aspireSingleton;
                }

                throw new InvalidOperationException(
                    $"No connection string found for TableServiceClient '{name}' and no Aspire TableServiceClient singleton available. " +
                    $"Ensure the 'tables' resource is properly configured in the AppHost and referenced by the API project.");
            }

            var client = new TableServiceClient(connectionString);
            _clients[name] = client;
            return client;
        }
    }
}

/// <summary>
/// Tracks which storage providers are available/configured
/// </summary>
public record StorageProviderStatus(
    bool BlobEnabled,
    bool TableEnabled,
    bool CosmosDbEnabled
);
