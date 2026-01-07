namespace ErikLieben.FA.ES.CosmosDb.Configuration;

/// <summary>
/// Configuration settings for the CosmosDB event store provider.
/// Designed for optimal RU efficiency with proper container and partition key strategies.
/// </summary>
public record EventStreamCosmosDbSettings
{
    private const string DefaultStoreType = "cosmosdb";

    /// <summary>
    /// The CosmosDB database name. Default: "eventstore".
    /// </summary>
    public string DatabaseName { get; init; } = "eventstore";

    /// <summary>
    /// Container name for storing document metadata (ObjectDocuments).
    /// Partition key: /objectName
    /// Default: "documents".
    /// </summary>
    public string DocumentsContainerName { get; init; } = "documents";

    /// <summary>
    /// Container name for storing event streams.
    /// Partition key: /streamId (keeps all events for a stream together for efficient reads).
    /// Default: "events".
    /// </summary>
    public string EventsContainerName { get; init; } = "events";

    /// <summary>
    /// Container name for storing aggregate snapshots.
    /// Partition key: /streamId (colocates snapshots with their events).
    /// Default: "snapshots".
    /// </summary>
    public string SnapshotsContainerName { get; init; } = "snapshots";

    /// <summary>
    /// Container name for storing document and stream tags.
    /// Partition key: /tagType (groups tags by type for efficient querying).
    /// Default: "tags".
    /// </summary>
    public string TagsContainerName { get; init; } = "tags";

    /// <summary>
    /// Container name for storing projections.
    /// Partition key: /projectionName (groups projection documents by type).
    /// Default: "projections".
    /// </summary>
    public string ProjectionsContainerName { get; init; } = "projections";

    /// <summary>
    /// The default data store type identifier. Default: "cosmosdb".
    /// </summary>
    public string DefaultDataStore { get; init; } = DefaultStoreType;

    /// <summary>
    /// The default document store type identifier. Default: "cosmosdb".
    /// </summary>
    public string DefaultDocumentStore { get; init; } = DefaultStoreType;

    /// <summary>
    /// The default snapshot store type identifier. Default: "cosmosdb".
    /// </summary>
    public string DefaultSnapShotStore { get; init; } = DefaultStoreType;

    /// <summary>
    /// The default document tag store type identifier. Default: "cosmosdb".
    /// </summary>
    public string DefaultDocumentTagStore { get; init; } = DefaultStoreType;

    /// <summary>
    /// The default stream tag type identifier. Default: "cosmosdb".
    /// </summary>
    public string DefaultEventStreamTagType { get; init; } = DefaultStoreType;

    /// <summary>
    /// Whether to automatically create the database and containers if they don't exist.
    /// Default: true.
    /// </summary>
    public bool AutoCreateContainers { get; init; } = true;

    /// <summary>
    /// Throughput configuration for the events container.
    /// Uses autoscale by default for cost-efficient RU management.
    /// Set to null to use shared database throughput.
    /// </summary>
    public ThroughputSettings? EventsThroughput { get; init; } = new() { AutoscaleMaxThroughput = 4000 };

    /// <summary>
    /// Throughput configuration for the documents container.
    /// Set to null to use shared database throughput.
    /// </summary>
    public ThroughputSettings? DocumentsThroughput { get; init; }

    /// <summary>
    /// Throughput configuration for the snapshots container.
    /// Set to null to use shared database throughput.
    /// </summary>
    public ThroughputSettings? SnapshotsThroughput { get; init; }

    /// <summary>
    /// Throughput configuration for the tags container.
    /// Set to null to use shared database throughput.
    /// </summary>
    public ThroughputSettings? TagsThroughput { get; init; }

    /// <summary>
    /// Throughput configuration for the projections container.
    /// Set to null to use shared database throughput.
    /// </summary>
    public ThroughputSettings? ProjectionsThroughput { get; init; }

    /// <summary>
    /// Shared database throughput. Used when container-level throughput is null.
    /// Default: autoscale with max 4000 RU/s.
    /// </summary>
    public ThroughputSettings? DatabaseThroughput { get; init; } = new() { AutoscaleMaxThroughput = 4000 };

    /// <summary>
    /// Time-to-live in seconds for event documents. Set to -1 for infinite (default).
    /// Useful for temporary event streams or event archival scenarios.
    /// </summary>
    public int DefaultTimeToLiveSeconds { get; init; } = -1;

    /// <summary>
    /// Whether to enable bulk execution mode for batch operations.
    /// Improves throughput for high-volume writes at the cost of latency.
    /// Default: false.
    /// </summary>
    public bool EnableBulkExecution { get; init; } = false;

    /// <summary>
    /// Maximum number of events to batch in a single transaction.
    /// CosmosDB supports up to 100 items per transactional batch.
    /// Default: 100.
    /// </summary>
    public int MaxBatchSize { get; init; } = 100;

    /// <summary>
    /// Whether to use optimistic concurrency with ETags for event appends.
    /// Prevents concurrent modifications to the same stream.
    /// Default: true.
    /// </summary>
    public bool UseOptimisticConcurrency { get; init; } = true;

    /// <summary>
    /// The page size for streaming reads via <see cref="CosmosDbDataStore.ReadAsStreamAsync"/>.
    /// Controls how many events are fetched per CosmosDB query page.
    /// Higher values reduce round-trips but increase memory per page.
    /// Default: 100.
    /// </summary>
    public int StreamingPageSize { get; init; } = 100;
}

/// <summary>
/// Throughput configuration for a CosmosDB container or database.
/// </summary>
public record ThroughputSettings
{
    /// <summary>
    /// Manual throughput in RU/s. Mutually exclusive with AutoscaleMaxThroughput.
    /// </summary>
    public int? ManualThroughput { get; init; }

    /// <summary>
    /// Autoscale maximum throughput in RU/s. The minimum will be 10% of this value.
    /// Recommended for variable workloads to optimize costs.
    /// </summary>
    public int? AutoscaleMaxThroughput { get; init; }
}
