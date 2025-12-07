namespace ErikLieben.FA.ES.EventStreamManagement.Transformation;

/// <summary>
/// Defines a pipeline of event transformers that are applied sequentially.
/// </summary>
public interface ITransformationPipeline : IEventTransformer
{
    /// <summary>
    /// Adds a transformer to the end of the pipeline.
    /// </summary>
    /// <param name="transformer">The transformer to add.</param>
    void AddTransformer(IEventTransformer transformer);

    /// <summary>
    /// Gets the number of transformers in the pipeline.
    /// </summary>
    int Count { get; }
}
