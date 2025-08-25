using System.Text.Json;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.InMemory;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class EventAssertionExtensionAdditionalTests
{
    [EventName("EvtA")] private record EvtA(string V);
    [EventName("EvtB")] private record EvtB(string V);

    private static TestContext CreateContext()
    {
        var provider = new SimpleServiceProvider();
        return TestSetup.GetContext(provider, _ => typeof(DummyFactory));
    }

    private class SimpleServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private class DummyFactory : ErikLieben.FA.ES.Aggregates.IAggregateCovarianceFactory<IBase>
    {
        public string GetObjectName() => "dummy";
        public IBase Create(IEventStream eventStream) => new D();
        public IBase Create(ErikLieben.FA.ES.Documents.IObjectDocument document) => new D();
        private class D : IBase { public Task Fold() => Task.CompletedTask; public void Fold(IEvent @event) { } public void ProcessSnapshot(object snapshot) { } }
    }

    [Fact]
    public void WithEventCount_should_throw_on_mismatch()
    {
        // Arrange
        var ctx = CreateContext();
        var key = InMemoryDataStore.GetStoreKey("order", "cnt");
        ctx.Events[key] = new Dictionary<int, IEvent>
        {
            [0] = new JsonEvent { EventType = "EvtA", EventVersion = 0, Payload = JsonSerializer.Serialize(new EvtA("x")) },
            [1] = new JsonEvent { EventType = "EvtA", EventVersion = 1, Payload = JsonSerializer.Serialize(new EvtA("y")) }
        };

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => ctx.Assert.ShouldHaveObject("order", "cnt").WithEventCount(1));
    }

    [Fact]
    public void WithSingleEvent_should_succeed_when_one_matching_event()
    {
        // Arrange
        var ctx = CreateContext();
        var key = InMemoryDataStore.GetStoreKey("order", "single");
        ctx.Events[key] = new Dictionary<int, IEvent>
        {
            [0] = new JsonEvent { EventType = "EvtA", EventVersion = 0, Payload = JsonSerializer.Serialize(new EvtA("ok")) }
        };

        // Act + Assert
        ctx.Assert.ShouldHaveObject("order", "single").WithSingleEvent(new EvtA("ok"));
    }

    [Fact]
    public void WithEventAtPosition_should_throw_on_type_mismatch()
    {
        // Arrange
        var ctx = CreateContext();
        var key = InMemoryDataStore.GetStoreKey("order", "typeMismatch");
        ctx.Events[key] = new Dictionary<int, IEvent>
        {
            [0] = new JsonEvent { EventType = "EvtB", EventVersion = 0, Payload = JsonSerializer.Serialize(new EvtB("x")) }
        };

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => ctx.Assert.ShouldHaveObject("order", "typeMismatch").WithEventAtPosition(0, new EvtA("x")));
    }

    [Fact]
    public void WithEventAtLastPosition_should_throw_on_payload_mismatch()
    {
        // Arrange
        var ctx = CreateContext();
        var key = InMemoryDataStore.GetStoreKey("order", "lastPayload");
        ctx.Events[key] = new Dictionary<int, IEvent>
        {
            [0] = new JsonEvent { EventType = "EvtA", EventVersion = 0, Payload = JsonSerializer.Serialize(new EvtA("one")) },
            [1] = new JsonEvent { EventType = "EvtA", EventVersion = 1, Payload = JsonSerializer.Serialize(new EvtA("two")) }
        };

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => ctx.Assert.ShouldHaveObject("order", "lastPayload").WithEventAtLastPosition(new EvtA("different")));
    }
}
