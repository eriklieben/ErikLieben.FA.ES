using System.Diagnostics;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.Processors;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.EventStream;

public class EventStreamFactory : IEventStreamFactory
{
    private readonly IDictionary<string, IEventStreamFactory> eventStreamFactories;
    private readonly EventStreamDefaultTypeSettings settings;

    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES");

    public EventStreamFactory(
        IDictionary<string, IEventStreamFactory> eventStreamFactories,
        EventStreamDefaultTypeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(eventStreamFactories);
        ArgumentNullException.ThrowIfNull(settings);

        this.eventStreamFactories = eventStreamFactories;
        this.settings = settings;
    }

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
