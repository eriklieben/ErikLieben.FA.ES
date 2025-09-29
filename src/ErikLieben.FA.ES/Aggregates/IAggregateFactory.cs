using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Aggregates;

/// <summary>
/// Provides methods to resolve aggregate factories for creating and retrieving aggregate instances.
/// </summary>
public interface IAggregateFactory
{
    /// <summary>
    /// Gets a factory for the specified aggregate type.
    /// </summary>
    /// <typeparam name="T">The aggregate type that implements <see cref="IBase"/>.</typeparam>
    /// <returns>An <see cref="IAggregateFactory{T}"/> when registered; otherwise, null.</returns>
    IAggregateFactory<T>? GetFactory<T>() where T : IBase;

    /// <summary>
    /// Gets a covariance factory for the specified aggregate runtime type.
    /// </summary>
    /// <param name="type">The aggregate runtime type that implements <see cref="IBase"/>.</param>
    /// <returns>An <see cref="IAggregateCovarianceFactory{T}"/> for <see cref="IBase"/> when registered; otherwise, null.</returns>
    IAggregateCovarianceFactory<IBase>? GetFactory(Type type);
}
