using ErikLieben.FA.ES.Documents;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Provides a base implementation for projections that fold event streams into materialized state and track processing checkpoints.
/// </summary>
public abstract class Projection : IProjectionBase
{
    /// <summary>
    /// Provides access to the object document storage used to load projection source documents.
    /// </summary>
    protected readonly IObjectDocumentFactory? DocumentFactory;
    /// <summary>
    /// Provides access to the event stream used to read events required to update the projection state.
    /// </summary>
    protected readonly IEventStreamFactory? EventStreamFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="Projection"/> class.
    /// </summary>
    protected Projection()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Projection"/> class using the specified factories.
    /// </summary>
    /// <param name="documentFactory">The factory that retrieves object documents for the projection.</param>
    /// <param name="eventStreamFactory">The factory that creates event streams to read events from.</param>
    protected Projection(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory)
    {
        DocumentFactory = documentFactory;
        EventStreamFactory = eventStreamFactory;
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="Projection"/> class with an initial checkpoint state.
    /// </summary>
    /// <param name="documentFactory">The factory that retrieves object documents for the projection.</param>
    /// <param name="eventStreamFactory">The factory that creates event streams to read events from.</param>
    /// <param name="checkpoint">The initial checkpoint map indicating processed version identifiers per stream.</param>
    /// <param name="checkpointFingerprint">An optional fingerprint representing the checkpoint integrity; may be null.</param>
    protected Projection(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        Checkpoint checkpoint,
        string? checkpointFingerprint) : this(documentFactory, eventStreamFactory)
    {
        Checkpoint = checkpoint;
        CheckpointFingerprint = checkpointFingerprint;
    }

    /// <summary>
    /// Folds a single event into the projection state using the specified document and optional data/context.
    /// </summary>
    /// <typeparam name="T">The type of auxiliary data provided to the fold operation.</typeparam>
    /// <param name="event">The event to fold.</param>
    /// <param name="document">The object document that represents the projection target.</param>
    /// <param name="data">Optional auxiliary data passed to the fold operation; may be null.</param>
    /// <param name="context">Optional execution context for the operation; may be null.</param>
    /// <returns>A task that represents the asynchronous fold operation.</returns>
    public abstract Task Fold<T>(IEvent @event, IObjectDocument document, T? data = null,
        IExecutionContext? context = null)
        where T : class;

    /// <summary>
    /// Folds a single event into the projection without auxiliary data or execution context.
    /// </summary>
    /// <param name="event">The event to fold.</param>
    /// <param name="document">The projection object document.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Fold(IEvent @event, IObjectDocument document)
    {
        return Fold<object>(@event, document, null!, null!);
    }

    /// <summary>
    /// Folds a single event into the projection with an execution context but without auxiliary data.
    /// </summary>
    /// <param name="event">The event to fold.</param>
    /// <param name="document">The projection object document.</param>
    /// <param name="context">Optional execution context for the operation; may be null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected Task Fold(IEvent @event, IObjectDocument document, IExecutionContext? context)
    {
        return Fold<object>(@event, document, null!, context);
    }

    // public abstract void LoadFromJson(string json);

    /// <summary>
    /// Serializes the projection state to a JSON string.
    /// </summary>
    /// <returns>A JSON representation of the current projection.</returns>
    public abstract string ToJson();

    private readonly VersionTokenComparer comparer = new VersionTokenComparer();

    /// <summary>
    /// Determines whether the specified version token represents a newer version than the current checkpoint.
    /// </summary>
    /// <param name="token">The version token to compare against the current checkpoint.</param>
    /// <returns>True if the token is newer than the checkpoint or the checkpoint does not contain the token; otherwise, false.</returns>
    protected bool IsNewer(VersionToken token)
    {
        if (Checkpoint.TryGetValue(token.ObjectIdentifier, out var value))
        {
            return comparer.IsNewer(
                token.Value,
                $"{token.ObjectIdentifier}__{value}");
        }

        return true;
    }

    /// <summary>
    /// Executes an action after all relevant events have been folded for the specified document.
    /// </summary>
    /// <param name="document">The projection object document that has been updated.</param>
    /// <returns>A task that represents the asynchronous post-processing operation.</returns>
    protected abstract Task PostWhenAll(IObjectDocument document);

    /// <summary>
    /// Gets the set of parameter value factories keyed by a parameter type identifier, used to resolve values for When-method parameters.
    /// </summary>
    protected abstract Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; }

