using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Notifications;
using ErikLieben.FA.ES.Processors;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES;

public interface IEventStream
{
    public IEventStreamSettings Settings { get; }
    public IObjectDocumentWithMethods Document { get; }
    public IStreamDependencies StreamDependencies { get; }

    public EventTypeRegistry EventTypeRegistry { get; }

    void RegisterEvent<T>(string eventName, JsonTypeInfo<T> jsonTypeInfo);

    void RegisterAction(IAction action);
    void RegisterNotification(INotification notification);
    void RegisterPostAppendAction(IPostAppendAction action);
    void RegisterPreAppendAction(IPreAppendAction action);
    void RegisterPostReadAction(IPostReadAction action);

    Task<IReadOnlyCollection<IEvent>> ReadAsync(
        int startVersion = 0, int? untilVersion = null, bool useExternalSequencer = false);

    Task Session(Action<ILeasedSession> context, Constraint constraint = Constraint.Loose);

    Task Snapshot<T>(int untilVersion, string? name = null) where T : class, IBase;

    Task<object?> GetSnapShot(int version, string? name = null);

    void SetSnapShotType(JsonTypeInfo typeInfo, string? version = null);
    void SetAggregateType(JsonTypeInfo typeInfo);
}
