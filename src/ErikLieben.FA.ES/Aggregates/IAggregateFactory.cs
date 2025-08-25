using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Aggregates;

public interface IAggregateFactory
{
    IAggregateFactory<T>? GetFactory<T>() where T : IBase;

    IAggregateCovarianceFactory<IBase>? GetFactory(Type type);
}
