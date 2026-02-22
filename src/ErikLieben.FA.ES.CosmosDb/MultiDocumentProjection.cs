using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Base class for projections that store data as multiple documents in Azure CosmosDB.
/// Each event processed can append new documents, enabling audit logs and event-based projections.
/// </summary>
/// <remarks>
/// Unlike single-document projections that store the entire projection state as one document,
/// multi-document projections store each entry as a separate CosmosDB document. This enables:
/// <list type="bullet">
///   <item><description>Append-only audit logs of all changes</description></item>
///   <item><description>Query filtering by partition key and properties</description></item>
///   <item><description>Efficient time-range queries on events</description></item>
///   <item><description>Large projections that would exceed single document size limits</description></item>
/// </list>
/// </remarks>
public abstract class MultiDocumentProjection : Projection
{
    private readonly List<object> _pendingDocuments = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiDocumentProjection"/> class.
    /// </summary>
    protected MultiDocumentProjection()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiDocumentProjection"/> class.
    /// </summary>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    protected MultiDocumentProjection(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory)
        : base(documentFactory, eventStreamFactory)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiDocumentProjection"/> class with checkpoint state.
    /// </summary>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <param name="checkpoint">The initial checkpoint.</param>
    /// <param name="checkpointFingerprint">The checkpoint fingerprint.</param>
    protected MultiDocumentProjection(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        Checkpoint checkpoint,
        string? checkpointFingerprint)
        : base(documentFactory, eventStreamFactory, checkpoint, checkpointFingerprint)
    {
        Checkpoint = checkpoint;
    }

    /// <summary>
    /// Appends a document to be saved when the projection is persisted.
    /// </summary>
    /// <typeparam name="TDocument">The document type.</typeparam>
    /// <param name="document">The document to append.</param>
    protected void AppendDocument<TDocument>(TDocument document) where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(document);
        _pendingDocuments.Add(document);
    }

    /// <summary>
    /// Gets the pending documents that will be saved when the projection is persisted.
    /// </summary>
    /// <returns>A read-only list of pending documents.</returns>
    internal IReadOnlyList<object> GetPendingDocuments() => _pendingDocuments;

    /// <summary>
    /// Clears all pending documents. Called after successful save.
    /// </summary>
    internal void ClearPendingDocuments() => _pendingDocuments.Clear();

    /// <summary>
    /// Gets the count of pending documents.
    /// </summary>
    public int PendingDocumentCount => _pendingDocuments.Count;

    /// <inheritdoc />
    [JsonPropertyName("$checkpoint")]
    public override Checkpoint Checkpoint { get; set; } = new();

    /// <inheritdoc />
    protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories =>
        new();

    /// <inheritdoc />
    protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;

    /// <inheritdoc />
    public override string ToJson()
    {
        var data = new MultiDocumentProjectionCheckpointData
        {
            Checkpoint = Checkpoint,
            CheckpointFingerprint = CheckpointFingerprint
        };
        return JsonSerializer.Serialize(data, MultiDocumentProjectionJsonContext.Default.MultiDocumentProjectionCheckpointData);
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
/// Checkpoint data for multi-document projections.
/// </summary>
internal class MultiDocumentProjectionCheckpointData
{
    [JsonPropertyName("$checkpoint")]
    public Checkpoint Checkpoint { get; set; } = new();

    [JsonPropertyName("$checkpointFingerprint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CheckpointFingerprint { get; set; }
}

/// <summary>
/// JSON serialization context for multi-document projection checkpoint data.
/// </summary>
[JsonSerializable(typeof(MultiDocumentProjectionCheckpointData))]
[JsonSerializable(typeof(Checkpoint))]
internal partial class MultiDocumentProjectionJsonContext : JsonSerializerContext
{
}
