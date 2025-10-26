using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.Notifications;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Upcasting;
using System.Diagnostics;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Base class for event stream implementations providing core functionality for event storage, retrieval, and processing.
/// </summary>
public abstract class BaseEventStream : IEventStream
{
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES");

    /// <summary>
    /// Gets the object document with methods for this event stream.
    /// </summary>
    public IObjectDocumentWithMethods Document { get; }

    /// <summary>
    /// Gets the stream dependencies (data store, factories, etc.).
    /// </summary>
    public IStreamDependencies StreamDependencies { get; }

    /// <summary>
    /// Gets the event stream settings.
    /// </summary>
    public IEventStreamSettings Settings { get; } = new EventStreamSettings();

    /// <summary>
    /// Gets the event type registry for managing event type mappings.
    /// </summary>
    public EventTypeRegistry EventTypeRegistry { get; } = new();

    /// <summary>
    /// Gets the registered actions for the event stream.
    /// </summary>
    protected readonly List<IAction> Actions = [];

    /// <summary>
    /// Gets the registered notifications for the event stream.
    /// </summary>
    protected readonly List<INotification> Notifications = [];

    /// <summary>
    /// Gets the registered upcasters for event migration.
    /// </summary>
    protected readonly List<IEventUpcaster> UpCasters = [];

    /// <summary>
    /// Gets or sets the JSON type information for snapshot serialization.
    /// </summary>
    protected JsonTypeInfo? JsonTypeInfoSnapshot;

