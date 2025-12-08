using System.Text.Json;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing.Assertions;
using ErikLieben.FA.ES.Testing.InMemory;

namespace ErikLieben.FA.ES.Testing.Builders;

/// <summary>
/// Extension methods for fluent chaining on async builder operations.
/// </summary>
public static class AggregateTestBuilderExtensions
{
    /// <summary>
    /// Executes assertions on the test result using a fluent lambda syntax.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="builderTask">The awaitable builder from the When method.</param>
    /// <param name="assertions">The assertion actions to execute on the result.</param>
    public static async Task Then<TAggregate>(
        this Task<AggregateTestBuilder<TAggregate>> builderTask,
        Action<AggregateAssertion<TAggregate>> assertions)
        where TAggregate : IBase
    {
        ArgumentNullException.ThrowIfNull(builderTask);
        ArgumentNullException.ThrowIfNull(assertions);

        var builder = await builderTask;
        var assertion = await builder.Then();
        assertions(assertion);
    }
}

/// <summary>
/// Provides a fluent Given-When-Then API for testing aggregates in an event-sourced system.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type that implements <see cref="IBase"/>.</typeparam>
public class AggregateTestBuilder<TAggregate> where TAggregate : IBase
{
    private readonly string _objectName;
    private readonly string _objectId;
    private readonly TestContext _context;
    private readonly Func<IEventStream, TAggregate> _aggregateFactory;
    private readonly List<IEvent> _givenEvents = new();
    private object? _givenSnapshot;
    private TAggregate? _aggregate;
    private Exception? _caughtException;
    private bool _hasExecuted;

    private AggregateTestBuilder(
        string objectName,
        string objectId,
        TestContext context,
        Func<IEventStream, TAggregate> aggregateFactory)
    {
        _objectName = objectName;
        _objectId = objectId;
        _context = context;
        _aggregateFactory = aggregateFactory;
    }

    /// <summary>
    /// Gets the test context (internal for time manipulation extensions).
    /// </summary>
    internal TestContext Context => _context;

    /// <summary>
    /// Creates a new test builder for the specified aggregate using explicit object name (for non-ITestableAggregate types).
    /// </summary>
    /// <param name="objectName">The logical name/scope of the aggregate.</param>
    /// <param name="objectId">The identifier of the aggregate instance.</param>
    /// <param name="context">The test context providing in-memory infrastructure.</param>
    /// <param name="aggregateFactory">Factory function to create the aggregate from an event stream (AOT-friendly).</param>
    /// <returns>A new <see cref="AggregateTestBuilder{TAggregate}"/> instance.</returns>
    public static AggregateTestBuilder<TAggregate> For(
        string objectName,
        string objectId,
        TestContext context,
        Func<IEventStream, TAggregate> aggregateFactory)
    {
        ArgumentNullException.ThrowIfNull(objectName);
        ArgumentNullException.ThrowIfNull(objectId);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(aggregateFactory);

        return new AggregateTestBuilder<TAggregate>(objectName, objectId, context, aggregateFactory);
    }