    /// <summary>
    /// Resolves the value for a parameter used in a When-method based on the configured factories.
    /// </summary>
    /// <typeparam name="T">The type of the parameter to create.</typeparam>
    /// <typeparam name="Te">The event payload type expected by the factory.</typeparam>
    /// <param name="forType">The identifier of the parameter type to resolve.</param>
    /// <param name="document">The current projection object document.</param>
    /// <param name="event">The current event being processed.</param>
    /// <returns>The created parameter value or null when no matching factory exists.</returns>
    protected T? GetWhenParameterValue<T, Te>(string forType, IObjectDocument document, IEvent @event)
        where Te : class where T : class
    {
        WhenParameterValueFactories.TryGetValue(forType, out var factory);
        switch (factory)
        {
            case null:
                return null;
            case IProjectionWhenParameterValueFactory<T, Te> factoryWithEvent:
            {
                var eventW = @event as IEvent<Te>;
                return factoryWithEvent?.Create(document, eventW!);
            }
            case IProjectionWhenParameterValueFactory<T> factoryWithoutEvent:
                return factoryWithoutEvent.Create(document, @event);
            default:
                return null;
        }
    }

    /// <summary>
    /// Updates the projection to the specified version by reading and folding events from the event stream.
    /// </summary>
    /// <param name="token">The version token that identifies the object and target version.</param>
    /// <param name="context">Optional execution context for the update operation; may be null.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required factories are not initialized.</exception>
    public async Task UpdateToVersion(VersionToken token, IExecutionContext? context = null)
    {
        if (DocumentFactory == null)
        {
            throw new InvalidOperationException("DocumentFactory is not initialized on this Projection instance.");
        }
        if (EventStreamFactory == null)
        {
            throw new InvalidOperationException("EventStreamFactory is not initialized on this Projection instance.");
        }

        // Guard clause to reduce nesting and cognitive complexity
        if (!IsNewer(token) && !token.TryUpdateToLatestVersion)
        {
            return;
        }

        var startIdx = -1;
        if (Checkpoint.TryGetValue(token.ObjectIdentifier, out var value))
        {
            startIdx = new VersionToken(token.ObjectIdentifier, value).Version + 1;
        }

        var document = await DocumentFactory.GetAsync(token.ObjectName, token.ObjectId);
        var eventStream = EventStreamFactory.Create(document);
        var events = token.TryUpdateToLatestVersion ?
            await eventStream.ReadAsync(startIdx) :
            await eventStream.ReadAsync(startIdx, token.Version);

        foreach (var @event in events)
        {
            await Fold(@event, document, context);
            UpdateVersionIndex(@event, document);
        }
        await PostWhenAll(document);
    }

    /// <summary>
    /// Updates the projection to the specified version using auxiliary data and a typed execution context.
    /// </summary>
    /// <typeparam name="T">The type of the auxiliary data and execution context.</typeparam>
    /// <param name="token">The version token that identifies the object and target version.</param>
    /// <param name="context">An optional execution context carrying the parent event and correlation; may be null.</param>
    /// <param name="data">Optional auxiliary data made available during folding; may be null.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required factories are not initialized.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the parent event in the context equals the current event, indicating a processing loop.</exception>
    public async Task UpdateToVersion<T>(VersionToken token, IExecutionContextWithData<T>? context = null, T? data = null)
        where T: class
    {
        if (DocumentFactory == null)
        {
            throw new InvalidOperationException("DocumentFactory is not initialized on this Projection instance.");
        }
        if (EventStreamFactory == null)
        {
            throw new InvalidOperationException("EventStreamFactory is not initialized on this Projection instance.");
        }

        if (!IsNewer(token) && !token.TryUpdateToLatestVersion)
        {
            return;
        }

        var startIdx = -1;
        if (Checkpoint.TryGetValue(token.ObjectIdentifier, out var value))
        {
            startIdx = new VersionToken(token.ObjectIdentifier, value).Version + 1;
        }

        var document = await DocumentFactory.GetAsync(token.ObjectName, token.ObjectId);
        var eventStream = EventStreamFactory.Create(document);
        var events = token.TryUpdateToLatestVersion ?
            await eventStream.ReadAsync(startIdx) :
            await eventStream.ReadAsync(startIdx, token.Version);

        foreach (var @event in events)
        {
            if (context != null && @event == context.Event)
            {
                throw new InvalidOperationException("Parent event is the same as the current event; a processing loop may be occurring.");
            }

            await Fold(@event, document, data, context);
            UpdateVersionIndex(@event, document);
        }

        if (events.Count > 0)
        {
            await PostWhenAll(document);
        }
    }

