using System.Collections.Frozen;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Contains information about a registered event type.
/// </summary>
/// <param name="Type">The CLR type of the event.</param>
/// <param name="EventName">The name used to identify the event in storage.</param>
/// <param name="SchemaVersion">The schema version of the event. Defaults to 1.</param>
/// <param name="JsonTypeInfo">The JSON type information for serialization/deserialization.</param>
public record EventTypeInfo(Type Type, string EventName, int SchemaVersion, JsonTypeInfo JsonTypeInfo);

/// <summary>
/// Represents a key for looking up event types by name and schema version.
/// </summary>
/// <param name="EventName">The name used to identify the event in storage.</param>
/// <param name="SchemaVersion">The schema version of the event.</param>
public readonly record struct EventTypeKey(string EventName, int SchemaVersion);

/// <summary>
/// Registry for managing event type mappings between CLR types, event names, and JSON type information.
/// Supports freezing to immutable collections for optimized read performance.
/// </summary>
public class EventTypeRegistry
{
    private readonly List<EventTypeInfo?> events = [];

    // Mutable dictionaries used during registration phase
    private Dictionary<Type, EventTypeInfo?>? byTypeMutable = new();
    private Dictionary<string, EventTypeInfo?>? byNameMutable = new();
    private Dictionary<EventTypeKey, EventTypeInfo?>? byNameAndVersionMutable = new();
    private Dictionary<JsonTypeInfo, EventTypeInfo?>? byJsonTypeInfoMutable = new();

    // Frozen dictionaries used after freeze for optimized lookups
    private FrozenDictionary<Type, EventTypeInfo?>? byTypeFrozen;
    private FrozenDictionary<string, EventTypeInfo?>? byNameFrozen;
    private FrozenDictionary<EventTypeKey, EventTypeInfo?>? byNameAndVersionFrozen;
    private FrozenDictionary<JsonTypeInfo, EventTypeInfo?>? byJsonTypeInfoFrozen;

    private bool isFrozen = false;

    /// <summary>
    /// Registers an event type with its associated metadata and schema version 1.
    /// </summary>
    /// <param name="type">The CLR type of the event.</param>
    /// <param name="eventName">The name used to identify the event in storage.</param>
    /// <param name="jsonTypeInfo">The JSON type information for serialization/deserialization.</param>
    /// <exception cref="InvalidOperationException">Thrown when attempting to add to a frozen registry.</exception>
    public void Add(Type type, string eventName, JsonTypeInfo jsonTypeInfo)
        => Add(type, eventName, 1, jsonTypeInfo);

    /// <summary>
    /// Registers an event type with its associated metadata and schema version.
    /// </summary>
    /// <param name="type">The CLR type of the event.</param>
    /// <param name="eventName">The name used to identify the event in storage.</param>
    /// <param name="schemaVersion">The schema version of the event.</param>
    /// <param name="jsonTypeInfo">The JSON type information for serialization/deserialization.</param>
    /// <exception cref="InvalidOperationException">Thrown when attempting to add to a frozen registry.</exception>
    public void Add(Type type, string eventName, int schemaVersion, JsonTypeInfo jsonTypeInfo)
    {
        if (isFrozen)
        {
            throw new InvalidOperationException("Cannot add event types to a frozen registry. Call Add before calling Freeze().");
        }

        var info = new EventTypeInfo(type, eventName, schemaVersion, jsonTypeInfo);
        events.Add(info);
        byTypeMutable![type] = info;
        byNameMutable![eventName] = info;
        byNameAndVersionMutable![new EventTypeKey(eventName, schemaVersion)] = info;
        byJsonTypeInfoMutable![jsonTypeInfo] = info;
    }

    /// <summary>
    /// Freezes the registry to use optimized frozen collections for lookups.
    /// After freezing, no more event types can be added.
    /// This provides ~50% faster lookups compared to regular dictionaries.
    /// </summary>
    public void Freeze()
    {
        if (isFrozen)
        {
            return;
        }

        byTypeFrozen = byTypeMutable!.ToFrozenDictionary();
        byNameFrozen = byNameMutable!.ToFrozenDictionary();
        byNameAndVersionFrozen = byNameAndVersionMutable!.ToFrozenDictionary();
        byJsonTypeInfoFrozen = byJsonTypeInfoMutable!.ToFrozenDictionary();

        // Release mutable dictionaries to free memory
        byTypeMutable = null;
        byNameMutable = null;
        byNameAndVersionMutable = null;
        byJsonTypeInfoMutable = null;

        isFrozen = true;
    }

