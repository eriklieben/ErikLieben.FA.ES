namespace ErikLieben.FA.ES.EventStreamManagement.Transformation;

/// <summary>
/// A simple function-based event transformer.
/// </summary>
public class FunctionTransformer : IEventTransformer
{
    private readonly Func<IEvent, CancellationToken, Task<IEvent>> transformFunc;
    private readonly Func<string, int, bool> canTransformFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionTransformer"/> class.
    /// </summary>
    /// <param name="transformFunc">The transformation function.</param>
    /// <param name="canTransformFunc">Optional predicate to determine if transformer applies.</param>
    public FunctionTransformer(
        Func<IEvent, CancellationToken, Task<IEvent>> transformFunc,
        Func<string, int, bool>? canTransformFunc = null)
    {
        this.transformFunc = transformFunc ?? throw new ArgumentNullException(nameof(transformFunc));
        this.canTransformFunc = canTransformFunc ?? ((_, _) => true);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionTransformer"/> class with a synchronous transform function.
    /// </summary>
    /// <param name="transformFunc">The synchronous transformation function.</param>
    /// <param name="canTransformFunc">Optional predicate to determine if transformer applies.</param>
    public FunctionTransformer(
        Func<IEvent, IEvent> transformFunc,
        Func<string, int, bool>? canTransformFunc = null)
    {
        ArgumentNullException.ThrowIfNull(transformFunc);
        this.transformFunc = (evt, _) => Task.FromResult(transformFunc(evt));
        this.canTransformFunc = canTransformFunc ?? ((_, _) => true);
    }

    /// <inheritdoc/>
    public bool CanTransform(string eventName, int version)
    {
        return canTransformFunc(eventName, version);
    }

    /// <inheritdoc/>
    public Task<IEvent> TransformAsync(IEvent sourceEvent, CancellationToken cancellationToken = default)
    {
        return transformFunc(sourceEvent, cancellationToken);
    }
}
