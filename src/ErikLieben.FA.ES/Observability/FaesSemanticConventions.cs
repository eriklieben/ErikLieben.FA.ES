namespace ErikLieben.FA.ES.Observability;

/// <summary>
/// Semantic conventions for OpenTelemetry attributes used in the ErikLieben.FA.ES library.
/// Following OpenTelemetry naming conventions with domain-specific "faes." prefix.
/// </summary>
/// <remarks>
/// These attribute names follow the OpenTelemetry semantic conventions pattern:
/// - Use lowercase with dots as separators
/// - Domain-specific attributes are prefixed with "faes."
/// - Standard OTel database conventions are used where applicable
/// </remarks>
public static class FaesSemanticConventions
{
    #region Domain-Specific Attributes (faes.*)

    /// <summary>
    /// The unique identifier of the event stream.
    /// Example: "order__order-123"
    /// </summary>
    public const string StreamId = "faes.stream.id";

    /// <summary>
    /// The name of the object type (aggregate or projection).
    /// Example: "order", "workitem", "project"
    /// </summary>
    public const string ObjectName = "faes.object.name";

    /// <summary>
    /// The unique identifier of the object instance.
    /// Example: "order-123", "workitem-456"
    /// </summary>
    public const string ObjectId = "faes.object.id";

    /// <summary>
    /// The number of events in an operation.
    /// Used for batch operations like commit or read.
    /// </summary>
    public const string EventCount = "faes.event.count";

    /// <summary>
    /// The CLR type name of an event.
    /// Example: "OrderCreated", "WorkItemAssigned"
    /// </summary>
    public const string EventType = "faes.event.type";

    /// <summary>
    /// The name of the event as stored (from EventName attribute).
    /// Example: "Order.Created", "WorkItem.Assigned"
    /// </summary>
    public const string EventName = "faes.event.name";

    /// <summary>
    /// The schema version of an event.
    /// Used for event versioning and upcasting.
    /// </summary>
    public const string EventVersion = "faes.event.version";

    /// <summary>
    /// The CLR type name of a projection.
    /// Example: "OrderDashboard", "ProjectKanbanBoard"
    /// </summary>
    public const string ProjectionType = "faes.projection.type";

    /// <summary>
    /// The current status of a projection.
    /// Values: "Active", "Rebuilding", "CatchingUp", "Ready", "Disabled", "Failed", "Archived"
    /// </summary>
    public const string ProjectionStatus = "faes.projection.status";

    /// <summary>
    /// The number of events folded during a projection update.
    /// </summary>
    public const string EventsFolded = "faes.events.folded";

    /// <summary>
    /// The storage provider type.
    /// Values: "blob", "table", "cosmosdb", "inmemory"
    /// </summary>
    public const string StorageProvider = "faes.storage.provider";

    /// <summary>
    /// The starting version for a read or update operation.
    /// </summary>
    public const string StartVersion = "faes.start.version";

    /// <summary>
    /// The target version for an update operation.
    /// </summary>
    public const string TargetVersion = "faes.target.version";

    /// <summary>
    /// The version of a snapshot.
    /// </summary>
    public const string SnapshotVersion = "faes.snapshot.version";

    /// <summary>
    /// The name of a snapshot.
    /// </summary>
    public const string SnapshotName = "faes.snapshot.name";

    /// <summary>
    /// The schema version of an event being registered.
    /// </summary>
    public const string SchemaVersion = "faes.schema.version";

    /// <summary>
    /// The CLR type name of an action.
    /// Example: "PostCommitProjectionHandler", "ValidationAction"
    /// </summary>
    public const string ActionType = "faes.action.type";

    /// <summary>
    /// The retry attempt number for resilient operations.
    /// Example: 1, 2, 3
    /// </summary>
    public const string RetryAttempt = "faes.retry.attempt";

    /// <summary>
    /// Duration of an operation in milliseconds.
    /// Example: 123.45
    /// </summary>
    public const string DurationMs = "faes.duration_ms";

    /// <summary>
    /// The source schema version when upcasting an event.
    /// </summary>
    public const string UpcastFromVersion = "faes.upcast.from_version";

