using ErikLieben.FA.ES.Aggregates;

namespace ErikLieben.FA.ES.Testing.InMemory;

public class InMemoryAggregateFactory(
    IServiceProvider serviceProvider,
    Func<Type?, Type>[] aggregateFactorGets)
    : AggregateFactory(serviceProvider!)
{
    private readonly Func<Type?, Type?>[] aggregateFactorGets = aggregateFactorGets;

    protected override Type InternalGet(Type type)
    {
        foreach (var aggregateFactorGet in aggregateFactorGets)
        {
            var aggregateType = aggregateFactorGet(type);
            if (aggregateType != null)
            {
                return aggregateType;
            }
        }

        return null!;
    }
}