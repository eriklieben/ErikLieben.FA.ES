using System;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing.InMemory;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryEventStreamFactoryTests
{
    [Fact]
    public void Create_should_switch_default_stream_type_to_inMemory()
    {
        // Arrange
        var document = new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation
            {
                StreamConnectionName = "inMemory",
                SnapShotConnectionName = "inMemory",
                DocumentTagConnectionName = "inMemory",
                StreamTagConnectionName = "inMemory",
                StreamIdentifier = "1-0000000000",
                StreamType = "default",
                DocumentTagType = "inMemory",
                CurrentStreamVersion = -1
            },
            [],
            "1.0.0");

        var factory = new InMemoryEventStreamFactory(
            new InMemoryDocumentTagDocumentFactory(),
            new InMemoryObjectDocumentFactory(new InMemoryDocumentTagStore()),
            new InMemoryDataStore(),
            new InMemoryAggregateFactory(new SimpleServiceProvider(), [ _ => typeof(DummyAggregateFactory) ]));

        // Act
        var stream = factory.Create(document);

        // Assert
        Assert.Equal("inMemory", stream.Document.Active.StreamType);
    }

    private class SimpleServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private class DummyAggregateFactory : IAggregateCovarianceFactory<IBase>
    {
        public string GetObjectName() => "dummy";
        public IBase Create(IEventStream eventStream) => new Dummy();
        public IBase Create(IObjectDocument document) => new Dummy();

        private class Dummy : IBase
        {
            public Task Fold() => Task.CompletedTask;
            public void Fold(IEvent @event) { }
            public void ProcessSnapshot(object snapshot) { }
        }
    }
}
