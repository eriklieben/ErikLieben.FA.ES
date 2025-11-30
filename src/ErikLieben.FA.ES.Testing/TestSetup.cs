using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Testing.Time;

namespace ErikLieben.FA.ES.Testing;

public static class TestSetup
{
    public static TestContext GetContext(
        IServiceProvider serviceProvider,
        params Func<Type?, Type>[] aggregateFactorGets)
    {
        return GetContext(serviceProvider, null, aggregateFactorGets);
    }

    public static TestContext GetContext(
        IServiceProvider serviceProvider,
        ITestClock? testClock,
        params Func<Type?, Type>[] aggregateFactorGets)
    {
        IObjectDocumentFactory documentFactory = new InMemoryObjectDocumentFactory(
                                                             new InMemoryDocumentTagStore());
        var documentTagStore = new InMemoryDocumentTagStore();
        var dataStore = new InMemoryDataStore();
        var eventStreamFactory = new InMemoryEventStreamFactory(
            new InMemoryDocumentTagDocumentFactory(),
            new InMemoryObjectDocumentFactory(documentTagStore),
            dataStore,
            new InMemoryAggregateFactory(serviceProvider, aggregateFactorGets));

        return new TestContext(
            documentFactory,
            eventStreamFactory,
            dataStore,
            testClock);
    }


    public static TestContext GetContext(TestContextSettings settings)
    {
        IObjectDocumentFactory documentFactory = new InMemoryObjectDocumentFactory(
                                                             new InMemoryDocumentTagStore());
        var documentTagStore = new InMemoryDocumentTagStore();
        var dataStore = new InMemoryDataStore();
        var eventStreamFactory = new InMemoryEventStreamFactory(
            new InMemoryDocumentTagDocumentFactory(),
            new InMemoryObjectDocumentFactory(documentTagStore),
            dataStore,
            new InMemoryAggregateFactory(settings.ServiceProvider, settings.AggregateFactorGets));

        return new TestContext(
            documentFactory,
            eventStreamFactory,
            dataStore);
    }
}

public record TestContextSettings(IServiceProvider ServiceProvider, params Func<Type?, Type>[] AggregateFactorGets);
