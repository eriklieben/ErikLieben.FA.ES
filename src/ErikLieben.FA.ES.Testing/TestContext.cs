using System.Text.Json;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Testing.InMemory;

namespace ErikLieben.FA.ES.Testing;

public class TestContext(
    IObjectDocumentFactory documentFactory,
    IEventStreamFactory eventStreamFactory,
    InMemoryDataStore dataStore)
{
    public Dictionary<string, Dictionary<int, IEvent>> Events => dataStore.Store;

    public AssertionExtension Assert => new(this);

    public IEventStreamFactory EventStreamFactory => eventStreamFactory;
    public IObjectDocumentFactory DocumentFactory => documentFactory;

    public async Task<IEventStream> GetEventStreamFor(string objectName, string objectId)
    {
        var document = await documentFactory.GetOrCreateAsync(objectName, objectId);
        return eventStreamFactory.Create(document);
    }
}

public class AssertionExtension(TestContext context)
{
    public EventAssertionExtension ShouldHaveObject(string objectName, string objectId)
    {
        var key = InMemoryDataStore.GetStoreKey(objectName, objectId);
        var events = context.Events[key];
        if (events == null)
        {
            throw new Exception($"Object {key} does not exist.");
        }
        return new EventAssertionExtension(events!);
    }
}


public class EventAssertionExtension(Dictionary<int, IEvent> events)
{
    public EventAssertionExtension WithEventCount(int expectedCount)
    {
        if (events.Count != expectedCount)
        {
            throw new Exception($"Expected count is {expectedCount} but actual count is {events.Count}.");
        }

        return this;
    }

    public EventAssertionExtension WithEventAtLastPosition<TEvent>(TEvent expected)
    {
        // Get event name
        var eventNameAttribute = typeof(TEvent).GetCustomAttributes(typeof(EventNameAttribute), false)
            .FirstOrDefault() as EventNameAttribute;
        if (eventNameAttribute == null)
        {
            throw new Exception($"EventNameAttribute is not found on {typeof(TEvent).Name}");
        }
        var eventName = eventNameAttribute.GetType().GetConstructors()
            .FirstOrDefault()?
            .GetParameters()
            .FirstOrDefault()?.ParameterType == typeof(string)
            ? eventNameAttribute.GetType().GetProperty("Name")?.GetValue(eventNameAttribute)?.ToString()
            : throw new Exception("The first constructor parameter is not a string.");

        var eventType = events[events.Keys.Max()].EventType;
        if (eventName != eventType)
        {
            throw new Exception($"Type '{eventType}' of event is different from expected ('{eventName}').");
        }

        var payload = JsonSerializer.Serialize(expected);
        var payloadCurrent = events[events.Keys.Max()].Payload;
        if (payload != payloadCurrent)
        {
            throw new Exception($"Expected payload '{payload}' is different from payload at position {events.Keys.Max()} (last) ('{payloadCurrent}').");
        }

        return this;
    }

    public EventAssertionExtension WithSingleEvent<TEvent>(TEvent expected)
    {
        // Get event name
        var eventNameAttribute = typeof(TEvent).GetCustomAttributes(typeof(EventNameAttribute), false)
            .FirstOrDefault() as EventNameAttribute;
        if (eventNameAttribute == null)
        {
            throw new Exception($"EventNameAttribute is not found on {typeof(TEvent).Name}");
        }
        var eventName = eventNameAttribute.GetType().GetConstructors()
            .FirstOrDefault()?
            .GetParameters()
            .FirstOrDefault()?.ParameterType == typeof(string)
            ? eventNameAttribute.GetType().GetProperty("Name")?.GetValue(eventNameAttribute)?.ToString()
            : throw new Exception("The first constructor parameter is not a string.");

        if (events.Count != 1)
        {
            throw new Exception($"Expected count is 1 but actual count is {events.Count}.");
        }

        var eventType = events[0].EventType;
        if (eventName != eventType)
        {
            throw new Exception($"Type '{eventType}' of event is different from expected ('{eventName}').");
        }

        var payload = JsonSerializer.Serialize(expected);
        var payloadCurrent = events[0].Payload;
        if (payload != payloadCurrent)
        {
            throw new Exception($"Expected payload '{payload}' is different from payload ('{payloadCurrent}').");
        }

        return this;
    }

    public EventAssertionExtension WithEventAtPosition<TEvent>(int position, TEvent expected)
    {
        if (position < 0 || position >= events.Count)
        {
            throw new Exception("Position is out of range");
        }

        // Get event name
        var eventNameAttribute = typeof(TEvent).GetCustomAttributes(typeof(EventNameAttribute), false)
            .FirstOrDefault() as EventNameAttribute;
        if (eventNameAttribute == null)
        {
            throw new Exception($"EventNameAttribute is not found on {typeof(TEvent).Name}");
        }
        var eventName = eventNameAttribute.GetType().GetConstructors()
            .FirstOrDefault()?
            .GetParameters()
            .FirstOrDefault()?.ParameterType == typeof(string)
            ? eventNameAttribute.GetType().GetProperty("Name")?.GetValue(eventNameAttribute)?.ToString()
            : throw new Exception("The first constructor parameter is not a string.");

        if (eventName != events[position].EventType)
        {
            throw new Exception($"Type '{events[position].EventType}' of event is different from expected ('{eventName}').");
        }

        var payload = JsonSerializer.Serialize(expected);
        if (payload != events[position].Payload)
        {
            throw new Exception($"Expected payload '{payload}' is different from payload at position {position} ('{events[position].Payload}').");
        }

        return this;
    }
}