    // public async Task UpdateToVersion(VersionToken token)
    // {
    //     if (documentFactory == null || eventStreamFactory == null)
    //     {
    //         throw new Exception("documentFactory or eventStreamFactory is null");
    //     }
    //
    //     if (IsNewer(token) || token.TryUpdateToLatestVersion)
    //     {
    //         var startIdx = -1;
    //         if (VersionIndex != null && VersionIndex.TryGetValue(token.ObjectIdentifier, out var value))
    //         {
    //             startIdx = new VersionToken(token.ObjectIdentifier, value).Version + 1;
    //         }
    //
    //         var document = await documentFactory.GetAsync(token.ObjectName, token.ObjectId);
    //         var eventStream = eventStreamFactory.Create(document);
    //         var events = token.TryUpdateToLatestVersion ?
    //             await eventStream.ReadAsync(startIdx) :
    //             await eventStream.ReadAsync(startIdx, token.Version);
    //
    //         foreach (var @event in events)
    //         {
    //             var executionContext = new ExecutionContext(@event, null!); // TODO: context.,..
    //             await Fold(@event, document, executionContext);
    //             UpdateVersionIndex(@event, document);
    //         }
    //     }
    // }

    /// <summary>
    /// Updates the projection to the latest versions for all tracked streams in the checkpoint.
    /// </summary>
    /// <param name="context">Optional execution context for the update operation; may be null.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public async Task UpdateToLatestVersion(IExecutionContext? context = null)
    {
        foreach (var versionToken in Checkpoint)
        {
            await UpdateToVersion(new VersionToken(versionToken.Key, versionToken.Value).ToLatestVersion(), null, context);
        }
    }

    private void UpdateVersionIndex(IEvent @event, IObjectDocument document)
    {
        var idString = new VersionToken(@event, document);
        if (Checkpoint!.ContainsKey(idString.ObjectIdentifier))
        {
            Checkpoint[idString.ObjectIdentifier] = idString.VersionIdentifier;
        }
        else
        {
            Checkpoint.Add(idString.ObjectIdentifier, idString.VersionIdentifier);
        }

        CheckpointFingerprint = GenerateCheckpointFingerprint();
    }

    private string GenerateCheckpointFingerprint()
    {
        StringBuilder sb = new();
        Checkpoint!.OrderBy(i => i.Key).ToList().ForEach(i => sb.AppendLine($"{i.Key}|{i.Value}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        StringBuilder builder = new();
        foreach (var t in bytes)
        {
            builder.Append(t.ToString("x2"));
        }
        var checkpointFingerprint = builder.ToString();
        return checkpointFingerprint;
    }

    /// <summary>
    /// Gets or sets the checkpoint map storing the latest processed version identifiers per object stream.
    /// </summary>
    [JsonPropertyName("$checkpoint")]
    public abstract Checkpoint Checkpoint { get; set; }

    /// <summary>
    /// Gets or sets the fingerprint (SHA-256 hex string) computed from the checkpoint content; null when the checkpoint is empty.
    /// </summary>
    [JsonPropertyName("$checkpointFingerprint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CheckpointFingerprint { get; set; }
}
