using Azure.Storage.Blobs;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.CosmosDb;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Events;
using ErikLieben.FA.ES.EventStreamManagement.LiveMigration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Integration;

/// <summary>
/// Integration tests for live migration from Azure Blob Storage to CosmosDB.
/// These tests use real TestContainers for both Azurite (blob storage) and CosmosDB emulator.
/// </summary>
[Collection("BlobToCosmosDbMigration")]
[Trait("Category", "Integration")]
[Trait("Feature", "LiveMigration")]
public class BlobToCosmosDbLiveMigrationIntegrationTests : IAsyncLifetime
{
    private readonly BlobToCosmosDbMigrationFixture _fixture;
    private readonly string _testId;
    private readonly EventStreamCosmosDbSettings _cosmosSettings;
    private Database? _database;
    private BlobDataStore? _blobDataStore;
    private CosmosDbDataStore? _cosmosDataStore;
    private IDocumentStore? _documentStore;

    public BlobToCosmosDbLiveMigrationIntegrationTests(BlobToCosmosDbMigrationFixture fixture)
    {
        _fixture = fixture;
        _testId = Guid.NewGuid().ToString("N")[..8];
        _cosmosSettings = new EventStreamCosmosDbSettings
        {
            DatabaseName = $"migrationdb_{_testId}",
            EventsContainerName = "events",
            DocumentsContainerName = "documents",
            AutoCreateContainers = true
        };
    }

    public async Task InitializeAsync()
    {
        // Create CosmosDB database for this test
        _database = (await _fixture.CosmosDb.CosmosClient!.CreateDatabaseIfNotExistsAsync(_cosmosSettings.DatabaseName)).Database;

        // Create blob container for source events
        var containerName = $"testobject";
        var blobContainerClient = _fixture.Azurite.BlobServiceClient!.GetBlobContainerClient(containerName);
        await blobContainerClient.CreateIfNotExistsAsync();

        // Create blob data store using a factory that returns the test client
        var blobClientFactory = CreateBlobClientFactory(_fixture.Azurite.BlobServiceClient);
        _blobDataStore = new BlobDataStore(blobClientFactory, autoCreateContainer: true);

        // Create CosmosDB data store
        _cosmosDataStore = new CosmosDbDataStore(_fixture.CosmosDb.CosmosClient!, _cosmosSettings);

        // Create a mock document store for the migration
        _documentStore = Substitute.For<IDocumentStore>();
        _documentStore.SetAsync(Arg.Any<IObjectDocument>()).Returns(Task.CompletedTask);
    }

