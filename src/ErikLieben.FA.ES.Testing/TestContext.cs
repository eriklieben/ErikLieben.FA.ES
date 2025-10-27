using System.Text.Json;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Testing.InMemory;

namespace ErikLieben.FA.ES.Testing;

/// <summary>
/// Represents an assertion failure in test context verification.
/// </summary>
public class TestAssertionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestAssertionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the assertion failure.</param>
    public TestAssertionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAssertionException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the assertion failure.</param>
    /// <param name="innerException">The exception that caused this assertion failure.</param>
    public TestAssertionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Provides a lightweight testing context that exposes factories and captured in-memory events for assertions.
/// </summary>
/// <param name="documentFactory">The object document factory used to create or retrieve documents under test.</param>
/// <param name="eventStreamFactory">The event stream factory used to create streams for folding and appending events.</param>
/// <param name="dataStore">The in-memory data store that captures appended events for verification.</param>
public class TestContext(
    IObjectDocumentFactory documentFactory,
    IEventStreamFactory eventStreamFactory,
    InMemoryDataStore dataStore)
{
    /// <summary>
    /// Gets the captured events grouped by stream identifier and version from the in-memory data store.
    /// </summary>
    public Dictionary<string, Dictionary<int, IEvent>> Events => dataStore.Store;

    /// <summary>
    /// Gets assertion helpers for verifying events for objects in the test context.
    /// </summary>
    public AssertionExtension Assert => new(this);

    /// <summary>
    /// Gets the event stream factory used to create streams for reading and appending events in tests.
    /// </summary>
    public IEventStreamFactory EventStreamFactory => eventStreamFactory;
    /// <summary>
    /// Gets the object document factory used to create or retrieve documents under test.
    /// </summary>
    public IObjectDocumentFactory DocumentFactory => documentFactory;

    /// <summary>
/// Retrieves or creates the object document and returns its event stream for testing.
/// </summary>
/// <param name="objectName">The object name (scope) used for the document.</param>
/// <param name="objectId">The identifier of the object.</param>
/// <returns>The event stream associated with the requested object.</returns>
public async Task<IEventStream> GetEventStreamFor(string objectName, string objectId)
    {
        var document = await documentFactory.GetOrCreateAsync(objectName, objectId);
        return eventStreamFactory.Create(document);
    }
}

/// <summary>
/// Provides assertion helpers for verifying that specific objects and events exist in the in-memory test store.
/// </summary>
/// <param name="context">The active test context containing factories and stored events.</param>
public class AssertionExtension(TestContext context)
{
    /// <summary>
/// Asserts that an object with the specified name and identifier exists in the in-memory event store.
/// </summary>
/// <param name="objectName">The logical name/scope of the object.</param>
/// <param name="objectId">The identifier of the object.</param>
/// <returns>An <see cref="EventAssertionExtension"/> to chain event-level assertions.</returns>
/// <exception cref="TestAssertionException">Thrown when the object is not found in the in-memory store.</exception>
public EventAssertionExtension ShouldHaveObject(string objectName, string objectId)
    {
        var key = InMemoryDataStore.GetStoreKey(objectName, objectId);
        var events = context.Events[key];
        if (events == null)
        {
            throw new TestAssertionException($"Object {key} does not exist.");
        }
        return new EventAssertionExtension(events!);
    }
}


/// <summary>
/// Provides fluent assertions for verifying event counts, types, and payloads in a specific in-memory stream.
/// </summary>
/// <param name="events">The indexed collection of events for a single stream.</param>
public class EventAssertionExtension(Dictionary<int, IEvent> events)
{
    /// <summary>
/// Asserts that the number of events equals the expected count.
/// </summary>
/// <param name="expectedCount">The expected number of events.</param>
/// <returns>The same <see cref="EventAssertionExtension"/> to allow chaining.</returns>
/// <exception cref="TestAssertionException">Thrown when the actual count differs from the expected count.</exception>
public EventAssertionExtension WithEventCount(int expectedCount)
    {
        if (events.Count != expectedCount)
        {
            throw new TestAssertionException($"Expected count is {expectedCount} but actual count is {events.Count}.");
        }

        return this;
    }

