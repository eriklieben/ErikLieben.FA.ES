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

public abstract class BaseEventStream : IEventStream
{
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES");

    public IObjectDocumentWithMethods Document { get; }
    public IStreamDependencies StreamDependencies { get; }
    public IEventStreamSettings Settings { get; } = new EventStreamSettings();

    public EventTypeRegistry EventTypeRegistry { get; } = new();

    protected readonly List<IAction> Actions = [];
    protected readonly List<INotification> Notifications = [];
    protected readonly List<IEventUpcaster> UpCasters = [];
    protected JsonTypeInfo? JsonTypeInfoSnapshot;
    protected JsonTypeInfo? JsonTypeInfoAgg;

    protected BaseEventStream(
        IObjectDocumentWithMethods document,
        IStreamDependencies streamDependencies)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(streamDependencies);

        Document = document;
        StreamDependencies = streamDependencies;
    }

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

    public void RegisterEvent<T>(string eventName, JsonTypeInfo<T> jsonTypeInfo)
    {
        using var activity = ActivitySource.StartActivity("EventStream.RegisterEvent");
        var type = typeof(T);
        activity?.AddTag("EventType", type.FullName);
        activity?.AddTag("EventName", eventName);

        EventTypeRegistry.Add(type, eventName, jsonTypeInfo);
    }

    public void RegisterAction(IAction action)
    {
        using var activity = ActivitySource.StartActivity("EventStream.RegisterAction");
        activity?.AddTag("ActionType", action.GetType().FullName);
        ArgumentNullException.ThrowIfNull(action);
        Actions.Add(action);
    }

    public void RegisterPostAppendAction(IPostAppendAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Actions.Add(action);
    }

    public void RegisterPostReadAction(IPostReadAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Actions.Add(action);
    }

    public void RegisterPreAppendAction(IPreAppendAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Actions.Add(action);
    }

    public void RegisterNotification(INotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        Notifications.Add(notification);
    }

    public void RegisterUpcaster(IEventUpcaster upcaster)
    {
        ArgumentNullException.ThrowIfNull(upcaster);
        UpCasters.Add(upcaster);
    }

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

    public async Task Snapshot<T>(int untilVersion, string? name = null) where T : class, IBase
    {
        using var activity = ActivitySource.StartActivity("EventStream.Snapshot");

        if (JsonTypeInfoAgg == null)
        {
            throw new SnapshotJsonTypeInfoNotSetException();
        }

        var factory = StreamDependencies.AggregateFactory.GetFactory(typeof(T));
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

    public Task<object?> GetSnapShot(int version, string? name = null)
    {
        if (JsonTypeInfoSnapshot == null)
        {
            throw new SnapshotJsonTypeInfoNotSetException();
        }

        return StreamDependencies.SnapshotStore.GetAsync(JsonTypeInfoSnapshot, Document, version, name);
    }

    public void SetSnapShotType(JsonTypeInfo typeInfo, string? version = null)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        JsonTypeInfoSnapshot = typeInfo;
    }

    public void SetAggregateType(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        JsonTypeInfoAgg = typeInfo;
    }

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
