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
    /// Folds a single event into the projection state using version token.
    /// This is the primary fold method that derived classes should implement.
    /// Version tokens are more efficient as they avoid redundant document lookups.
    /// </summary>
    /// <typeparam name="T">The type of auxiliary data provided to the fold operation.</typeparam>
    /// <param name="event">The event to fold.</param>
    /// <param name="versionToken">The version token identifying the event's version context.</param>
    /// <param name="data">Optional auxiliary data passed to the fold operation; may be null.</param>
    /// <param name="context">Optional execution context for nested projections; may be null.</param>
    /// <returns>A task that represents the asynchronous fold operation.</returns>
    public abstract Task Fold<T>(
        IEvent @event,
        VersionToken versionToken,
        T? data = null,
        IExecutionContext? context = null)
        where T : class;

    /// <summary>
    /// Folds a single event into the projection state using the specified document and optional data/context.
    /// This method exists for backwards compatibility and convenience. It creates a version token
    /// from the event and document, then delegates to the version token-based fold method.
    /// </summary>
    /// <typeparam name="T">The type of auxiliary data provided to the fold operation.</typeparam>
    /// <param name="event">The event to fold.</param>
    /// <param name="document">The object document that represents the projection target.</param>
    /// <param name="data">Optional auxiliary data passed to the fold operation; may be null.</param>
    /// <param name="context">Optional execution context for nested projections; may be null.</param>
    /// <returns>A task that represents the asynchronous fold operation.</returns>
    [Obsolete("Use Fold(IEvent, VersionToken, T?, IExecutionContext?) instead. This overload will be removed in a future major version.")]
    public virtual Task Fold<T>(IEvent @event, IObjectDocument document, T? data = null,
        IExecutionContext? context = null)
        where T : class
    {
        // Create version token from event and document
        var versionToken = new VersionToken(@event, document);

        // Delegate to the version token-based fold method
        return Fold(@event, versionToken, data, context);
    }

    /// <summary>
    /// Folds a single event into the projection without auxiliary data or execution context.
    /// </summary>
    /// <param name="event">The event to fold.</param>
    /// <param name="document">The projection object document.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Obsolete("Use Fold(IEvent, VersionToken) instead. This overload will be removed in a future major version.")]
    public Task Fold(IEvent @event, IObjectDocument document)
    {
        return Fold<object>(@event, document, null!, null!);
    }

    /// <summary>
    /// Folds a single event into the projection with an execution context but without auxiliary data.
    /// </summary>
    /// <param name="event">The event to fold.</param>
    /// <param name="document">The projection object document.</param>
    /// <param name="context">Optional execution context for nested projections; may be null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Obsolete("Use Fold(IEvent, VersionToken, IExecutionContext?) instead. This overload will be removed in a future major version.")]
    protected Task Fold(IEvent @event, IObjectDocument document, IExecutionContext? context)
    {
        return Fold<object>(@event, document, null!, context);
    }

    /// <summary>
    /// Folds a single event into the projection using version token without auxiliary data.
    /// </summary>
    /// <param name="event">The event to fold.</param>
    /// <param name="versionToken">The version token identifying the event's version context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Fold(IEvent @event, VersionToken versionToken)
    {
        return Fold<object>(@event, versionToken, null!, null!);
    }

    /// <summary>
    /// Folds a single event with version token and execution context but without auxiliary data.
    /// </summary>
    /// <param name="event">The event to fold.</param>
    /// <param name="versionToken">The version token identifying the event's version context.</param>
    /// <param name="context">Optional execution context for nested projections; may be null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected Task Fold(IEvent @event, VersionToken versionToken, IExecutionContext? context)
    {
        return Fold<object>(@event, versionToken, null!, context);
    }

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
    /// Initializes the projection from metadata provided when the projection is created as a destination.
    /// Override this method to extract values from the metadata dictionary.
    /// </summary>
    /// <param name="metadata">The metadata dictionary containing initialization values.</param>
    public virtual void InitializeFromMetadata(Dictionary<string, string> metadata)
    {
        // Default implementation does nothing. Override to use metadata values.
    }

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
    [Obsolete("Use GetWhenParameterValue<T, Te>(string, VersionToken, IEvent) instead. This overload will be removed in a future major version.")]
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
                return factoryWithEvent.Create(document, eventW!);
            }
            case IProjectionWhenParameterValueFactory<T> factoryWithoutEvent:
                return factoryWithoutEvent.Create(document, @event);
            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves the value for a parameter used in a When-method based on the configured factories using version token.
    /// </summary>
    /// <typeparam name="T">The type of the parameter to create.</typeparam>
    /// <typeparam name="Te">The event payload type expected by the factory.</typeparam>
    /// <param name="forType">The identifier of the parameter type to resolve.</param>
    /// <param name="versionToken">The current version token.</param>
    /// <param name="event">The current event being processed.</param>
    /// <returns>The created parameter value or null when no matching factory exists.</returns>
    protected T? GetWhenParameterValue<T, Te>(string forType, VersionToken versionToken, IEvent @event)
        where Te : class where T : class
    {
        WhenParameterValueFactories.TryGetValue(forType, out var factory);
        if (factory == null)
        {
            return null;
        }

        // Try event-specific factory with version token
        if (factory is IProjectionWhenParameterValueFactoryWithVersionToken<T, Te> vtFactoryWithEvent)
        {
            var eventW = @event as IEvent<Te>;
            return vtFactoryWithEvent.Create(versionToken, eventW!);
        }

        // Try non-event-specific factory with version token
        if (factory is IProjectionWhenParameterValueFactoryWithVersionToken<T> vtFactory)
        {
            return vtFactory.Create(versionToken, @event);
        }

        // Fallback: no version token support
        return null;
    }

    /// <summary>
    /// Updates the projection to the specified version by reading and folding events from the event stream.
    /// </summary>
    /// <param name="token">The version token that identifies the object and target version.</param>
    /// <param name="context">Optional execution context for nested projections; may be null.</param>
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
            // Create version token directly from event and document info
            var eventVersionToken = new VersionToken(@event, document);
            await Fold(@event, eventVersionToken, context);
            UpdateCheckpointEntry(eventVersionToken);
        }

        // Generate fingerprint once after all events are processed
        if (events.Count > 0)
        {
            CheckpointFingerprint = GenerateCheckpointFingerprint();
        }

        await PostWhenAll(document);
    }

    /// <summary>
    /// Updates the projection to the specified version using auxiliary data and a typed execution context.
    /// </summary>
    /// <typeparam name="T">The type of the auxiliary data and execution context.</typeparam>
    /// <param name="token">The version token that identifies the object and target version.</param>
    /// <param name="context">Optional execution context carrying the parent event and correlation; may be null.</param>
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

            // Create version token directly from event and document info
            var eventVersionToken = new VersionToken(@event, document);
            await Fold(@event, eventVersionToken, data, context);
            UpdateCheckpointEntry(eventVersionToken);
        }

        if (events.Count > 0)
        {
            // Generate fingerprint once after all events are processed
            CheckpointFingerprint = GenerateCheckpointFingerprint();
            await PostWhenAll(document);
        }
    }

    /// <summary>
    /// Updates the projection to the latest versions for all tracked streams in the checkpoint.
    /// </summary>
    /// <param name="context">Optional execution context for nested projections; may be null.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public async Task UpdateToLatestVersion(IExecutionContext? context = null)
    {
        foreach (var versionToken in Checkpoint)
        {
            await UpdateToVersion(new VersionToken(versionToken.Key, versionToken.Value).ToLatestVersion(), context);
        }
    }

    private void UpdateVersionIndex(VersionToken versionToken)
    {
        UpdateCheckpoint(versionToken);
    }

    /// <summary>
    /// Updates the checkpoint entry without regenerating the fingerprint.
    /// Use this when processing multiple events in a batch for better performance.
    /// </summary>
    /// <param name="versionToken">The version token that was processed.</param>
    private void UpdateCheckpointEntry(VersionToken versionToken)
    {
        if (Checkpoint!.ContainsKey(versionToken.ObjectIdentifier))
        {
            Checkpoint[versionToken.ObjectIdentifier] = versionToken.VersionIdentifier;
        }
        else
        {
            Checkpoint.Add(versionToken.ObjectIdentifier, versionToken.VersionIdentifier);
        }
    }

    /// <summary>
    /// Updates the checkpoint to record that the given version token has been processed.
    /// This method can be called externally (e.g., by a parent routed projection) to update
    /// the checkpoint after folding an event. Also regenerates the fingerprint.
    /// </summary>
    /// <param name="versionToken">The version token that was processed.</param>
    public virtual void UpdateCheckpoint(VersionToken versionToken)
    {
        UpdateCheckpointEntry(versionToken);
        CheckpointFingerprint = GenerateCheckpointFingerprint();
    }

    private string GenerateCheckpointFingerprint()
    {
        StringBuilder sb = new();
        foreach (var item in Checkpoint!.OrderBy(i => i.Key))
        {
            sb.AppendLine($"{item.Key}|{item.Value}");
        }
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        Span<char> chars = stackalloc char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i].TryFormat(chars.Slice(i * 2, 2), out _, "x2");
        }
        return new string(chars);
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
