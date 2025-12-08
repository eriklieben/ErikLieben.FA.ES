using System.Text.Json;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing.Aggregates;
using ErikLieben.FA.ES.Testing.InMemory;

namespace ErikLieben.FA.ES.Testing.Assertions;

/// <summary>
/// Provides fluent assertion methods for verifying aggregate behavior in tests.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type that implements <see cref="IBase"/>.</typeparam>
public class AggregateAssertion<TAggregate> where TAggregate : IBase
{
    private readonly TAggregate _aggregate;
    private readonly TestContext _context;
    private readonly Exception? _caughtException;
    private readonly Dictionary<int, IEvent> _events;

    internal AggregateAssertion(
        TAggregate aggregate,
        string objectName,
        string objectId,
        TestContext context,
        Exception? caughtException)
    {
        _aggregate = aggregate;
        _context = context;
        _caughtException = caughtException;

        // Get events from context
        var key = InMemoryDataStore.GetStoreKey(objectName, objectId);
        if (_context.Events.TryGetValue(key, out var events))
        {
            _events = events;
        }
        else
        {
            _events = new Dictionary<int, IEvent>();
        }
    }

    /// <summary>
    /// Gets the aggregate instance being tested.
    /// </summary>
    public TAggregate Aggregate => _aggregate;

    /// <summary>
    /// Gets the test context.
    /// </summary>
    internal TestContext Context => _context;