    /// <summary>
    /// Gets event type information by CLR type.
    /// </summary>
    /// <param name="type">The CLR type to look up.</param>
    /// <returns>The event type information.</returns>
    public EventTypeInfo? GetByType(Type type) =>
        isFrozen ? byTypeFrozen![type] : byTypeMutable![type];

    /// <summary>
    /// Gets event type information by event name.
    /// </summary>
    /// <param name="eventName">The event name to look up.</param>
    /// <returns>The event type information.</returns>
    public EventTypeInfo? GetByName(string eventName) =>
        isFrozen ? byNameFrozen![eventName] : byNameMutable![eventName];

    /// <summary>
    /// Gets event type information by JSON type information.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type information to look up.</param>
    /// <returns>The event type information.</returns>
    public EventTypeInfo? GetByJsonTypeInfo(JsonTypeInfo jsonTypeInfo) =>
        isFrozen ? byJsonTypeInfoFrozen![jsonTypeInfo] : byJsonTypeInfoMutable![jsonTypeInfo];

    /// <summary>
    /// Tries to get event type information by CLR type.
    /// </summary>
    /// <param name="type">The CLR type to look up.</param>
    /// <param name="info">When this method returns, contains the event type information if found; otherwise, null.</param>
    /// <returns>True if the type was found; otherwise, false.</returns>
    public bool TryGetByType(Type type, out EventTypeInfo? info) =>
        isFrozen ? byTypeFrozen!.TryGetValue(type, out info) : byTypeMutable!.TryGetValue(type, out info);

    /// <summary>
    /// Tries to get event type information by event name.
    /// </summary>
    /// <param name="eventName">The event name to look up.</param>
    /// <param name="info">When this method returns, contains the event type information if found; otherwise, null.</param>
    /// <returns>True if the event name was found; otherwise, false.</returns>
    public bool TryGetByName(string eventName, out EventTypeInfo? info) =>
        isFrozen ? byNameFrozen!.TryGetValue(eventName, out info) : byNameMutable!.TryGetValue(eventName, out info);

    /// <summary>
    /// Gets event type information by event name and schema version.
    /// </summary>
    /// <param name="eventName">The event name to look up.</param>
    /// <param name="schemaVersion">The schema version to look up.</param>
    /// <returns>The event type information.</returns>
    public EventTypeInfo? GetByNameAndVersion(string eventName, int schemaVersion)
    {
        var key = new EventTypeKey(eventName, schemaVersion);
        return isFrozen ? byNameAndVersionFrozen![key] : byNameAndVersionMutable![key];
    }

    /// <summary>
    /// Tries to get event type information by event name and schema version.
    /// </summary>
    /// <param name="eventName">The event name to look up.</param>
    /// <param name="schemaVersion">The schema version to look up.</param>
    /// <param name="info">When this method returns, contains the event type information if found; otherwise, null.</param>
    /// <returns>True if the event was found; otherwise, false.</returns>
    public bool TryGetByNameAndVersion(string eventName, int schemaVersion, out EventTypeInfo? info)
    {
        var key = new EventTypeKey(eventName, schemaVersion);
        return isFrozen
            ? byNameAndVersionFrozen!.TryGetValue(key, out info)
            : byNameAndVersionMutable!.TryGetValue(key, out info);
    }

    /// <summary>
    /// Tries to get event type information by JSON type information.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type information to look up.</param>
    /// <param name="info">When this method returns, contains the event type information if found; otherwise, null.</param>
    /// <returns>True if the JSON type information was found; otherwise, false.</returns>
    public bool TryGetByJsonTypeInfo(JsonTypeInfo jsonTypeInfo, out EventTypeInfo? info) =>
        isFrozen ? byJsonTypeInfoFrozen!.TryGetValue(jsonTypeInfo, out info) : byJsonTypeInfoMutable!.TryGetValue(jsonTypeInfo, out info);

    /// <summary>
    /// Gets all registered event type information.
    /// </summary>
    public IEnumerable<EventTypeInfo?> All => events;
}
