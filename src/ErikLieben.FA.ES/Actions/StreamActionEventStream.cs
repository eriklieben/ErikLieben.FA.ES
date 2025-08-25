using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Notifications;
using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Actions;

public class StreamActionEventStream(IEventStream eventStream)
    : BaseEventStream(eventStream.Document, eventStream.StreamDependencies), IEventStream
{
    public new IEventStreamSettings Settings => eventStream.Settings;
    public new IObjectDocumentWithMethods Document => eventStream.Document;

    public new void RegisterAction(IAction action)
    {
        eventStream.RegisterAction(action);
    }

    public new void RegisterNotification(INotification notification)
    {
        eventStream.RegisterNotification(notification);
    }

    public new void RegisterPostAppendAction(IPostAppendAction action)
    {
        eventStream.RegisterPostAppendAction(action);
    }

    public new void RegisterPreAppendAction(IPreAppendAction action)
    {
        eventStream.RegisterPreAppendAction(action);
    }

    public new void RegisterPostReadAction(IPostReadAction action)
    {
        eventStream.RegisterPostReadAction(action);
    }

    public new Task<IReadOnlyCollection<IEvent>> ReadAsync(int startVersion = 0, int? untilVersion = null, bool useExternalSequencer = false)
    {
        return eventStream.ReadAsync(startVersion, untilVersion, useExternalSequencer);
    }

    public new async Task Session(Action<ILeasedSession> context, Constraint constraint = Constraint.Loose)
    {
        var session = new StreamActionLeasedSession(GetSession(new List<IAction>()));
        context(session);
        await session.CommitAsync();
    }

    public new Task Snapshot<T>(int untilVersion, string? name = null) where T : class, IBase
    {
        return eventStream.Snapshot<T>(untilVersion, name);
    }

    public new Task<object?> GetSnapShot(int version, string? name = null)
    {
        return eventStream.GetSnapShot(version, name);
    }

    public new void SetSnapShotType(JsonTypeInfo typeInfo, string? version = null)
    {
        eventStream.SetSnapShotType(typeInfo, version);
    }

    public new void SetAggregateType(JsonTypeInfo typeInfo)
    {
        eventStream.SetAggregateType(typeInfo);
    }
}
