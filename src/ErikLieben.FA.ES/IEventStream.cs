﻿using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Notifications;
using ErikLieben.FA.ES.Processors;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES;

/// <summary>
/// Represents an event stream for an object document, providing read, append, snapshot, and registration operations.
/// </summary>
public interface IEventStream
{
    /// <summary>
    /// Gets settings that control behavior of the event stream (e.g., stream type and JSON serializers).
    /// </summary>
    public IEventStreamSettings Settings { get; }

    /// <summary>
    /// Gets the associated object document and its stream-related methods.
    /// </summary>
    public IObjectDocumentWithMethods Document { get; }

    /// <summary>
    /// Gets the dependencies used by the stream (data store, snapshot store, etc.).
    /// </summary>
    public IStreamDependencies StreamDependencies { get; }

    /// <summary>
    /// Gets the registry that maps event names to types and JSON metadata for serialization.
    /// </summary>
    public EventTypeRegistry EventTypeRegistry { get; }

    /// <summary>
    /// Registers an event type and its JSON metadata under a logical name.
    /// </summary>
    /// <typeparam name="T">The CLR type of the event payload.</typeparam>
    /// <param name="eventName">The logical event name to use in the stream.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type info for <typeparamref name="T"/>.</param>
    void RegisterEvent<T>(string eventName, JsonTypeInfo<T> jsonTypeInfo);

    /// <summary>
    /// Registers a command/action that can append events to the stream.
    /// </summary>
    /// <param name="action">The action to register.</param>
    void RegisterAction(IAction action);

    /// <summary>
    /// Registers a notification hook that runs on specific stream events.
    /// </summary>
    /// <param name="notification">The notification to register.</param>
    void RegisterNotification(INotification notification);

    /// <summary>
    /// Registers a processor that runs after events are appended.
    /// </summary>
    /// <param name="action">The post-append action to register.</param>
    void RegisterPostAppendAction(IPostAppendAction action);

    /// <summary>
    /// Registers a processor that runs before events are appended.
    /// </summary>
    /// <param name="action">The pre-append action to register.</param>
    void RegisterPreAppendAction(IPreAppendAction action);

    /// <summary>
    /// Registers a processor that runs after reading events.
    /// </summary>
    /// <param name="action">The post-read action to register.</param>
    void RegisterPostReadAction(IPostReadAction action);

    /// <summary>
    /// Reads events from the stream within the specified range.
    /// </summary>
    /// <param name="startVersion">The starting version (inclusive); default 0.</param>
    /// <param name="untilVersion">The last version to read (inclusive); null for end of stream.</param>
    /// <param name="useExternalSequencer">True to use external sequencing when available.</param>
    /// <returns>A read-only collection of events.</returns>
    Task<IReadOnlyCollection<IEvent>> ReadAsync(
        int startVersion = 0, int? untilVersion = null, bool useExternalSequencer = false);

    /// <summary>
    /// Executes a session with a leased stream context applying the given constraint.
    /// </summary>
    /// <param name="context">The work to execute within the leased session.</param>
    /// <param name="constraint">The constraint applied to the session; default is loose.</param>
    Task Session(Action<ILeasedSession> context, Constraint constraint = Constraint.Loose);

    /// <summary>
    /// Creates a snapshot of the aggregate at the specified version.
    /// </summary>
    /// <typeparam name="T">The aggregate base type to snapshot.</typeparam>
    /// <param name="untilVersion">The version up to which the snapshot is taken.</param>
    /// <param name="name">An optional name or version of the snapshot type.</param>
    Task Snapshot<T>(int untilVersion, string? name = null) where T : class, IBase;

    /// <summary>
    /// Retrieves a snapshot of the aggregate, if available.
    /// </summary>
    /// <param name="version">The version the snapshot was taken at.</param>
    /// <param name="name">An optional name or version of the snapshot type.</param>
    /// <returns>The snapshot object when found; otherwise null.</returns>
    Task<object?> GetSnapShot(int version, string? name = null);

    /// <summary>
    /// Sets the snapshot type info used to serialize/deserialize snapshots.
    /// </summary>
    /// <param name="typeInfo">The source-generated JSON type information.</param>
    /// <param name="version">An optional version string for the snapshot type.</param>
    void SetSnapShotType(JsonTypeInfo typeInfo, string? version = null);

    /// <summary>
    /// Sets the aggregate type info used for folding events.
    /// </summary>
    /// <param name="typeInfo">The source-generated JSON type information.</param>
    void SetAggregateType(JsonTypeInfo typeInfo);
}
