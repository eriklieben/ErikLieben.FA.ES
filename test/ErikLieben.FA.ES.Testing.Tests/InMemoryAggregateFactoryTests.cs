using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing.InMemory;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryAggregateFactoryTests
{
    private class A : IBase
    {
        public Task Fold() => Task.CompletedTask;
        public void Fold(IEvent @event) { }
        public void ProcessSnapshot(object snapshot) { }
    }

    private class DummyCovFactory : IAggregateCovarianceFactory<IBase>
    {
        public string GetObjectName() => "a";
        public IBase Create(IEventStream eventStream) => new A();
        public IBase Create(ErikLieben.FA.ES.Documents.IObjectDocument document) => new A();
    }

    private class FakeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IAggregateCovarianceFactory<IBase>) ? new DummyCovFactory() : null;
    }

    [Fact]
    public void GetFactory_by_type_should_return_registered_factory_from_service_provider()
    {
        // Arrange
        var provider = new FakeServiceProvider();
        var factory = new InMemoryAggregateFactory(provider, [ t => t == typeof(A) ? typeof(IAggregateCovarianceFactory<IBase>) : null! ]);

        // Act
        var f = factory.GetFactory(typeof(A));

        // Assert
        Assert.NotNull(f);
        Assert.IsType<DummyCovFactory>(f);
    }
}
