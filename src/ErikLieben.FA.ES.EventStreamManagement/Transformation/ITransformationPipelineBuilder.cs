namespace ErikLieben.FA.ES.EventStreamManagement.Transformation;

/// <summary>
/// Builder for configuring a transformation pipeline.
/// </summary>
public interface ITransformationPipelineBuilder
{
    /// <summary>
    /// Adds a transformer instance to the pipeline.
    /// </summary>
    /// <param name="transformer">The transformer to add.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ITransformationPipelineBuilder AddTransformer(IEventTransformer transformer);

    /// <summary>
    /// Adds a transformer of the specified type to the pipeline.
    /// </summary>
    /// <typeparam name="T">The type of transformer to add.</typeparam>
    /// <returns>This builder for fluent chaining.</returns>
    ITransformationPipelineBuilder AddTransformer<T>() where T : IEventTransformer, new();

    /// <summary>
    /// Adds a filter to exclude certain events from transformation.
    /// </summary>
    /// <param name="predicate">Predicate to determine if event should be included.</param>
    /// <returns>This builder for fluent chaining.</returns>
    ITransformationPipelineBuilder AddFilter(Func<IEvent, bool> predicate);

    /// <summary>
    /// Builds the configured pipeline.
    /// </summary>
    /// <returns>The configured transformation pipeline.</returns>
    ITransformationPipeline Build();
}
