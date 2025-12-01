namespace ErikLieben.FA.ES.EventStreamManagement.Transformation;

/// <summary>
/// Combines multiple transformers into a single transformer that applies them sequentially.
/// </summary>
public class CompositeTransformer : IEventTransformer
{
    private readonly IReadOnlyList<IEventTransformer> transformers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeTransformer"/> class.
    /// </summary>
    /// <param name="transformers">The transformers to compose.</param>
    public CompositeTransformer(params IEventTransformer[] transformers)
    {
        ArgumentNullException.ThrowIfNull(transformers);

        if (transformers.Length == 0)
        {
            throw new ArgumentException("At least one transformer is required", nameof(transformers));
        }

        this.transformers = transformers;
    }

    /// <inheritdoc/>
    public bool CanTransform(string eventName, int version)
    {
        // Can transform if any of the inner transformers can
        return transformers.Any(t => t.CanTransform(eventName, version));
    }

    /// <inheritdoc/>
    public async Task<IEvent> TransformAsync(IEvent sourceEvent, CancellationToken cancellationToken = default)
    {
        var currentEvent = sourceEvent;

        foreach (var transformer in transformers)
        {
            if (transformer.CanTransform(currentEvent.EventType, currentEvent.EventVersion))
            {
                currentEvent = await transformer.TransformAsync(currentEvent, cancellationToken);
            }
        }

        return currentEvent;
    }
}
