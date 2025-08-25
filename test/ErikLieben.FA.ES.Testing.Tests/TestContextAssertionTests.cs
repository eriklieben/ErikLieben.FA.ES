using System.Text.Json;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Processors;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class TestContextAssertionTests
{
    [EventName("UserRegistered")]
    private record UserRegistered(string Email);

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
    public void ShouldHaveObject_should_throw_when_object_absent()
    {
        // Arrange
        var ctx = CreateContext();

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => ctx.Assert.ShouldHaveObject("order", "1"));
    }

    [Fact]
    public void WithEventCount_and_WithSingleEvent_and_WithEventAtPosition_and_WithEventAtLastPosition_should_validate_events()
    {
        // Arrange
        var ctx = CreateContext();
        var key = InMemoryDataStore.GetStoreKey("user", "1");
        ctx.Events[key] = new Dictionary<int, IEvent>
        {
            [0] = new JsonEvent
            {
                EventType = "UserRegistered",
                EventVersion = 0,
                Payload = JsonSerializer.Serialize(new UserRegistered("u@example.com"))
            },
            [1] = new JsonEvent
            {
                EventType = "UserRegistered",
                EventVersion = 1,
                Payload = JsonSerializer.Serialize(new UserRegistered("u@example.com"))
            }
        };

        // Act + Assert (AAA pattern per assertion)
        // Count
        ctx.Assert.ShouldHaveObject("user", "1").WithEventCount(2);

        // Single (will fail because there are 2) -> verify throws
        Assert.ThrowsAny<Exception>(() => ctx.Assert.ShouldHaveObject("user", "1").WithSingleEvent(new UserRegistered("u@example.com")));

        // Position
        ctx.Assert.ShouldHaveObject("user", "1").WithEventAtPosition(0, new UserRegistered("u@example.com"));

        // Last position
        ctx.Assert.ShouldHaveObject("user", "1").WithEventAtLastPosition(new UserRegistered("u@example.com"));
    }
}
