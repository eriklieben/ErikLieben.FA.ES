#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Testing.Time;
using ErikLieben.FA.ES.Processors;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class TestContextTests
{
    [EventName("EvtA")] private record EvtA(string V);
    [EventName("EvtB")] private record EvtB(string V);
    private record NoAttr(string V);

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
    public async Task GetEventStreamFor_should_return_stream_and_document_matches()
    {
        // Arrange
        var ctx = CreateContext();

        // Act
        var stream = await ctx.GetEventStreamFor("Order", "42");

        // Assert
        Assert.NotNull(stream);
        Assert.Equal("order", stream.Document.ObjectName); // should be lowercased by factory
        Assert.Equal("42", stream.Document.ObjectId);
    }

    [Fact]
    public void ShouldHaveObject_should_return_EventAssertionExtension_when_object_present()
    {
        // Arrange
        var ctx = CreateContext();
        var key = InMemoryDataStore.GetStoreKey("order", "1");
        ctx.Events[key] = new Dictionary<int, IEvent>();

        // Act
        var ext = ctx.Assert.ShouldHaveObject("order", "1");

        // Assert
        Assert.NotNull(ext);
        // Also check WithEventCount(0) success path
        ext.WithEventCount(0);
    }

    [Fact]
    public void WithEventAtPosition_should_throw_when_out_of_range()
    {
        // Arrange
        var ctx = CreateContext();
        var key = InMemoryDataStore.GetStoreKey("order", "2");
        ctx.Events[key] = new Dictionary<int, IEvent> { [0] = new JsonEvent { EventType = "EvtA", EventVersion = 0, Payload = JsonSerializer.Serialize(new EvtA("x")) } };

        // Act & Assert
        Assert.Throws<TestAssertionException>(() => ctx.Assert.ShouldHaveObject("order", "2").WithEventAtPosition(5, new EvtA("x")));
    }

    [Fact]
    public void WithEventAtLastPosition_should_throw_when_event_type_mismatch()
    {
        // Arrange
        var ctx = CreateContext();
        var key = InMemoryDataStore.GetStoreKey("order", "3");
        ctx.Events[key] = new Dictionary<int, IEvent>
        {
            [0] = new JsonEvent { EventType = "EvtB", EventVersion = 0, Payload = JsonSerializer.Serialize(new EvtB("y")) }
        };

        // Act & Assert (expected type EvtA but stored is EvtB)
        Assert.Throws<TestAssertionException>(() => ctx.Assert.ShouldHaveObject("order", "3").WithEventAtLastPosition(new EvtA("y")));
    }

    [Fact]
    public void WithEventAtPosition_should_throw_when_payload_mismatch()
    {
        // Arrange
        var ctx = CreateContext();
        var key = InMemoryDataStore.GetStoreKey("order", "4");
        ctx.Events[key] = new Dictionary<int, IEvent>
        {
            [0] = new JsonEvent { EventType = "EvtA", EventVersion = 0, Payload = JsonSerializer.Serialize(new EvtA("one")) }
        };

        // Act & Assert (payload different)
        Assert.Throws<TestAssertionException>(() => ctx.Assert.ShouldHaveObject("order", "4").WithEventAtPosition(0, new EvtA("two")));
    }

    [Fact]
    public void WithSingleEvent_should_throw_when_EventNameAttribute_missing()
    {
        // Arrange
        var ctx = CreateContext();
        var key = InMemoryDataStore.GetStoreKey("order", "5");
        ctx.Events[key] = new Dictionary<int, IEvent>
        {
            [0] = new JsonEvent { EventType = "NoAttr", EventVersion = 0, Payload = JsonSerializer.Serialize(new NoAttr("x")) }
        };

        // Act & Assert
        Assert.Throws<TestAssertionException>(() => ctx.Assert.ShouldHaveObject("order", "5").WithSingleEvent(new NoAttr("x")));
    }

    [Fact]
    public void TestAssertionException_should_be_constructable_with_message()
    {
        // Arrange & Act
        var exception = new TestAssertionException("Test message");

        // Assert
        Assert.NotNull(exception);
        Assert.Equal("Test message", exception.Message);
    }

    [Fact]
    public void TestAssertionException_should_be_constructable_with_message_and_inner_exception()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new TestAssertionException("Test message", innerException);

        // Assert
        Assert.NotNull(exception);
        Assert.Equal("Test message", exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void TestClock_property_should_return_null_when_not_provided()
    {
        // Arrange
        var ctx = CreateContext();

        // Assert
        Assert.Null(ctx.TestClock);
    }

    [Fact]
    public void TestClock_property_should_return_provided_clock()
    {
        // Arrange
        var testClock = new TestClock(new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var provider = new SimpleServiceProvider();
        var ctx = TestSetup.GetContext(provider, testClock, _ => typeof(DummyFactory));

        // Assert
        Assert.NotNull(ctx.TestClock);
        Assert.Same(testClock, ctx.TestClock);
        Assert.Equal(new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero), ctx.TestClock.UtcNow);
    }

    [Fact]
    public void DocumentFactory_property_should_return_factory()
    {
        // Arrange
        var ctx = CreateContext();

        // Assert
        Assert.NotNull(ctx.DocumentFactory);
    }

    [Fact]
    public void EventStreamFactory_property_should_return_factory()
    {
        // Arrange
        var ctx = CreateContext();

        // Assert
        Assert.NotNull(ctx.EventStreamFactory);
    }
}
