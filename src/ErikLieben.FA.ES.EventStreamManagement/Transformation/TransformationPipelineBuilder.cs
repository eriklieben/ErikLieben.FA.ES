namespace ErikLieben.FA.ES.EventStreamManagement.Transformation;

using Microsoft.Extensions.Logging;

/// <summary>
/// Builder for creating transformation pipelines.
/// </summary>
public class TransformationPipelineBuilder : ITransformationPipelineBuilder
{
    private readonly List<IEventTransformer> transformers = new();
    private readonly List<Func<IEvent, bool>> filters = new();
    private readonly ILoggerFactory loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransformationPipelineBuilder"/> class.
    /// </summary>
    public TransformationPipelineBuilder(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <inheritdoc/>
    public ITransformationPipelineBuilder AddTransformer(IEventTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);
        transformers.Add(transformer);
        return this;
    }

    /// <inheritdoc/>
    public ITransformationPipelineBuilder AddTransformer<T>() where T : IEventTransformer, new()
    {
        transformers.Add(new T());
        return this;
    }

    /// <inheritdoc/>
    public ITransformationPipelineBuilder AddFilter(Func<IEvent, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        filters.Add(predicate);
        return this;
    }

    /// <inheritdoc/>
    public ITransformationPipeline Build()
    {
        var pipeline = new TransformationPipeline(loggerFactory.CreateLogger<TransformationPipeline>());

        // Add filter transformer if filters were specified
        if (filters.Count > 0)
        {
            pipeline.AddTransformer(new FilterTransformer(filters));
        }

        // Add all configured transformers
        foreach (var transformer in transformers)
        {
            pipeline.AddTransformer(transformer);
        }

        return pipeline;
    }

    /// <summary>
    /// Internal transformer that applies filters.
    /// </summary>
    private sealed class FilterTransformer : IEventTransformer
    {
        private readonly List<Func<IEvent, bool>> filters;

        public FilterTransformer(List<Func<IEvent, bool>> filters)
        {
            this.filters = filters;
        }

        public bool CanTransform(string eventName, int version) => true;

        public Task<IEvent> TransformAsync(IEvent sourceEvent, CancellationToken cancellationToken = default)
        {
            // Check if event passes all filters
            if (!filters.All(filter => filter(sourceEvent)))
            {
                throw new EventFilteredException(
                    $"Event {sourceEvent.EventType} v{sourceEvent.EventVersion} was filtered out");
            }

            return Task.FromResult(sourceEvent);
        }
    }
}

/// <summary>
/// Exception thrown when an event is filtered out by the pipeline.
/// </summary>
public class EventFilteredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventFilteredException"/> class.
    /// </summary>
    public EventFilteredException(string message) : base(message)
    {
    }
}
