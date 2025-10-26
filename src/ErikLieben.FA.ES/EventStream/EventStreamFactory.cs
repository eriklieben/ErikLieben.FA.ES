using System.Diagnostics;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.Processors;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Factory for creating event stream instances based on document stream types.
/// </summary>
public class EventStreamFactory : IEventStreamFactory
{
    private readonly IDictionary<string, IEventStreamFactory> eventStreamFactories;
    private readonly EventStreamDefaultTypeSettings settings;

    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES");

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamFactory"/> class.
    /// </summary>
    /// <param name="eventStreamFactories">Dictionary of stream type-specific factories.</param>
    /// <param name="settings">Default type settings for fallback behavior.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public EventStreamFactory(
        IDictionary<string, IEventStreamFactory> eventStreamFactories,
        EventStreamDefaultTypeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(eventStreamFactories);
        ArgumentNullException.ThrowIfNull(settings);

        this.eventStreamFactories = eventStreamFactories;
        this.settings = settings;
    }

    /// <summary>
    /// Creates an event stream instance for the specified document.
    /// </summary>
    /// <param name="document">The object document for which to create the event stream.</param>
    /// <returns>The created event stream instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the document or its stream type is null.</exception>
    /// <exception cref="UnableToCreateEventStreamForStreamTypeException">Thrown when no factory is found for the stream type.</exception>
    public IEventStream Create(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamType);

        using var activity = ActivitySource.StartActivity($"EventStreamFactory.{nameof(Create)}");

        if (eventStreamFactories.TryGetValue(document.Active.StreamType, out IEventStreamFactory? factory))
        {
            return factory.Create(document);
        }

        if (eventStreamFactories.TryGetValue(settings.StreamType, out var fallbackFactory))
        {
            return fallbackFactory.Create(document);
        }

        throw new UnableToCreateEventStreamForStreamTypeException(document.Active.StreamType, settings.StreamType);
    }
}
