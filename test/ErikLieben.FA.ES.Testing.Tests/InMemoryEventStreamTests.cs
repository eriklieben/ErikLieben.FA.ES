#pragma warning disable CS0618 // Type or member is obsolete - testing deprecated API intentionally

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryEventStreamTests
{
    [Fact]
    public void Events_property_should_reflect_underlying_data_store()
    {
        // Arrange
        var tagFactory = new InMemoryDocumentTagDocumentFactory();
        var docFactory = new InMemoryObjectDocumentFactory(new InMemoryDocumentTagStore());
        var dataStore = new InMemoryDataStore();
        var aggregateFactory = new InMemoryAggregateFactory(new SimpleServiceProvider(), [ _ => typeof(DummyFactory) ]);
        var eventStreamFactory = new InMemoryEventStreamFactory(tagFactory, docFactory, dataStore, aggregateFactory);

        var document = new InMemoryEventStreamDocument(
            "42",
            "order",
            new StreamInformation
            {
                StreamConnectionName = "inMemory",
                SnapShotConnectionName = "inMemory",
                DocumentTagConnectionName = "inMemory",
                StreamTagConnectionName = "inMemory",
                StreamIdentifier = "42-0000000000",
                StreamType = "inMemory",
                DocumentTagType = "inMemory",
                CurrentStreamVersion = -1,
            },
            [],
            "1.0.0");

        var stream = eventStreamFactory.Create(document);

        // Inject two events directly (simulate previously appended)
        var key = InMemoryDataStore.GetStoreKey(document.ObjectName, document.ObjectId);
        dataStore.Store[key] = new Dictionary<int, IEvent>
        {
            [0] = new JsonEvent { EventType = "OrderCreated", EventVersion = 0, Payload = JsonSerializer.Serialize(new { id = 1 }) },
            [1] = new JsonEvent { EventType = "OrderConfirmed", EventVersion = 1, Payload = JsonSerializer.Serialize(new { id = 1 }) }
        };

        // Act
        var events = ((InMemoryStream)stream).Events;

        // Assert
        Assert.Equal(2, events.Count());
        Assert.Collection(events,
            e => Assert.Equal("OrderCreated", e.EventType),
            e => Assert.Equal("OrderConfirmed", e.EventType));
    }

    private class SimpleServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private class DummyFactory : IAggregateCovarianceFactory<IBase>
    {
        public string GetObjectName() => "dummy";
        public IBase Create(IEventStream eventStream) => new D();
        public IBase Create(IObjectDocument document) => new D();

        private class D : IBase
        {
            public Task Fold() => Task.CompletedTask;
            public void Fold(IEvent @event) { }
            public void ProcessSnapshot(object snapshot) { }
        }
    }
}