    public async Task DisposeAsync()
    {
        // Clean up CosmosDB database
        if (_database != null)
        {
            try
            {
                await _database.DeleteAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task Should_migrate_events_from_blob_storage_to_cosmosdb()
    {
        // Arrange - Create source events in Blob Storage
        var sourceStreamId = $"orders-{_testId}";
        var targetStreamId = $"orders-{_testId}-v2";

        var sourceDocument = CreateObjectDocument(sourceStreamId, "blob", "default");
        var targetDocument = CreateObjectDocument(targetStreamId, "cosmosdb", "cosmosdb");

        // Write test events to blob storage
        await _blobDataStore!.AppendAsync(sourceDocument, new JsonEvent
        {
            EventType = "OrderCreated",
            EventVersion = 0,
            Payload = """{"orderId":"ORD-001","customer":"Alice"}"""
        });
        await _blobDataStore.AppendAsync(sourceDocument, new JsonEvent
        {
            EventType = "OrderItemAdded",
            EventVersion = 1,
            Payload = """{"orderId":"ORD-001","product":"Widget","quantity":5}"""
        });
        await _blobDataStore.AppendAsync(sourceDocument, new JsonEvent
        {
            EventType = "OrderShipped",
            EventVersion = 2,
            Payload = """{"orderId":"ORD-001","trackingNumber":"TRK-123"}"""
        });

        // Verify events exist in blob storage
        var sourceEvents = await _blobDataStore.ReadAsync(sourceDocument);
        Assert.NotNull(sourceEvents);
        Assert.Equal(3, sourceEvents.Count());

        // Create migration context - use blob for source reads, cosmos for target writes
        var context = new LiveMigrationContext
        {
            MigrationId = Guid.NewGuid(),
            SourceDocument = sourceDocument,
            SourceStreamId = sourceStreamId,
            TargetDocument = targetDocument,
            TargetStreamId = targetStreamId,
            DataStore = new MigrationDataStoreAdapter(_blobDataStore, _cosmosDataStore!, sourceDocument, targetDocument),
            DocumentStore = _documentStore!,
            Options = new LiveMigrationOptions()
        };

        var loggerFactory = CreateLoggerFactory();
        var executor = new LiveMigrationExecutor(context, loggerFactory);

        // Act
        var result = await executor.ExecuteAsync();

        // Assert
        Assert.True(result.Success, $"Migration failed: {result.Error}");
        Assert.Equal(3, result.TotalEventsCopied);
        Assert.Equal(sourceStreamId, result.SourceStreamId);
        Assert.Equal(targetStreamId, result.TargetStreamId);

        // Verify events exist in CosmosDB target
        var targetEvents = (await _cosmosDataStore!.ReadAsync(targetDocument))?.ToList();
        Assert.NotNull(targetEvents);
        Assert.Equal(3, targetEvents.Count);
        Assert.Equal("OrderCreated", targetEvents[0].EventType);
        Assert.Equal("OrderItemAdded", targetEvents[1].EventType);
        Assert.Equal("OrderShipped", targetEvents[2].EventType);

        // Verify versions are preserved
        Assert.Equal(0, targetEvents[0].EventVersion);
        Assert.Equal(1, targetEvents[1].EventVersion);
        Assert.Equal(2, targetEvents[2].EventVersion);
    }

    [Fact]
    public async Task Should_not_copy_close_event_to_target()
    {
        // Arrange - Write business events to source, verify migration copies them but doesn't
        // copy the close event that the executor writes to the source at the end.
        // Note: This tests that the target doesn't get the close event that the executor
        // creates for the source stream during the close phase.
        var sourceStreamId = $"customers-{_testId}";
        var targetStreamId = $"customers-{_testId}-v2";

        var sourceDocument = CreateObjectDocument(sourceStreamId, "blob", "default");
        var targetDocument = CreateObjectDocument(targetStreamId, "cosmosdb", "cosmosdb");

        // Write business events to blob storage
        await _blobDataStore!.AppendAsync(sourceDocument, new JsonEvent
        {
            EventType = "CustomerRegistered",
            EventVersion = 0,
            Payload = """{"customerId":"CUST-001","name":"Bob"}"""
        });
        await _blobDataStore.AppendAsync(sourceDocument, new JsonEvent
        {
            EventType = "CustomerVerified",
            EventVersion = 1,
            Payload = """{"customerId":"CUST-001","verifiedAt":"2024-01-15"}"""
        });

        // Create migration context
        var context = new LiveMigrationContext
        {
            MigrationId = Guid.NewGuid(),
            SourceDocument = sourceDocument,
            SourceStreamId = sourceStreamId,
            TargetDocument = targetDocument,
            TargetStreamId = targetStreamId,
            DataStore = new MigrationDataStoreAdapter(_blobDataStore, _cosmosDataStore!, sourceDocument, targetDocument),
            DocumentStore = _documentStore!,
            Options = new LiveMigrationOptions()
        };

        var loggerFactory = CreateLoggerFactory();
        var executor = new LiveMigrationExecutor(context, loggerFactory);

        // Act
        var result = await executor.ExecuteAsync();

        // Assert
        Assert.True(result.Success, $"Migration failed: {result.Error}");
        Assert.Equal(2, result.TotalEventsCopied);

        // Verify target has only the business events (no close event from the migration process)
        var targetEvents = (await _cosmosDataStore!.ReadAsync(targetDocument))?.ToList();
        Assert.NotNull(targetEvents);
        Assert.Equal(2, targetEvents.Count);
        Assert.DoesNotContain(targetEvents, e => e.EventType == StreamClosedEvent.EventTypeName);

        // Verify source stream was properly closed (has the close event)
        var sourceEventsAfterMigration = (await _blobDataStore.ReadAsync(sourceDocument))?.ToList();
        Assert.NotNull(sourceEventsAfterMigration);
        Assert.Equal(3, sourceEventsAfterMigration.Count); // 2 business events + close event
        Assert.Contains(sourceEventsAfterMigration, e => e.EventType == StreamClosedEvent.EventTypeName);
    }

    [Fact]
    public async Task Should_preserve_event_order_during_migration()
    {
        // Arrange - Create events with specific ordering
        var sourceStreamId = $"inventory-{_testId}";
        var targetStreamId = $"inventory-{_testId}-v2";

        var sourceDocument = CreateObjectDocument(sourceStreamId, "blob", "default");
        var targetDocument = CreateObjectDocument(targetStreamId, "cosmosdb", "cosmosdb");

        // Write events in specific order
        var eventTypes = new[] { "StockReceived", "StockReserved", "StockShipped", "StockReturned", "StockAdjusted" };
        for (int i = 0; i < eventTypes.Length; i++)
        {
            await _blobDataStore!.AppendAsync(sourceDocument, new JsonEvent
            {
                EventType = eventTypes[i],
                EventVersion = i,
                Payload = $$$"""{"sequence":{{{i}}},"type":"{{{eventTypes[i]}}}"}"""
            });
        }

        // Create migration context
        var context = new LiveMigrationContext
        {
            MigrationId = Guid.NewGuid(),
            SourceDocument = sourceDocument,
            SourceStreamId = sourceStreamId,
            TargetDocument = targetDocument,
            TargetStreamId = targetStreamId,
            DataStore = new MigrationDataStoreAdapter(_blobDataStore!, _cosmosDataStore!, sourceDocument, targetDocument),
            DocumentStore = _documentStore!,
            Options = new LiveMigrationOptions()
        };

        var loggerFactory = CreateLoggerFactory();
        var executor = new LiveMigrationExecutor(context, loggerFactory);

        // Act
        var result = await executor.ExecuteAsync();

        // Assert
        Assert.True(result.Success, $"Migration failed: {result.Error}");
        Assert.Equal(5, result.TotalEventsCopied);

        // Verify events are in correct order
        var targetEvents = (await _cosmosDataStore!.ReadAsync(targetDocument))?.ToList();
        Assert.NotNull(targetEvents);
        for (int i = 0; i < eventTypes.Length; i++)
        {
            Assert.Equal(eventTypes[i], targetEvents[i].EventType);
            Assert.Equal(i, targetEvents[i].EventVersion);
        }
    }

    [Fact]
    public async Task Should_handle_empty_source_stream()
    {
        // Arrange - Create empty source stream (no events)
        var sourceStreamId = $"empty-{_testId}";
        var targetStreamId = $"empty-{_testId}-v2";

        var sourceDocument = CreateObjectDocument(sourceStreamId, "blob", "default");
        var targetDocument = CreateObjectDocument(targetStreamId, "cosmosdb", "cosmosdb");

        // Don't write any events to source

        // Create migration context
        var context = new LiveMigrationContext
        {
            MigrationId = Guid.NewGuid(),
            SourceDocument = sourceDocument,
            SourceStreamId = sourceStreamId,
            TargetDocument = targetDocument,
            TargetStreamId = targetStreamId,
            DataStore = new MigrationDataStoreAdapter(_blobDataStore!, _cosmosDataStore!, sourceDocument, targetDocument),
            DocumentStore = _documentStore!,
            Options = new LiveMigrationOptions()
        };

        var loggerFactory = CreateLoggerFactory();
        var executor = new LiveMigrationExecutor(context, loggerFactory);

        // Act
        var result = await executor.ExecuteAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.TotalEventsCopied);
        Assert.Equal(1, result.Iterations);
    }

    [Fact]
    public async Task Should_report_progress_during_migration()
    {
        // Arrange
        var sourceStreamId = $"progress-{_testId}";
        var targetStreamId = $"progress-{_testId}-v2";

        var sourceDocument = CreateObjectDocument(sourceStreamId, "blob", "default");
        var targetDocument = CreateObjectDocument(targetStreamId, "cosmosdb", "cosmosdb");

        // Write events to blob storage
        for (int i = 0; i < 5; i++)
        {
            await _blobDataStore!.AppendAsync(sourceDocument, new JsonEvent
            {
                EventType = $"Event{i}",
                EventVersion = i,
                Payload = $$$"""{"index":{{{i}}}}"""
            });
        }

        var progressReports = new List<LiveMigrationProgress>();

        var options = new LiveMigrationOptions();
        options.OnCatchUpProgress(p => progressReports.Add(p));

        var context = new LiveMigrationContext
        {
            MigrationId = Guid.NewGuid(),
            SourceDocument = sourceDocument,
            SourceStreamId = sourceStreamId,
            TargetDocument = targetDocument,
            TargetStreamId = targetStreamId,
            DataStore = new MigrationDataStoreAdapter(_blobDataStore!, _cosmosDataStore!, sourceDocument, targetDocument),
            DocumentStore = _documentStore!,
            Options = options
        };

        var loggerFactory = CreateLoggerFactory();
        var executor = new LiveMigrationExecutor(context, loggerFactory);

        // Act
        var result = await executor.ExecuteAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(progressReports);
        Assert.All(progressReports, p => Assert.True(p.Iteration > 0));
    }

    private static IObjectDocument CreateObjectDocument(string streamId, string streamType, string dataStore)
    {
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = streamId,
            StreamType = streamType,
            CurrentStreamVersion = -1,
            DataStore = dataStore,
            DocumentStore = dataStore
        };

        var objectDocument = Substitute.For<IObjectDocument>();
        objectDocument.ObjectName.Returns("TestObject");
        objectDocument.ObjectId.Returns(streamId);
        objectDocument.Active.Returns(streamInfo);
        objectDocument.TerminatedStreams.Returns(new List<TerminatedStream>());

        return objectDocument;
    }

    private static IAzureClientFactory<BlobServiceClient> CreateBlobClientFactory(BlobServiceClient client)
    {
        var factory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
        factory.CreateClient(Arg.Any<string>()).Returns(client);
        return factory;
    }

    private static ILoggerFactory CreateLoggerFactory()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<LiveMigrationExecutor>()
            .Returns(Substitute.For<ILogger<LiveMigrationExecutor>>());
        return loggerFactory;
    }
}