    /// <summary>
    /// Asserts that an event of the specified type was appended to the stream.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the event was not found.</exception>
    public AggregateAssertion<TAggregate> ShouldHaveAppended<TEvent>()
    {
        if (_caughtException != null)
        {
            throw new TestAssertionException(
                $"Expected event {typeof(TEvent).Name} to be appended, but an exception was thrown: {_caughtException.Message}",
                _caughtException);
        }

        var eventName = EventNameResolver.GetEventName<TEvent>();
        var matchingEvent = _events.Values.FirstOrDefault(e => e.EventType == eventName);

        if (matchingEvent == null)
        {
            throw new TestAssertionException(
                $"Expected event '{eventName}' to be appended, but it was not found in the stream.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that no event of the specified type was appended to the stream.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the event was found.</exception>
    public AggregateAssertion<TAggregate> ShouldNotHaveAppended<TEvent>()
    {
        var eventName = EventNameResolver.GetEventName<TEvent>();
        var matchingEvent = _events.Values.FirstOrDefault(e => e.EventType == eventName);

        if (matchingEvent != null)
        {
            throw new TestAssertionException(
                $"Expected event '{eventName}' NOT to be appended, but it was found in the stream.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that a specific event was appended to the stream.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The expected event.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the event was not found.</exception>
    public AggregateAssertion<TAggregate> ShouldHaveAppended<TEvent>(TEvent @event)
    {
        if (_caughtException != null)
        {
            throw new TestAssertionException(
                $"Expected event {@event?.GetType().Name} to be appended, but an exception was thrown: {_caughtException.Message}",
                _caughtException);
        }

        var eventName = EventNameResolver.GetEventName<TEvent>();
        var expectedPayload = JsonSerializer.Serialize(@event);

        var matchingEvent = _events.Values.FirstOrDefault(e =>
            e.EventType == eventName && e.Payload == expectedPayload);

        if (matchingEvent == null)
        {
            throw new TestAssertionException(
                $"Expected event '{eventName}' with payload '{expectedPayload}' was not found in the stream.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that exactly the specified number of events were appended.
    /// </summary>
    /// <param name="count">The expected event count.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the count doesn't match.</exception>
    public AggregateAssertion<TAggregate> ShouldHaveAppendedCount(int count)
    {
        if (_caughtException != null)
        {
            throw new TestAssertionException(
                $"Expected {count} event(s) to be appended, but an exception was thrown: {_caughtException.Message}",
                _caughtException);
        }

        if (_events.Count != count)
        {
            throw new TestAssertionException(
                $"Expected {count} event(s) to be appended, but found {_events.Count}.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that at least the specified number of events were appended.
    /// </summary>
    /// <param name="count">The minimum expected event count.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when fewer events were appended.</exception>
    public AggregateAssertion<TAggregate> ShouldHaveAppendedAtLeast(int count)
    {
        if (_caughtException != null)
        {
            throw new TestAssertionException(
                $"Expected at least {count} event(s) to be appended, but an exception was thrown: {_caughtException.Message}",
                _caughtException);
        }

        if (_events.Count < count)
        {
            throw new TestAssertionException(
                $"Expected at least {count} event(s) to be appended, but found {_events.Count}.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that no events were appended to the stream.
    /// </summary>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when events were appended.</exception>
    public AggregateAssertion<TAggregate> ShouldNotHaveAppendedAnyEvents()
    {
        if (_events.Count > 0)
        {
            throw new TestAssertionException(
                $"Expected no events to be appended, but found {_events.Count} event(s).");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the stream contains an event matching the predicate.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="predicate">The predicate to match events.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when no matching event is found.</exception>
    public AggregateAssertion<TAggregate> ShouldContainEvent<TEvent>(Predicate<TEvent> predicate)
    {
        var eventName = EventNameResolver.GetEventName<TEvent>();
        var matchingEvents = _events.Values
            .Where(e => e.EventType == eventName && e.Payload != null)
            .Select(e => JsonSerializer.Deserialize<TEvent>(e.Payload!))
            .Where(e => e != null && predicate(e));

        if (!matchingEvents.Any())
        {
            throw new TestAssertionException(
                $"No event of type '{eventName}' matching the predicate was found.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the aggregate state satisfies the given assertion.
    /// </summary>
    /// <param name="assertion">The assertion to execute on the aggregate.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the assertion fails.</exception>
    public AggregateAssertion<TAggregate> ShouldHaveState(Action<TAggregate> assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);

        if (_caughtException != null)
        {
            throw new TestAssertionException(
                $"Cannot verify state because an exception was thrown: {_caughtException.Message}",
                _caughtException);
        }

        try
        {
            assertion(_aggregate);
        }
        catch (Exception ex)
        {
            throw new TestAssertionException(
                $"State assertion failed: {ex.Message}",
                ex);
        }

        return this;
    }

    /// <summary>
    /// Asserts that the aggregate state satisfies the given predicate.
    /// </summary>
    /// <param name="predicate">The predicate that the aggregate state must satisfy.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the predicate returns false.</exception>
    public AggregateAssertion<TAggregate> ShouldHaveState(Func<TAggregate, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (_caughtException != null)
        {
            throw new TestAssertionException(
                $"Cannot verify state because an exception was thrown: {_caughtException.Message}",
                _caughtException);
        }

        if (!predicate(_aggregate))
        {
            throw new TestAssertionException(
                "State predicate returned false. The aggregate state does not match expected conditions.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the command threw an exception of the specified type.
    /// Alias for <see cref="ShouldThrow{TException}"/> for API consistency.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <returns>The assertion instance for method chaining.</returns>
    public AggregateAssertion<TAggregate> ShouldHaveThrown<TException>() where TException : Exception
    {
        return ShouldThrow<TException>();
    }

    /// <summary>
    /// Asserts that a specific property of the aggregate has the expected value.
    /// </summary>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="propertySelector">The property selector.</param>
    /// <param name="expectedValue">The expected value.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the property value doesn't match.</exception>
    public AggregateAssertion<TAggregate> ShouldHaveProperty<TValue>(
        Func<TAggregate, TValue> propertySelector,
        TValue expectedValue)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        if (_caughtException != null)
        {
            throw new TestAssertionException(
                $"Cannot verify property because an exception was thrown: {_caughtException.Message}",
                _caughtException);
        }

        var actualValue = propertySelector(_aggregate);

        if (!EqualityComparer<TValue>.Default.Equals(actualValue, expectedValue))
        {
            throw new TestAssertionException(
                $"Expected property to be '{expectedValue}', but found '{actualValue}'.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the command threw an exception of the specified type.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when no exception or wrong exception type was thrown.</exception>
    public AggregateAssertion<TAggregate> ShouldThrow<TException>() where TException : Exception
    {
        if (_caughtException == null)
        {
            throw new TestAssertionException(
                $"Expected exception of type '{typeof(TException).Name}' to be thrown, but no exception was thrown.");
        }

        if (_caughtException.GetType() != typeof(TException))
        {
            throw new TestAssertionException(
                $"Expected exception of type '{typeof(TException).Name}', but got '{_caughtException.GetType().Name}'.",
                _caughtException);
        }

        return this;
    }

    /// <summary>
    /// Asserts that the command did not throw any exception.
    /// </summary>
    /// <returns>The assertion instance for method chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when an exception was thrown.</exception>
    public AggregateAssertion<TAggregate> ShouldNotThrow()
    {
        if (_caughtException != null)
        {
            throw new TestAssertionException(
                $"Expected no exception to be thrown, but got '{_caughtException.GetType().Name}': {_caughtException.Message}",
                _caughtException);
        }

        return this;
    }

    /// <summary>
    /// Asserts that the aggregate state matches the stored snapshot.
    /// </summary>
    /// <param name="snapshotName">The name of the snapshot.</param>
    /// <param name="options">Optional snapshot options.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    public AggregateAssertion<TAggregate> ShouldMatchSnapshot(
        string snapshotName,
        SnapshotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshotName);

        SnapshotAssertion.MatchesSnapshot(_aggregate, snapshotName, options);
        return this;
    }

    /// <summary>
    /// Asserts that a selected portion of the aggregate state matches the stored snapshot.
    /// </summary>
    /// <param name="snapshotName">The name of the snapshot.</param>
    /// <param name="stateSelector">A function to select the portion of state to snapshot.</param>
    /// <param name="options">Optional snapshot options.</param>
    /// <returns>The assertion instance for method chaining.</returns>
    public AggregateAssertion<TAggregate> ShouldMatchSnapshot(
        string snapshotName,
        Func<TAggregate, object> stateSelector,
        SnapshotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshotName);
        ArgumentNullException.ThrowIfNull(stateSelector);

        var selectedState = stateSelector(_aggregate);
        SnapshotAssertion.MatchesSnapshot(selectedState, snapshotName, options);
        return this;
    }
}

/// <summary>
/// Helper class for resolving event names from types (AOT-friendly when using known types).
/// </summary>
internal static class EventNameResolver
{
    /// <summary>
    /// Gets the event name from the EventNameAttribute on the type.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <returns>The event name.</returns>
    /// <exception cref="TestAssertionException">Thrown when EventNameAttribute is not found.</exception>
    public static string GetEventName<TEvent>()
    {
        var eventNameAttribute = typeof(TEvent).GetCustomAttributes(typeof(EventNameAttribute), false)
            .FirstOrDefault() as EventNameAttribute;

        if (eventNameAttribute == null)
        {
            throw new TestAssertionException($"EventNameAttribute is not found on {typeof(TEvent).Name}");
        }

        return eventNameAttribute.Name;
    }
}
