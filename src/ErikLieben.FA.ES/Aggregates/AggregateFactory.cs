using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Aggregates;

/// <summary>
/// Provides a base implementation for factories that resolve aggregate-specific factories from a service provider.
/// </summary>
public abstract class AggregateFactory : IAggregateFactory
{
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve registered aggregate factories.</param>
    protected AggregateFactory(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets a strongly typed aggregate factory for the specified aggregate base type.
    /// </summary>
    /// <typeparam name="T">The aggregate type that implements <see cref="IBase"/>.</typeparam>
    /// <returns>The resolved <see cref="IAggregateFactory{T}"/> instance if registered; otherwise, null.</returns>
    public IAggregateFactory<T>? GetFactory<T>() where T : IBase
    {
        var factory = serviceProvider.GetService(InternalGet(typeof(T)));
        return factory as IAggregateFactory<T>;
    }

    /// <summary>
    /// Gets a covariance aggregate factory for the specified aggregate runtime type.
    /// </summary>
    /// <param name="type">The runtime type of the aggregate implementing <see cref="IBase"/>.</param>
    /// <returns>An <see cref="IAggregateCovarianceFactory{T}"/> for <see cref="IBase"/> if registered; otherwise, null.</returns>
    public IAggregateCovarianceFactory<IBase>? GetFactory(Type type)
    {
        return serviceProvider.GetService(InternalGet(type)) as IAggregateCovarianceFactory<IBase>;
    }

    /// <summary>
    /// Gets the service type that should be requested from the service provider for the given aggregate type.
    /// </summary>
    /// <param name="type">The aggregate type for which the factory service should be resolved.</param>
    /// <returns>The service type to request from the service provider.</returns>
    protected abstract Type InternalGet(Type type);
}