    /// <summary>
/// Asserts that the last event has the expected event type and payload.
/// </summary>
/// <typeparam name="TEvent">The event payload type to compare by JSON serialization.</typeparam>
/// <param name="expected">The expected event payload.</param>
/// <returns>The same <see cref="EventAssertionExtension"/> to allow chaining.</returns>
/// <exception cref="TestAssertionException">Thrown when the event type or payload does not match.</exception>
public EventAssertionExtension WithEventAtLastPosition<TEvent>(TEvent expected)
    {
        // Get event name
        var eventNameAttribute = typeof(TEvent).GetCustomAttributes(typeof(EventNameAttribute), false)
            .FirstOrDefault() as EventNameAttribute;
        if (eventNameAttribute == null)
        {
            throw new TestAssertionException($"EventNameAttribute is not found on {typeof(TEvent).Name}");
        }
        var eventName = eventNameAttribute.GetType().GetConstructors()
            .FirstOrDefault()?
            .GetParameters()
            .FirstOrDefault()?.ParameterType == typeof(string)
            ? eventNameAttribute.GetType().GetProperty("Name")?.GetValue(eventNameAttribute)?.ToString()
            : throw new TestAssertionException("The first constructor parameter is not a string.");

        var eventType = events[events.Keys.Max()].EventType;
        if (eventName != eventType)
        {
            throw new TestAssertionException($"Type '{eventType}' of event is different from expected ('{eventName}').");
        }

        var payload = JsonSerializer.Serialize(expected);
        var payloadCurrent = events[events.Keys.Max()].Payload;
        if (payload != payloadCurrent)
        {
            throw new TestAssertionException($"Expected payload '{payload}' is different from payload at position {events.Keys.Max()} (last) ('{payloadCurrent}').");
        }

        return this;
    }

    /// <summary>
/// Asserts that exactly one event exists and its type and payload match the expected event.
/// </summary>
/// <typeparam name="TEvent">The event payload type to compare by JSON serialization.</typeparam>
/// <param name="expected">The expected event payload.</param>
/// <returns>The same <see cref="EventAssertionExtension"/> to allow chaining.</returns>
/// <exception cref="TestAssertionException">Thrown when the count, event type, or payload does not match.</exception>
public EventAssertionExtension WithSingleEvent<TEvent>(TEvent expected)
    {
        // Get event name
        var eventNameAttribute = typeof(TEvent).GetCustomAttributes(typeof(EventNameAttribute), false)
            .FirstOrDefault() as EventNameAttribute;
        if (eventNameAttribute == null)
        {
            throw new TestAssertionException($"EventNameAttribute is not found on {typeof(TEvent).Name}");
        }
        var eventName = eventNameAttribute.GetType().GetConstructors()
            .FirstOrDefault()?
            .GetParameters()
            .FirstOrDefault()?.ParameterType == typeof(string)
            ? eventNameAttribute.GetType().GetProperty("Name")?.GetValue(eventNameAttribute)?.ToString()
            : throw new TestAssertionException("The first constructor parameter is not a string.");

        if (events.Count != 1)
        {
            throw new TestAssertionException($"Expected count is 1 but actual count is {events.Count}.");
        }

        var eventType = events[0].EventType;
        if (eventName != eventType)
        {
            throw new TestAssertionException($"Type '{eventType}' of event is different from expected ('{eventName}').");
        }

        var payload = JsonSerializer.Serialize(expected);
        var payloadCurrent = events[0].Payload;
        if (payload != payloadCurrent)
        {
            throw new TestAssertionException($"Expected payload '{payload}' is different from payload ('{payloadCurrent}').");
        }

        return this;
    }

    /// <summary>
/// Asserts that the event at the specified position has the expected type and payload.
/// </summary>
/// <typeparam name="TEvent">The event payload type to compare by JSON serialization.</typeparam>
/// <param name="position">The zero-based position of the event to verify.</param>
/// <param name="expected">The expected event payload.</param>
/// <returns>The same <see cref="EventAssertionExtension"/> to allow chaining.</returns>
/// <exception cref="TestAssertionException">Thrown when the position is out of range or when the event type/payload does not match.</exception>
public EventAssertionExtension WithEventAtPosition<TEvent>(int position, TEvent expected)
    {
        if (position < 0 || position >= events.Count)
        {
            throw new TestAssertionException("Position is out of range");
        }

        // Get event name
        var eventNameAttribute = typeof(TEvent).GetCustomAttributes(typeof(EventNameAttribute), false)
            .FirstOrDefault() as EventNameAttribute;
        if (eventNameAttribute == null)
        {
            throw new TestAssertionException($"EventNameAttribute is not found on {typeof(TEvent).Name}");
        }
        var eventName = eventNameAttribute.GetType().GetConstructors()
            .FirstOrDefault()?
            .GetParameters()
            .FirstOrDefault()?.ParameterType == typeof(string)
            ? eventNameAttribute.GetType().GetProperty("Name")?.GetValue(eventNameAttribute)?.ToString()
            : throw new TestAssertionException("The first constructor parameter is not a string.");

        if (eventName != events[position].EventType)
        {
            throw new TestAssertionException($"Type '{events[position].EventType}' of event is different from expected ('{eventName}').");
        }

        var payload = JsonSerializer.Serialize(expected);
        if (payload != events[position].Payload)
        {
            throw new TestAssertionException($"Expected payload '{payload}' is different from payload at position {position} ('{events[position].Payload}').");
        }

        return this;
    }
}