/// <summary>
/// Adapter that routes read/write operations to appropriate data stores based on document.
/// Reads from source (Blob) and writes to target (CosmosDB) during migration.
/// </summary>
internal class MigrationDataStoreAdapter : IDataStore
{
    private readonly IDataStore _sourceDataStore;
    private readonly IDataStore _targetDataStore;
    private readonly IObjectDocument _sourceDocument;
    private readonly IObjectDocument _targetDocument;

    public MigrationDataStoreAdapter(
        IDataStore sourceDataStore,
        IDataStore targetDataStore,
        IObjectDocument sourceDocument,
        IObjectDocument targetDocument)
    {
        _sourceDataStore = sourceDataStore;
        _targetDataStore = targetDataStore;
        _sourceDocument = sourceDocument;
        _targetDocument = targetDocument;
    }

    public Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null, int? chunk = null)
    {
        // Route reads based on which document is being read
        if (document.Active.StreamIdentifier == _sourceDocument.Active.StreamIdentifier)
        {
            return _sourceDataStore.ReadAsync(document, startVersion, untilVersion, chunk);
        }
        return _targetDataStore.ReadAsync(document, startVersion, untilVersion, chunk);
    }

    public Task AppendAsync(IObjectDocument document, params IEvent[] events)
        => AppendAsync(document, preserveTimestamp: false, events);

    public async Task AppendAsync(IObjectDocument document, bool preserveTimestamp, params IEvent[] events)
    {
        // Route appends based on which document is being written
        if (document.Active.StreamIdentifier == _sourceDocument.Active.StreamIdentifier)
        {
            await _sourceDataStore.AppendAsync(document, preserveTimestamp, events);
            return;
        }

        // For CosmosDB target: append events one at a time because
        // the vnext-preview emulator doesn't support transactional batches
        foreach (var evt in events)
        {
            await _targetDataStore.AppendAsync(document, preserveTimestamp, evt);
        }
    }
}
