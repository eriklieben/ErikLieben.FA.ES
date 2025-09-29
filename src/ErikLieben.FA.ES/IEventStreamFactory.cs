using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES;

/// <summary>
/// Creates event stream instances for specific object documents and aggregates.
/// </summary>
public interface IEventStreamFactory
{
    /// <summary>
    /// Creates an event stream for the specified document.
    /// </summary>
    /// <param name="document">The object document the stream belongs to.</param>
    /// <returns>A new <see cref="IEventStream"/> instance.</returns>
    IEventStream Create(IObjectDocument document);

}

/// <summary>
/// Creates aggregate-specific event stream wrappers.
/// </summary>
/// <typeparam name="T">The aggregate type.</typeparam>
public interface IEventStreamFactory<T> : IEventStreamFactory where T : Aggregate
{
    /// <summary>
    /// Creates an event stream wrapper for the specified underlying stream.
    /// </summary>
    /// <param name="eventStream">The underlying event stream.</param>
    /// <returns>An <see cref="IEventStream"/> specialized for <typeparamref name="T"/>.</returns>
    IEventStream Create(IEventStream eventStream);
}
