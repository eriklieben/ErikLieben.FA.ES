using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Notifications;
using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Decorates an event stream with action registration and execution capabilities,
/// allowing pre- and post-processing of events during read and append operations.
/// </summary>
public class StreamActionEventStream(IEventStream eventStream)
    : BaseEventStream(eventStream.Document, eventStream.StreamDependencies), IEventStream
{
    /// <summary>
    /// Gets the event stream settings.
    /// </summary>
    public new IEventStreamSettings Settings => eventStream.Settings;

    /// <summary>
    /// Gets the object document with methods for the stream.
    /// </summary>
    public new IObjectDocumentWithMethods Document => eventStream.Document;

    /// <summary>
    /// Registers a general action to be executed in the event processing pipeline.
    /// </summary>
    /// <param name="action">The action to register.</param>
    public new void RegisterAction(IAction action)
    {
        eventStream.RegisterAction(action);
    }

    /// <summary>
    /// Registers a notification handler to receive event notifications.
    /// </summary>
    /// <param name="notification">The notification handler to register.</param>
    public new void RegisterNotification(INotification notification)
    {
        eventStream.RegisterNotification(notification);
    }

    /// <summary>
    /// Registers an action to execute after events are appended to the stream.
    /// </summary>
    /// <param name="action">The post-append action to register.</param>
    public new void RegisterPostAppendAction(IPostAppendAction action)
    {
        eventStream.RegisterPostAppendAction(action);
    }

    /// <summary>
    /// Registers an action to execute before events are appended to the stream.
    /// </summary>
    /// <param name="action">The pre-append action to register.</param>
    public new void RegisterPreAppendAction(IPreAppendAction action)
    {
        eventStream.RegisterPreAppendAction(action);
    }

    /// <summary>
    /// Registers an action to execute after events are read from the stream.
    /// </summary>
    /// <param name="action">The post-read action to register.</param>
    public new void RegisterPostReadAction(IPostReadAction action)
    {
        eventStream.RegisterPostReadAction(action);
    }

    /// <summary>
    /// Reads events from the stream asynchronously within the specified version range.
    /// </summary>
    /// <param name="startVersion">The starting version (inclusive). Defaults to 0.</param>
    /// <param name="untilVersion">The ending version (inclusive). If null, reads to the latest version.</param>
    /// <param name="useExternalSequencer">Whether to use an external sequencer for ordering events.</param>
    /// <returns>A read-only collection of events.</returns>
    public Task<IReadOnlyCollection<IEvent>> ReadAsync(int startVersion = 0, int? untilVersion = null, bool useExternalSequencer = false)
    {
        return eventStream.ReadAsync(startVersion, untilVersion, useExternalSequencer);
    }

    /// <summary>
    /// Creates and executes a session for appending events to the stream with registered actions.
    /// </summary>
    /// <param name="context">The action to execute within the session context.</param>
    /// <param name="constraint">The concurrency constraint for the session. Defaults to <see cref="Constraint.Loose"/>.</param>
    /// <returns>A task representing the asynchronous session operation.</returns>
    public async Task Session(Action<ILeasedSession> context, Constraint constraint = Constraint.Loose)
    {
        var session = new StreamActionLeasedSession(GetSession([]));
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
    public Task Snapshot<T>(int untilVersion, string? name = null) where T : class, IBase
    {
        return eventStream.Snapshot<T>(untilVersion, name);
    }

    /// <summary>
    /// Retrieves a snapshot at the specified version.
    /// </summary>
    /// <param name="version">The version of the snapshot to retrieve.</param>
    /// <param name="name">Optional name of the snapshot.</param>
    /// <returns>The snapshot object, or null if not found.</returns>
    public Task<object?> GetSnapShot(int version, string? name = null)
    {
        return eventStream.GetSnapShot(version, name);
    }

    /// <summary>
    /// Sets the JSON type information for snapshot serialization.
    /// </summary>
    /// <param name="typeInfo">The JSON type information for the snapshot type.</param>
    /// <param name="version">Optional version identifier for the snapshot type.</param>
    public new void SetSnapShotType(JsonTypeInfo typeInfo, string? version = null)
    {
        eventStream.SetSnapShotType(typeInfo, version);
    }

    /// <summary>
    /// Sets the JSON type information for aggregate serialization.
    /// </summary>
    /// <param name="typeInfo">The JSON type information for the aggregate type.</param>
    public new void SetAggregateType(JsonTypeInfo typeInfo)
    {
        eventStream.SetAggregateType(typeInfo);
    }
}
