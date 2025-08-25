using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Aggregates;

public abstract class AggregateFactory : IAggregateFactory
{
    private readonly IServiceProvider serviceProvider;

    protected AggregateFactory(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        this.serviceProvider = serviceProvider;
    }

    public IAggregateFactory<T>? GetFactory<T>() where T : IBase
    {
        var factory = serviceProvider.GetService(InternalGet(typeof(T)));
        return factory as IAggregateFactory<T>;
    }

    public IAggregateCovarianceFactory<IBase>? GetFactory(Type type)
    {
        return serviceProvider.GetService(InternalGet(type)) as IAggregateCovarianceFactory<IBase>;
    }

    protected abstract Type InternalGet(Type type);
}