    /// <summary>
    /// Sets up the initial state with a sequence of events.
    /// </summary>
    /// <param name="events">The events representing the initial state.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public AggregateTestBuilder<TAggregate> Given(params IEvent[] events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _givenEvents.AddRange(events);
        return this;
    }

    /// <summary>
    /// Sets up the initial state with domain event objects (records/classes with EventNameAttribute).
    /// This overload automatically wraps domain events as JsonEvent instances.
    /// </summary>
    /// <param name="domainEvents">The domain event objects representing the initial state.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a domain event doesn't have EventNameAttribute.</exception>
    public AggregateTestBuilder<TAggregate> Given(params object[] domainEvents)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        var version = _givenEvents.Count;
        foreach (var domainEvent in domainEvents)
        {
            var jsonEvent = WrapDomainEvent(domainEvent, ++version);
            _givenEvents.Add(jsonEvent);
        }

        return this;
    }

    /// <summary>
    /// Wraps a domain event object as a JsonEvent.
    /// </summary>
    private static JsonEvent WrapDomainEvent(object domainEvent, int version)
    {
        var eventType = domainEvent.GetType();
        var eventNameAttribute = eventType.GetCustomAttributes(typeof(EventNameAttribute), false)
            .FirstOrDefault() as EventNameAttribute;

        if (eventNameAttribute == null)
        {
            throw new InvalidOperationException(
                $"Domain event '{eventType.Name}' must have an [EventName] attribute. " +
                $"Add [EventName(\"{eventType.Name}\")] to the event class.");
        }

        var payload = JsonSerializer.Serialize(domainEvent, eventType);

        return new JsonEvent
        {
            EventType = eventNameAttribute.Name,
            EventVersion = version,
            Payload = payload
        };
    }

    /// <summary>
    /// Sets up the initial state with a collection of events.
    /// </summary>
    /// <param name="events">The events representing the initial state.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public AggregateTestBuilder<TAggregate> GivenEvents(IEnumerable<IEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _givenEvents.AddRange(events);
        return this;
    }

    /// <summary>
    /// Explicitly declares that the aggregate has no prior events (for clarity in tests).
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public AggregateTestBuilder<TAggregate> GivenNoPriorEvents()
    {
        return this;
    }

    /// <summary>
    /// Sets up the initial state with a snapshot.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of the snapshot.</typeparam>
    /// <param name="snapshot">The snapshot object.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public AggregateTestBuilder<TAggregate> GivenSnapshot<TSnapshot>(TSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _givenSnapshot = snapshot;
        return this;
    }

    /// <summary>
    /// Executes a synchronous command on the aggregate.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public async Task<AggregateTestBuilder<TAggregate>> When(Action<TAggregate> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        await SetupAggregate();
        _hasExecuted = true;

        try
        {
            command(_aggregate!);
        }
        catch (Exception ex)
        {
            _caughtException = ex;
        }

        return this;
    }

    /// <summary>
    /// Executes an asynchronous command on the aggregate.
    /// </summary>
    /// <param name="command">The asynchronous command to execute.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public async Task<AggregateTestBuilder<TAggregate>> When(Func<TAggregate, Task> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        await SetupAggregate();
        _hasExecuted = true;

        try
        {
            await command(_aggregate!);
        }
        catch (Exception ex)
        {
            _caughtException = ex;
        }

        return this;
    }

    /// <summary>
    /// Returns an assertion helper to verify the outcome.
    /// </summary>
    /// <returns>An <see cref="AggregateAssertion{TAggregate}"/> for chaining assertions.</returns>
    public async Task<AggregateAssertion<TAggregate>> Then()
    {
        if (!_hasExecuted)
        {
            // If When was not called, just setup the aggregate with given events
            await SetupAggregate();
        }

        return new AggregateAssertion<TAggregate>(
            _aggregate!,
            _objectName,
            _objectId,
            _context,
            _caughtException);
    }

    /// <summary>
    /// Returns the aggregate instance for direct inspection.
    /// </summary>
    /// <returns>The aggregate instance.</returns>
    public async Task<TAggregate> ThenAggregate()
    {
        if (!_hasExecuted)
        {
            await SetupAggregate();
        }

        return _aggregate!;
    }

    private async Task SetupAggregate()
    {
        if (!EqualityComparer<TAggregate>.Default.Equals(_aggregate, default))
        {
            return; // Already setup
        }

        // Get or create the document first
        var document = await _context.DocumentFactory.GetOrCreateAsync(_objectName, _objectId);

        // Apply given events directly to the data store (bypasses type registry)
        if (_givenEvents.Count > 0)
        {
            // Update document version to match events
            document.Active.CurrentStreamVersion = _givenEvents.Max(e => e.EventVersion);

            // Add events directly to the in-memory data store
            await _context.DataStore.AppendAsync(document, [.. _givenEvents]);
        }

        // Get event stream for the object
        var stream = _context.EventStreamFactory.Create(document);

        // Create aggregate using the provided factory (AOT-friendly)
        _aggregate = _aggregateFactory(stream);

        // Apply snapshot if provided
        if (_givenSnapshot != null)
        {
            _aggregate.ProcessSnapshot(_givenSnapshot);
        }

        // Fold the aggregate to apply all events
        await _aggregate.Fold();
    }
}

