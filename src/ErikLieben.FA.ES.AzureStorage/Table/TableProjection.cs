using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Data.Tables;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Base class for projections that store data as multiple rows in Azure Table Storage.
/// Each event processed can create, update, or delete table entities.
/// </summary>
/// <remarks>
/// Unlike blob projections that store the entire projection state as a single JSON document,
/// table projections store each entity as a separate table row. This enables:
/// <list type="bullet">
///   <item><description>Query filtering by PartitionKey, RowKey, and properties</description></item>
///   <item><description>Sorting and pagination of results</description></item>
///   <item><description>Efficient updates to individual entities</description></item>
///   <item><description>Large projections that would exceed blob size limits</description></item>
/// </list>
///
/// Derived classes must be marked as partial and implement When methods.
/// The code generator will provide the Fold implementation.
/// </remarks>
public abstract class TableProjection : Projection
{
    // Use dictionary for O(1) lookup/update by entity key
    private readonly Dictionary<(string PartitionKey, string RowKey), TableOperation> _pendingOperationsMap = new();
    private Checkpoint _checkpoint = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TableProjection"/> class.
    /// </summary>
    protected TableProjection()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableProjection"/> class.
    /// </summary>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    protected TableProjection(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory)
        : base(documentFactory, eventStreamFactory)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableProjection"/> class with checkpoint state.
    /// </summary>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <param name="checkpoint">The initial checkpoint.</param>
    /// <param name="checkpointFingerprint">The checkpoint fingerprint.</param>
    protected TableProjection(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        Checkpoint checkpoint,
        string? checkpointFingerprint)
        : base(documentFactory, eventStreamFactory, checkpoint, checkpointFingerprint)
    {
        _checkpoint = checkpoint;
    }

    /// <summary>
    /// Queues an upsert operation for the specified entity.
    /// The entity will be inserted if it doesn't exist, or updated if it does.
    /// If an operation for the same entity already exists, it will be replaced (deduplication).
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to upsert.</param>
    protected void UpsertEntity<TEntity>(TEntity entity) where TEntity : ITableEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        var key = (entity.PartitionKey, entity.RowKey);
        _pendingOperationsMap[key] = TableOperation.Upsert(entity);
    }

    /// <summary>
    /// Queues a delete operation for the entity with the specified keys.
    /// If an upsert operation for the same entity exists, it will be replaced with delete.
    /// </summary>
    /// <param name="partitionKey">The partition key of the entity to delete.</param>
    /// <param name="rowKey">The row key of the entity to delete.</param>
    protected void DeleteEntity(string partitionKey, string rowKey)
    {
        ArgumentNullException.ThrowIfNull(partitionKey);
        ArgumentNullException.ThrowIfNull(rowKey);
        var key = (partitionKey, rowKey);
        _pendingOperationsMap[key] = TableOperation.Delete(partitionKey, rowKey);
    }

    /// <summary>
    /// Gets the pending operations that will be executed when the projection is saved.
    /// Operations are deduplicated by PartitionKey+RowKey, keeping only the latest operation for each entity.
    /// </summary>
    /// <returns>A read-only list of pending operations.</returns>
    internal IReadOnlyList<TableOperation> GetPendingOperations() => _pendingOperationsMap.Values.ToList();

    /// <summary>
    /// Clears all pending operations. Called after successful save.
    /// </summary>
    internal void ClearPendingOperations() => _pendingOperationsMap.Clear();

    /// <summary>
    /// Gets the count of pending operations (deduplicated by entity key).
    /// </summary>
    public int PendingOperationCount => _pendingOperationsMap.Count;

    /// <inheritdoc />
    [JsonPropertyName("$checkpoint")]
    public override Checkpoint Checkpoint
    {
        get => _checkpoint;
        set => _checkpoint = value;
    }

    /// <inheritdoc />
    protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories =>
        new();

    /// <inheritdoc />
    protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;

    /// <inheritdoc />
    public override string ToJson()
    {
        var data = new TableProjectionCheckpointData
        {
            Checkpoint = _checkpoint,
            CheckpointFingerprint = CheckpointFingerprint
        };
        return JsonSerializer.Serialize(data, TableProjectionJsonContext.Default.TableProjectionCheckpointData);
    }

    /// <inheritdoc />
    public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? context = null)
        where T : class
    {
        // Default implementation does nothing - derived classes should override via code generation
        return Task.CompletedTask;
    }
}

/// <summary>
/// Checkpoint data for table projections.
/// </summary>
internal class TableProjectionCheckpointData
{
    [JsonPropertyName("$checkpoint")]
    public Checkpoint Checkpoint { get; set; } = new();

    [JsonPropertyName("$checkpointFingerprint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CheckpointFingerprint { get; set; }
}

/// <summary>
/// JSON serialization context for table projection checkpoint data.
/// </summary>
[JsonSerializable(typeof(TableProjectionCheckpointData))]
[JsonSerializable(typeof(Checkpoint))]
internal partial class TableProjectionJsonContext : JsonSerializerContext
{
}
