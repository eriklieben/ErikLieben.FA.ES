using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Notifications;
using ErikLieben.FA.ES.Processors;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Upcasting;

namespace ErikLieben.FA.ES;

/// <summary>
/// Represents an event stream for an object document, providing read, append, snapshot, and registration operations.
/// </summary>
public interface IEventStream
{
    /// <summary>
    /// Gets the current version of the event stream.
    /// Returns -1 if the stream has no events (new stream).
    /// </summary>
    int CurrentVersion { get; }

    /// <summary>
    /// Gets the unique identifier for this event stream.
    /// Used for checkpoint tracking and decision validation.
    /// </summary>
    string StreamIdentifier { get; }

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
    /// Gets the registry for AOT-friendly event upcasters that transform events between schema versions.
    /// </summary>
    public EventUpcasterRegistry EventUpcasterRegistry { get; }

    /// <summary>
    /// Registers an event type and its JSON metadata under a logical name with schema version 1.
    /// </summary>
    /// <typeparam name="T">The CLR type of the event payload.</typeparam>
    /// <param name="eventName">The logical event name to use in the stream.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type info for <typeparamref name="T"/>.</param>
    void RegisterEvent<T>(string eventName, JsonTypeInfo<T> jsonTypeInfo);

    /// <summary>
    /// Registers an event type and its JSON metadata under a logical name with the specified schema version.
    /// </summary>
    /// <typeparam name="T">The CLR type of the event payload.</typeparam>
    /// <param name="eventName">The logical event name to use in the stream.</param>
    /// <param name="schemaVersion">The schema version of the event.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type info for <typeparamref name="T"/>.</param>
    void RegisterEvent<T>(string eventName, int schemaVersion, JsonTypeInfo<T> jsonTypeInfo);

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
    /// Registers an event upcast for migrating legacy event schemas to current versions.
    /// Upcasts transform old event formats into new ones during event stream replay,
    /// enabling backward compatibility and schema evolution.
    /// </summary>
    /// <param name="upcast">The event upcast to register.</param>
    void RegisterUpcast(IUpcastEvent upcast);

    /// <summary>
    /// Registers an upcaster function for transforming events from one schema version to another.
    /// This is an AOT-friendly alternative to <see cref="RegisterUpcast(IUpcastEvent)"/>.
    /// </summary>
    /// <typeparam name="TFrom">The source event type.</typeparam>
    /// <typeparam name="TTo">The target event type.</typeparam>
    /// <param name="eventName">The event name (must match the stored event type).</param>
    /// <param name="fromVersion">The source schema version to upcast from.</param>
    /// <param name="toVersion">The target schema version to upcast to.</param>
    /// <param name="upcast">The function that transforms the event.</param>
    void RegisterUpcaster<TFrom, TTo>(string eventName, int fromVersion, int toVersion, Func<TFrom, TTo> upcast)
        where TFrom : class
        where TTo : class;

    /// <summary>
    /// Reads events from the stream within the specified range.
    /// </summary>
    /// <param name="startVersion">The starting version (inclusive); default 0.</param>
    /// <param name="untilVersion">The last version to read (inclusive); null for end of stream.</param>
    /// <param name="useExternalSequencer">True to use external sequencing when available.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only collection of events.</returns>
    Task<IReadOnlyCollection<IEvent>> ReadAsync(
        int startVersion = 0,
        int? untilVersion = null,
        bool useExternalSequencer = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a session with a leased stream context applying the given constraint.
    /// </summary>
    /// <param name="context">The work to execute within the leased session.</param>
    /// <param name="constraint">The constraint applied to the session; default is loose.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task Session(Action<ILeasedSession> context, Constraint constraint = Constraint.Loose, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a snapshot of the aggregate at the specified version.
    /// </summary>
    /// <typeparam name="T">The aggregate base type to snapshot.</typeparam>
    /// <param name="untilVersion">The version up to which the snapshot is taken.</param>
    /// <param name="name">An optional name or version of the snapshot type.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task Snapshot<T>(int untilVersion, string? name = null, CancellationToken cancellationToken = default) where T : class, IBase;

    /// <summary>
    /// Retrieves a snapshot of the aggregate, if available.
    /// </summary>
    /// <param name="version">The version the snapshot was taken at.</param>
    /// <param name="name">An optional name or version of the snapshot type.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The snapshot object when found; otherwise null.</returns>
    Task<object?> GetSnapShot(int version, string? name = null, CancellationToken cancellationToken = default);

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
