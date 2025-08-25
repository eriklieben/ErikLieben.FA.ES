using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES.EventStream;

public record EventTypeInfo(Type Type, string EventName, JsonTypeInfo JsonTypeInfo);

public class EventTypeRegistry
{
    private readonly List<EventTypeInfo?> events = [];
    private readonly Dictionary<Type, EventTypeInfo?> byType = new();
    private readonly Dictionary<string, EventTypeInfo?> byName = new();
    private readonly Dictionary<JsonTypeInfo, EventTypeInfo?> byJsonTypeInfo = new();

    public void Add(Type type, string eventName, JsonTypeInfo jsonTypeInfo)
    {
        var info = new EventTypeInfo(type, eventName, jsonTypeInfo);
        events.Add(info);
        byType[type] = info;
        byName[eventName] = info;
        byJsonTypeInfo[jsonTypeInfo] = info;
    }

    public EventTypeInfo? GetByType(Type type) => byType[type];
    public EventTypeInfo? GetByName(string eventName) => byName[eventName];
    public EventTypeInfo? GetByJsonTypeInfo(JsonTypeInfo jsonTypeInfo) => byJsonTypeInfo[jsonTypeInfo];

    public bool TryGetByType(Type type, out EventTypeInfo? info) => byType.TryGetValue(type, out info);
    public bool TryGetByName(string eventName, out EventTypeInfo? info) => byName.TryGetValue(eventName, out info);
    public bool TryGetByJsonTypeInfo(JsonTypeInfo jsonTypeInfo, out EventTypeInfo? info) => byJsonTypeInfo.TryGetValue(jsonTypeInfo, out info);

    public IEnumerable<EventTypeInfo?> All => events;
}