/// <summary>
/// Provides static factory methods for creating aggregate test builders with AOT-friendly patterns.
/// </summary>
public static class AggregateTestBuilder
{
    /// <summary>
    /// Creates a new test builder for an aggregate that implements ITestableAggregate.
    /// Uses the static Create method from the interface (fully AOT-friendly, no factory needed).
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type implementing ITestableAggregate.</typeparam>
    /// <param name="objectId">The identifier of the aggregate instance.</param>
    /// <param name="context">The test context providing in-memory infrastructure.</param>
    /// <returns>A new <see cref="AggregateTestBuilder{TAggregate}"/> instance.</returns>
    public static AggregateTestBuilder<TAggregate> For<TAggregate>(
        string objectId,
        TestContext context)
        where TAggregate : ITestableAggregate<TAggregate>
    {
        return AggregateTestBuilder<TAggregate>.For(
            TAggregate.ObjectName,
            objectId,
            context,
            TAggregate.Create);
    }

    /// <summary>
    /// Creates a new test builder for an aggregate with strongly-typed identifier.
    /// Uses the static Create method from the interface (fully AOT-friendly, no factory needed).
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type implementing ITestableAggregate with TId.</typeparam>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <param name="objectId">The identifier of the aggregate instance.</param>
    /// <param name="context">The test context providing in-memory infrastructure.</param>
    /// <returns>A new <see cref="AggregateTestBuilder{TAggregate}"/> instance.</returns>
    public static AggregateTestBuilder<TAggregate> For<TAggregate, TId>(
        TId objectId,
        TestContext context)
        where TAggregate : ITestableAggregate<TAggregate, TId>
    {
        return AggregateTestBuilder<TAggregate>.For(
            TAggregate.ObjectName,
            objectId!.ToString()!,
            context,
            TAggregate.Create);
    }

    /// <summary>
    /// Creates a new test builder for an aggregate that implements ITestableAggregate with custom factory.
    /// Use this overload when you need custom aggregate initialization.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type implementing ITestableAggregate.</typeparam>
    /// <param name="objectId">The identifier of the aggregate instance.</param>
    /// <param name="context">The test context providing in-memory infrastructure.</param>
    /// <param name="aggregateFactory">Factory function to create the aggregate from an event stream.</param>
    /// <returns>A new <see cref="AggregateTestBuilder{TAggregate}"/> instance.</returns>
    public static AggregateTestBuilder<TAggregate> For<TAggregate>(
        string objectId,
        TestContext context,
        Func<IEventStream, TAggregate> aggregateFactory)
        where TAggregate : ITestableAggregate<TAggregate>
    {
        return AggregateTestBuilder<TAggregate>.For(
            TAggregate.ObjectName,
            objectId,
            context,
            aggregateFactory);
    }

    /// <summary>
    /// Creates a new test builder for an aggregate with strongly-typed identifier and custom factory.
    /// Use this overload when you need custom aggregate initialization.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type implementing ITestableAggregate with TId.</typeparam>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <param name="objectId">The identifier of the aggregate instance.</param>
    /// <param name="context">The test context providing in-memory infrastructure.</param>
    /// <param name="aggregateFactory">Factory function to create the aggregate from an event stream.</param>
    /// <returns>A new <see cref="AggregateTestBuilder{TAggregate}"/> instance.</returns>
    public static AggregateTestBuilder<TAggregate> For<TAggregate, TId>(
        TId objectId,
        TestContext context,
        Func<IEventStream, TAggregate> aggregateFactory)
        where TAggregate : ITestableAggregate<TAggregate, TId>
    {
        return AggregateTestBuilder<TAggregate>.For(
            TAggregate.ObjectName,
            objectId!.ToString()!,
            context,
            aggregateFactory);
    }
}