    /// <summary>
    /// Gets or sets the JSON type information for aggregate serialization.
    /// </summary>
    protected JsonTypeInfo? JsonTypeInfoAgg;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseEventStream"/> class.
    /// </summary>
    /// <param name="document">The object document with methods.</param>
    /// <param name="streamDependencies">The stream dependencies.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    protected BaseEventStream(
        IObjectDocumentWithMethods document,
        IStreamDependencies streamDependencies)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(streamDependencies);

        Document = document;
        StreamDependencies = streamDependencies;
    }

    /// <summary>
    /// Reads events from the stream within the specified version range.
    /// </summary>
    /// <param name="startVersion">The starting version (inclusive). Defaults to 0.</param>
    /// <param name="untilVersion">The ending version (inclusive). If null, reads to the latest version.</param>
    /// <param name="useExternalSequencer">Whether to order events by external sequencer.</param>
    /// <returns>A read-only collection of events.</returns>
    public async Task<IReadOnlyCollection<IEvent>> ReadAsync(
        int startVersion = 0,
        int? untilVersion = null,
        bool useExternalSequencer = false)
    {
        using var activity = ActivitySource.StartActivity("EventStream.ReadAsync");
        var events = new List<IEvent>();

        if (Document.Active.ChunkingEnabled())
        {
            foreach (var chunk in Document.Active.StreamChunks)
            {
                var data = await StreamDependencies.DataStore.ReadAsync(Document, startVersion, untilVersion, chunk: chunk.ChunkIdentifier);
                if (data != null)
                {
                    events.AddRange(data);
                }
            }
        }
        else
        {
            var data = await StreamDependencies.DataStore.ReadAsync(Document, startVersion, untilVersion);
            if (data != null)
            {
                events.AddRange(data);
            }
        }

        if (useExternalSequencer)
        {
            events = [.. events.OrderBy(e => e.ExternalSequencer)];
        }

        if (UpCasters.Count != 0)
        {
            events = TryUpcasting(events);
        }

        return events.Where(e => e != null).ToList();
    }

    private List<IEvent> TryUpcasting(List<IEvent> events)
    {
        for (var i = 0; i < events.Count; i++)
        {
            var @event = events[i];
            foreach (var upcast in UpCasters)
            {
                if (@event == null || !upcast.CanUpcast(@event))
                {
                    continue;
                }

                Upcast(ref events, i, ref @event, upcast);
            }
        }

        return events;
    }

    private static void Upcast(ref List<IEvent> events, int i, ref IEvent? @event, IEventUpcaster upcast)
    {
        if (@event == null) {
            return;
        }

        var upcastedTo = upcast.UpCast(@event).ToArray();
        switch (upcastedTo.Length)
        {
            case 1:
                @event = upcastedTo[0];
                events[i] = @event;
                break;
            case > 1:
                {
                    @event = upcastedTo[0];
                    var nextItem = i < events.Count ? (Index)(i + 1) : (Index)(events.Count - 1);
                    var prevItem = i > 0 ? (Index)(i - 1) : (Index)0;
                    events = [.. events[0..prevItem], .. upcastedTo, .. events[nextItem..]];
                    break;
                }
            default:
                events[i] = @event;
                break;
        }
    }

    /// <summary>
    /// Registers an event type with its associated metadata.
    /// </summary>
    /// <typeparam name="T">The CLR type of the event.</typeparam>
    /// <param name="eventName">The name used to identify the event in storage.</param>
    /// <param name="jsonTypeInfo">The JSON type information for serialization/deserialization.</param>
    public void RegisterEvent<T>(string eventName, JsonTypeInfo<T> jsonTypeInfo)
    {
        using var activity = ActivitySource.StartActivity("EventStream.RegisterEvent");
        var type = typeof(T);
        activity?.AddTag("EventType", type.FullName);
        activity?.AddTag("EventName", eventName);

        EventTypeRegistry.Add(type, eventName, jsonTypeInfo);
    }

    /// <summary>
    /// Registers a general action to be executed in the event processing pipeline.
    /// </summary>
    /// <param name="action">The action to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    public void RegisterAction(IAction action)
    {
        using var activity = ActivitySource.StartActivity("EventStream.RegisterAction");
        activity?.AddTag("ActionType", action.GetType().FullName);
        ArgumentNullException.ThrowIfNull(action);
        Actions.Add(action);
    }

    /// <summary>
    /// Registers an action to execute after events are appended to the stream.
    /// </summary>
    /// <param name="action">The post-append action to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    public void RegisterPostAppendAction(IPostAppendAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Actions.Add(action);
    }

    /// <summary>
    /// Registers an action to execute after events are read from the stream.
    /// </summary>
    /// <param name="action">The post-read action to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    public void RegisterPostReadAction(IPostReadAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Actions.Add(action);
    }

    /// <summary>
    /// Registers an action to execute before events are appended to the stream.
    /// </summary>
    /// <param name="action">The pre-append action to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    public void RegisterPreAppendAction(IPreAppendAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Actions.Add(action);
    }

    /// <summary>
    /// Registers a notification handler to receive event notifications.
    /// </summary>
    /// <param name="notification">The notification handler to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when notification is null.</exception>
    public void RegisterNotification(INotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        Notifications.Add(notification);
    }

    /// <summary>
    /// Registers an upcaster for migrating events from old versions to new versions.
    /// </summary>
    /// <param name="upcaster">The upcaster to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when upcaster is null.</exception>
    public void RegisterUpcaster(IEventUpcaster upcaster)
    {
        ArgumentNullException.ThrowIfNull(upcaster);
        UpCasters.Add(upcaster);
    }

    /// <summary>
    /// Creates and executes a session for appending events to the stream.
    /// </summary>
    /// <param name="context">The action to execute within the session context.</param>
    /// <param name="constraint">The concurrency constraint for the session. Defaults to <see cref="Constraint.Loose"/>.</param>
    /// <returns>A task representing the asynchronous session operation.</returns>
    /// <exception cref="ConstraintException">Thrown when the constraint is violated.</exception>
    public async Task Session(Action<ILeasedSession> context, Constraint constraint = Constraint.Loose)
    {
        using var activity = ActivitySource.StartActivity("EventStream.Session");

        switch (constraint)
        {
            case Constraint.Existing when Document.Active.CurrentStreamVersion == -1:
                throw new ConstraintException($"The {Document.ObjectName} by id {Document.ObjectId} does not exist while the session is constrained for an existing stream.", constraint);
            case Constraint.New when Document.Active.CurrentStreamVersion > -1:
                throw new ConstraintException($"The {Document.ObjectName} by id {Document.ObjectId} exists while the session is constrained for a new stream.", constraint);
        }

        var session = GetSession(Actions);

        context(session);
        await session.CommitAsync();
    }

    /// <summary>
    /// Creates a snapshot of the aggregate state at the specified version.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="untilVersion">The version up to which to create the snapshot.</param>
    /// <param name="name">Optional name for the snapshot.</param>
    /// <returns>A task representing the asynchronous snapshot operation.</returns>
    /// <exception cref="SnapshotJsonTypeInfoNotSetException">Thrown when JSON type info is not set.</exception>
    public async Task Snapshot<T>(int untilVersion, string? name = null) where T : class, IBase
    {
        using var activity = ActivitySource.StartActivity("EventStream.Snapshot");

        if (JsonTypeInfoAgg == null)
        {
            throw new SnapshotJsonTypeInfoNotSetException();
        }

        var factory = StreamDependencies.AggregateFactory.GetFactory<T>();
        var obj = factory!.Create(this);

        var events = await ReadAsync(0, untilVersion);

        foreach(var @event in events)
        {
            obj.Fold(@event);
        }

        await StreamDependencies.SnapshotStore.SetAsync(obj, JsonTypeInfoAgg, Document, untilVersion, name);
        Document.Active.SnapShots.Add(new StreamSnapShot { UntilVersion = untilVersion, Name = name });
        await StreamDependencies.ObjectDocumentFactory.SetAsync(Document);
    }

    /// <summary>
    /// Retrieves a snapshot at the specified version.
    /// </summary>
    /// <param name="version">The version of the snapshot to retrieve.</param>
    /// <param name="name">Optional name of the snapshot.</param>
    /// <returns>The snapshot object, or null if not found.</returns>
    /// <exception cref="SnapshotJsonTypeInfoNotSetException">Thrown when JSON type info is not set.</exception>
    public Task<object?> GetSnapShot(int version, string? name = null)
    {
        if (JsonTypeInfoSnapshot == null)
        {
            throw new SnapshotJsonTypeInfoNotSetException();
        }

        return StreamDependencies.SnapshotStore.GetAsync(JsonTypeInfoSnapshot, Document, version, name);
    }

    /// <summary>
    /// Sets the JSON type information for snapshot deserialization.
    /// </summary>
    /// <param name="typeInfo">The JSON type information for the snapshot type.</param>
    /// <param name="version">Optional version identifier for the snapshot schema.</param>
    /// <exception cref="ArgumentNullException">Thrown when typeInfo is null.</exception>
    public void SetSnapShotType(JsonTypeInfo typeInfo, string? version = null)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        JsonTypeInfoSnapshot = typeInfo;
    }

    /// <summary>
    /// Sets the JSON type information for aggregate serialization.
    /// </summary>
    /// <param name="typeInfo">The JSON type information for the aggregate type.</param>
    /// <exception cref="ArgumentNullException">Thrown when typeInfo is null.</exception>
    public void SetAggregateType(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        JsonTypeInfoAgg = typeInfo;
    }

    /// <summary>
    /// Creates a new leased session for appending events with the specified actions.
    /// </summary>
    /// <param name="actions">The collection of actions to register with the session.</param>
    /// <returns>A new leased session instance.</returns>
    protected ILeasedSession GetSession(List<IAction> actions)
    {
        return new LeasedSession(
            this,
            Document,
            StreamDependencies.DataStore,
            StreamDependencies.ObjectDocumentFactory,
            Notifications.OfType<IStreamDocumentChunkClosedNotification>(),
            actions.OfType<IAsyncPostCommitAction>(),
            actions.OfType<IPreAppendAction>(),
            actions.OfType<IPostReadAction>());
    }
}