    /// <summary>
    /// The target schema version when upcasting an event.
    /// </summary>
    public const string UpcastToVersion = "faes.upcast.to_version";

    /// <summary>
    /// Whether chunking is enabled for the operation.
    /// </summary>
    public const string ChunkingEnabled = "faes.chunking.enabled";

    /// <summary>
    /// The number of chunks involved in an operation.
    /// </summary>
    public const string ChunkCount = "faes.chunk.count";

    /// <summary>
    /// The concurrency constraint for a session.
    /// Values: "Loose", "Existing", "New"
    /// </summary>
    public const string SessionConstraint = "faes.session.constraint";

    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public const string Success = "faes.success";

    /// <summary>
    /// Whether a projection was loaded from cache.
    /// </summary>
    public const string LoadedFromCache = "faes.loaded_from_cache";

    /// <summary>
    /// The page size for paginated operations.
    /// </summary>
    public const string PageSize = "faes.page.size";

    /// <summary>
    /// Whether there is a continuation token for more results.
    /// </summary>
    public const string HasContinuation = "faes.has_continuation";

    /// <summary>
    /// The total estimated count for discovery operations.
    /// </summary>
    public const string TotalEstimate = "faes.total.estimate";

    #endregion

    #region Standard OTel Database Conventions

    /// <summary>
    /// The database system identifier (OTel standard).
    /// Values: "azure_blob", "azure_table", "cosmosdb"
    /// </summary>
    public const string DbSystem = "db.system";

    /// <summary>
    /// The database operation type (OTel standard).
    /// Values: "read", "write", "delete", "query"
    /// </summary>
    public const string DbOperation = "db.operation";

    /// <summary>
    /// The database/container name (OTel standard).
    /// </summary>
    public const string DbName = "db.name";

    #endregion

    #region Database System Values

    /// <summary>
    /// Database system value for Azure Blob Storage.
    /// </summary>
    public const string DbSystemAzureBlob = "azure_blob";

    /// <summary>
    /// Database system value for Azure Table Storage.
    /// </summary>
    public const string DbSystemAzureTable = "azure_table";

    /// <summary>
    /// Database system value for Azure Cosmos DB.
    /// </summary>
    public const string DbSystemCosmosDb = "cosmosdb";

    /// <summary>
    /// Database system value for Azure Append Blob Storage.
    /// </summary>
    public const string DbSystemAzureAppendBlob = "azure_appendblob";

    /// <summary>
    /// Database system value for in-memory storage (testing).
    /// </summary>
    public const string DbSystemInMemory = "inmemory";

    #endregion

    #region Database Operation Values

    /// <summary>
    /// Database operation value for read operations.
    /// </summary>
    public const string DbOperationRead = "read";

    /// <summary>
    /// Database operation value for write operations.
    /// </summary>
    public const string DbOperationWrite = "write";

    /// <summary>
    /// Database operation value for delete operations.
    /// </summary>
    public const string DbOperationDelete = "delete";

    /// <summary>
    /// Database operation value for query operations.
    /// </summary>
    public const string DbOperationQuery = "query";

    /// <summary>
    /// Database operation value for upsert operations.
    /// </summary>
    public const string DbOperationUpsert = "upsert";

    #endregion

    #region Storage Provider Values

    /// <summary>
    /// Storage provider value for Azure Blob Storage.
    /// </summary>
    public const string StorageProviderBlob = "blob";

    /// <summary>
    /// Storage provider value for Azure Table Storage.
    /// </summary>
    public const string StorageProviderTable = "table";

    /// <summary>
    /// Storage provider value for Azure Cosmos DB.
    /// </summary>
    public const string StorageProviderCosmosDb = "cosmosdb";

    /// <summary>
    /// Storage provider value for Azure Append Blob Storage.
    /// </summary>
    public const string StorageProviderAppendBlob = "appendblob";

    /// <summary>
    /// Storage provider value for in-memory storage (testing).
    /// </summary>
    public const string StorageProviderInMemory = "inmemory";

    #endregion
}
