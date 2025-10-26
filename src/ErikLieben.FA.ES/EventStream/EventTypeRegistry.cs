using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Contains information about a registered event type.
/// </summary>
/// <param name="Type">The CLR type of the event.</param>
/// <param name="EventName">The name used to identify the event in storage.</param>
/// <param name="JsonTypeInfo">The JSON type information for serialization/deserialization.</param>
public record EventTypeInfo(Type Type, string EventName, JsonTypeInfo JsonTypeInfo);

/// <summary>
/// Registry for managing event type mappings between CLR types, event names, and JSON type information.
/// </summary>
public class EventTypeRegistry
{
    private readonly List<EventTypeInfo?> events = [];
    private readonly Dictionary<Type, EventTypeInfo?> byType = new();
    private readonly Dictionary<string, EventTypeInfo?> byName = new();
    private readonly Dictionary<JsonTypeInfo, EventTypeInfo?> byJsonTypeInfo = new();

    /// <summary>
    /// Registers an event type with its associated metadata.
    /// </summary>
    /// <param name="type">The CLR type of the event.</param>
    /// <param name="eventName">The name used to identify the event in storage.</param>
    /// <param name="jsonTypeInfo">The JSON type information for serialization/deserialization.</param>
    public void Add(Type type, string eventName, JsonTypeInfo jsonTypeInfo)
    {
        var info = new EventTypeInfo(type, eventName, jsonTypeInfo);
        events.Add(info);
        byType[type] = info;
        byName[eventName] = info;
        byJsonTypeInfo[jsonTypeInfo] = info;
    }

    /// <summary>
    /// Gets event type information by CLR type.
    /// </summary>
    /// <param name="type">The CLR type to look up.</param>
    /// <returns>The event type information.</returns>
    public EventTypeInfo? GetByType(Type type) => byType[type];

    /// <summary>
    /// Gets event type information by event name.
    /// </summary>
    /// <param name="eventName">The event name to look up.</param>
    /// <returns>The event type information.</returns>
    public EventTypeInfo? GetByName(string eventName) => byName[eventName];

    /// <summary>
    /// Gets event type information by JSON type information.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type information to look up.</param>
    /// <returns>The event type information.</returns>
    public EventTypeInfo? GetByJsonTypeInfo(JsonTypeInfo jsonTypeInfo) => byJsonTypeInfo[jsonTypeInfo];

    /// <summary>
    /// Tries to get event type information by CLR type.
    /// </summary>
    /// <param name="type">The CLR type to look up.</param>
    /// <param name="info">When this method returns, contains the event type information if found; otherwise, null.</param>
    /// <returns>True if the type was found; otherwise, false.</returns>
    public bool TryGetByType(Type type, out EventTypeInfo? info) => byType.TryGetValue(type, out info);

    /// <summary>
    /// Tries to get event type information by event name.
    /// </summary>
    /// <param name="eventName">The event name to look up.</param>
    /// <param name="info">When this method returns, contains the event type information if found; otherwise, null.</param>
    /// <returns>True if the event name was found; otherwise, false.</returns>
    public bool TryGetByName(string eventName, out EventTypeInfo? info) => byName.TryGetValue(eventName, out info);

    /// <summary>
    /// Tries to get event type information by JSON type information.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type information to look up.</param>
    /// <param name="info">When this method returns, contains the event type information if found; otherwise, null.</param>
    /// <returns>True if the JSON type information was found; otherwise, false.</returns>
    public bool TryGetByJsonTypeInfo(JsonTypeInfo jsonTypeInfo, out EventTypeInfo? info) => byJsonTypeInfo.TryGetValue(jsonTypeInfo, out info);

    /// <summary>
    /// Gets all registered event type information.
    /// </summary>
    public IEnumerable<EventTypeInfo?> All => events;
}
