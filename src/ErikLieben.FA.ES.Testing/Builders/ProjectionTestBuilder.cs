using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.Testing.Assertions;

namespace ErikLieben.FA.ES.Testing.Builders;

/// <summary>
/// Non-generic entry point for creating projection test builders using ITestableProjection.
/// </summary>
public static class ProjectionTestBuilder
{
    /// <summary>
    /// Creates a new projection test builder for a projection implementing ITestableProjection (AOT-friendly).
    /// </summary>
    /// <typeparam name="TProjection">The projection type implementing ITestableProjection.</typeparam>
    /// <param name="context">The test context providing in-memory infrastructure.</param>
    /// <returns>A new <see cref="ProjectionTestBuilder{TProjection}"/> instance.</returns>
    public static ProjectionTestBuilder<TProjection> For<TProjection>(TestContext context)
        where TProjection : Projection, ITestableProjection<TProjection>
    {
        ArgumentNullException.ThrowIfNull(context);

        var projection = TProjection.Create(context.DocumentFactory, context.EventStreamFactory);
        return ProjectionTestBuilder<TProjection>.Create(context, projection);
    }
}

/// <summary>
/// Provides a fluent API for testing projections with event application and state verification.
/// </summary>
/// <typeparam name="TProjection">The projection type that inherits from <see cref="Projection"/>.</typeparam>
public class ProjectionTestBuilder<TProjection> where TProjection : Projection
{
    private readonly TestContext _context;
    private readonly TProjection _projection;
    private readonly List<(string objectName, string objectId, IEvent[] events)> _givenEventStreams = new();

    private ProjectionTestBuilder(TestContext context, TProjection projection)
    {
        _context = context;
        _projection = projection;
    }

    /// <summary>
    /// Creates a new projection test builder with an existing projection instance (AOT-friendly).
    /// </summary>
    /// <param name="context">The test context providing in-memory infrastructure.</param>
    /// <param name="projection">The projection instance to test.</param>
    /// <returns>A new <see cref="ProjectionTestBuilder{TProjection}"/> instance.</returns>
    public static ProjectionTestBuilder<TProjection> Create(TestContext context, TProjection projection)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(projection);

        return new ProjectionTestBuilder<TProjection>(context, projection);
    }

    /// <summary>
    /// Creates a new projection test builder using a factory function (AOT-friendly).
    /// </summary>
    /// <param name="context">The test context providing in-memory infrastructure.</param>
    /// <param name="projectionFactory">Factory function to create the projection.</param>
    /// <returns>A new <see cref="ProjectionTestBuilder{TProjection}"/> instance.</returns>
    public static ProjectionTestBuilder<TProjection> Create(
        TestContext context,
        Func<IObjectDocumentFactory, IEventStreamFactory, TProjection> projectionFactory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(projectionFactory);

        var projection = projectionFactory(context.DocumentFactory, context.EventStreamFactory);
        return new ProjectionTestBuilder<TProjection>(context, projection);
    }

    /// <summary>
    /// Sets up events for a specific object stream using explicit object name.
    /// </summary>
    /// <param name="objectName">The logical name/scope of the object.</param>
    /// <param name="objectId">The identifier of the object instance.</param>
    /// <param name="events">The events to apply to this stream.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ProjectionTestBuilder<TProjection> GivenEvents(
        string objectName,
        string objectId,
        params IEvent[] events)
    {
        ArgumentNullException.ThrowIfNull(objectName);
        ArgumentNullException.ThrowIfNull(objectId);
        ArgumentNullException.ThrowIfNull(events);

        _givenEventStreams.Add((objectName, objectId, events));
        return this;
    }

    /// <summary>
    /// Sets up events for a specific object stream using aggregate type (AOT-friendly).
    /// Uses the static ObjectName from the aggregate type.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type implementing ITestableAggregate.</typeparam>
    /// <param name="objectId">The identifier of the object instance.</param>
    /// <param name="events">The events to apply to this stream.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ProjectionTestBuilder<TProjection> Given<TAggregate>(
        string objectId,
        params IEvent[] events)
        where TAggregate : ITestableAggregate<TAggregate>
    {
        ArgumentNullException.ThrowIfNull(objectId);
        ArgumentNullException.ThrowIfNull(events);

        _givenEventStreams.Add((TAggregate.ObjectName, objectId, events));
        return this;
    }

    /// <summary>
    /// Sets up events for a specific object stream using aggregate type with strongly-typed identifier (AOT-friendly).
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type implementing ITestableAggregate with TId.</typeparam>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <param name="objectId">The identifier of the object instance.</param>
    /// <param name="events">The events to apply to this stream.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ProjectionTestBuilder<TProjection> Given<TAggregate, TId>(
        TId objectId,
        params IEvent[] events)
        where TAggregate : ITestableAggregate<TAggregate, TId>
    {
        ArgumentNullException.ThrowIfNull(objectId);
        ArgumentNullException.ThrowIfNull(events);

        _givenEventStreams.Add((TAggregate.ObjectName, TAggregate.FormatId(objectId), events));
        return this;
    }

    /// <summary>
    /// Sets up events from multiple object streams.
    /// </summary>
    /// <param name="streams">Tuples of (objectName, objectId, events).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ProjectionTestBuilder<TProjection> GivenEventsFrom(
        params (string objectName, string objectId, IEvent[] events)[] streams)
    {
        ArgumentNullException.ThrowIfNull(streams);

        foreach (var stream in streams)
        {
            _givenEventStreams.Add(stream);
        }

        return this;
    }

    /// <summary>
    /// Updates the projection to the latest version for all tracked streams.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public async Task<ProjectionTestBuilder<TProjection>> UpdateToLatest()
    {
        // Apply all given events to their respective streams
        foreach (var (objectName, objectId, events) in _givenEventStreams)
        {
            var stream = await _context.GetEventStreamFor(objectName, objectId);

            await stream.Session(ctx =>
            {
                foreach (var @event in events)
                {
                    ctx.Append(@event);
                }
            });

            // Update projection to this stream's version
            var document = await _context.DocumentFactory.GetAsync(objectName, objectId);
            var versionToken = new VersionToken(events.Last(), document);
            await _projection.UpdateToVersion(versionToken.ToLatestVersion());
        }

        return this;
    }

    /// <summary>
    /// Updates the projection to a specific version.
    /// </summary>
    /// <param name="token">The version token to update to.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public async Task<ProjectionTestBuilder<TProjection>> UpdateToVersion(VersionToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        await _projection.UpdateToVersion(token);
        return this;
    }

    /// <summary>
    /// Returns an assertion helper to verify the projection outcome.
    /// </summary>
    /// <returns>A <see cref="ProjectionAssertion{TProjection}"/> for chaining assertions.</returns>
    public ProjectionAssertion<TProjection> Then()
    {
        return new ProjectionAssertion<TProjection>(_projection);
    }

    /// <summary>
    /// Gets the projection instance being tested.
    /// </summary>
    public TProjection Projection => _projection;
}
