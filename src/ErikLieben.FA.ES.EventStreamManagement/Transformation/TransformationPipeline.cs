namespace ErikLieben.FA.ES.EventStreamManagement.Transformation;

using Microsoft.Extensions.Logging;

/// <summary>
/// Implementation of a transformation pipeline that applies multiple transformers sequentially.
/// </summary>
public class TransformationPipeline : ITransformationPipeline
{
    private readonly List<IEventTransformer> transformers = new();
    private readonly ILogger<TransformationPipeline> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransformationPipeline"/> class.
    /// </summary>
    public TransformationPipeline(ILogger<TransformationPipeline> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public int Count => transformers.Count;

    /// <inheritdoc/>
    public void AddTransformer(IEventTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);
        transformers.Add(transformer);

        logger.TransformerAddedToPipeline(transformer.GetType().Name, transformers.Count);
    }

    /// <inheritdoc/>
    public bool CanTransform(string eventName, int version)
    {
        // Can transform if any of the inner transformers can
        return transformers.Any(t => t.CanTransform(eventName, version));
    }

    /// <inheritdoc/>
    public async Task<IEvent> TransformAsync(
        IEvent sourceEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceEvent);

        if (transformers.Count == 0)
        {
            return sourceEvent;
        }

        return await transformers.Aggregate(
            Task.FromResult(sourceEvent),
            async (eventTask, transformer) =>
            {
                var current = await eventTask;
                if (transformer.CanTransform(current.EventType, current.EventVersion))
                {
                    logger.ApplyingTransformer(
                        transformer.GetType().Name,
                        current.EventType,
                        current.EventVersion);

                    return await transformer.TransformAsync(current, cancellationToken);
                }
                return current;
            });
    }
}
