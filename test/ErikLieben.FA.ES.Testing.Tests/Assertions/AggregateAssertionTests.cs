using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing.Assertions;
using ErikLieben.FA.ES.Testing.Builders;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Assertions;

/// <summary>
/// Tests for AggregateAssertion class, focusing on failure paths and edge cases.
/// </summary>
public partial class AggregateAssertionTests
{
    [EventName("ItemCreated")]
    private record ItemCreated(string Name);

    [EventName("ItemUpdated")]
    private record ItemUpdated(string NewName);

    [EventName("ItemDeleted")]
    private record ItemDeleted();

    [JsonSerializable(typeof(ItemCreated))]
    [JsonSerializable(typeof(ItemUpdated))]
    [JsonSerializable(typeof(ItemDeleted))]
    private partial class AggAssertItemEventsJsonContext : JsonSerializerContext { }

    private class ItemAggregate : Aggregate
    {
        public ItemAggregate(IEventStream stream) : base(stream)
        {
            stream.EventTypeRegistry.Add(
                typeof(ItemCreated),
                "ItemCreated",
                AggAssertItemEventsJsonContext.Default.ItemCreated);
            stream.EventTypeRegistry.Add(
                typeof(ItemUpdated),
                "ItemUpdated",
                AggAssertItemEventsJsonContext.Default.ItemUpdated);
            stream.EventTypeRegistry.Add(
                typeof(ItemDeleted),
                "ItemDeleted",
                AggAssertItemEventsJsonContext.Default.ItemDeleted);
        }

        public string? Name { get; private set; }
        public bool IsDeleted { get; private set; }

        public async Task Create(string name)
        {
            await Stream.Session(context =>
                Fold(context.Append(new ItemCreated(name))));
        }

        public async Task Update(string newName)
        {
            await Stream.Session(context =>
                Fold(context.Append(new ItemUpdated(newName))));
        }

        public async Task Delete()
        {
            await Stream.Session(context =>
                Fold(context.Append(new ItemDeleted())));
        }

        public async Task FailWithException()
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("Test exception");
        }

        private void When(ItemCreated @event) => Name = @event.Name;
        private void When(ItemUpdated @event) => Name = @event.NewName;
        private void When(ItemDeleted _) => IsDeleted = true;

        public override void Fold(IEvent @event)
        {
            if (@event is JsonEvent jsonEvent)
            {
                switch (jsonEvent.EventType)
                {
                    case "ItemCreated":
                        var created = JsonSerializer.Deserialize(jsonEvent.Payload, AggAssertItemEventsJsonContext.Default.ItemCreated);
                        if (created != null) When(created);
                        break;
                    case "ItemUpdated":
                        var updated = JsonSerializer.Deserialize(jsonEvent.Payload, AggAssertItemEventsJsonContext.Default.ItemUpdated);
                        if (updated != null) When(updated);
                        break;
                    case "ItemDeleted":
                        var deleted = JsonSerializer.Deserialize(jsonEvent.Payload, AggAssertItemEventsJsonContext.Default.ItemDeleted);
                        if (deleted != null) When(deleted);
                        break;
                }
            }
        }
    }

    private static TestContext CreateTestContext()
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
        public IBase Create(IEventStream eventStream) => new ItemAggregate(eventStream);
        public ErikLieben.FA.ES.Documents.IObjectDocument Create(ErikLieben.FA.ES.Documents.IObjectDocument document) =>
            throw new NotImplementedException();
        IBase ErikLieben.FA.ES.Aggregates.IAggregateCovarianceFactory<IBase>.Create(ErikLieben.FA.ES.Documents.IObjectDocument document) =>
            throw new NotImplementedException();
    }

    public class ShouldHaveAppendedMethod : AggregateAssertionTests
    {
        [Fact]
        public async Task Should_throw_when_exception_was_caught()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-1", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.FailWithException());
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveAppended<ItemCreated>());
            Assert.Contains("exception was thrown", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_event_not_found()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-2", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.Create("Test"));
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveAppended<ItemDeleted>());
            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public async Task Should_throw_with_payload_when_exception_was_caught()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-3", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.FailWithException());
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveAppended(new ItemCreated("Test")));
            Assert.Contains("exception was thrown", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_payload_does_not_match()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-4", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.Create("ActualName"));
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveAppended(new ItemCreated("DifferentName")));
            Assert.Contains("not found", ex.Message);
        }
    }

    public class ShouldNotHaveAppendedMethod : AggregateAssertionTests
    {
        [Fact]
        public async Task Should_throw_when_event_was_found()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-5", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.Create("Test"));
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldNotHaveAppended<ItemCreated>());
            Assert.Contains("NOT to be appended", ex.Message);
        }
    }

    public class ShouldHaveAppendedCountMethod : AggregateAssertionTests
    {
        [Fact]
        public async Task Should_throw_when_exception_was_caught()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-6", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.FailWithException());
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveAppendedCount(1));
            Assert.Contains("exception was thrown", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_count_does_not_match()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-7", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.Create("Test"));
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveAppendedCount(5));
            Assert.Contains("Expected 5", ex.Message);
            Assert.Contains("found 1", ex.Message);
        }
    }

    public class ShouldHaveAppendedAtLeastMethod : AggregateAssertionTests
    {
        [Fact]
        public async Task Should_throw_when_exception_was_caught()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-8", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.FailWithException());
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveAppendedAtLeast(1));
            Assert.Contains("exception was thrown", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_fewer_events_appended()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-9", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.Create("Test"));
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveAppendedAtLeast(10));
            Assert.Contains("at least 10", ex.Message);
        }
    }

    public class ShouldNotHaveAppendedAnyEventsMethod : AggregateAssertionTests
    {
        [Fact]
        public async Task Should_throw_when_events_were_appended()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-10", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.Create("Test"));
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldNotHaveAppendedAnyEvents());
            Assert.Contains("no events to be appended", ex.Message);
        }
    }

    public class ShouldContainEventMethod : AggregateAssertionTests
    {
        [Fact]
        public async Task Should_throw_when_no_event_matches_predicate()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-11", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.Create("ActualName"));
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldContainEvent<ItemCreated>(e => e.Name == "DifferentName"));
            Assert.Contains("matching the predicate", ex.Message);
        }
    }

    public class ShouldHaveStateMethod : AggregateAssertionTests
    {
        [Fact]
        public async Task Should_throw_when_exception_was_caught_action()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-12", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.FailWithException());
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveState(agg => Assert.Equal("Test", agg.Name)));
            Assert.Contains("exception was thrown", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_assertion_fails()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-13", context,
                stream => new ItemAggregate(stream));

            var assertion = await builder
                .Given(new ItemCreated("ActualName"))
                .Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveState(agg => Assert.Equal("WrongName", agg.Name)));
            Assert.Contains("State assertion failed", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_exception_was_caught_predicate()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-14", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.FailWithException());
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveState(agg => agg.Name == "Test"));
            Assert.Contains("exception was thrown", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_predicate_returns_false()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-15", context,
                stream => new ItemAggregate(stream));

            var assertion = await builder
                .Given(new ItemCreated("ActualName"))
                .Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveState(agg => agg.Name == "WrongName"));
            Assert.Contains("predicate returned false", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_action_is_null()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-16", context,
                stream => new ItemAggregate(stream));
            var assertion = await builder.GivenNoPriorEvents().Then();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                assertion.ShouldHaveState((Action<ItemAggregate>)null!));
        }

        [Fact]
        public async Task Should_throw_when_predicate_is_null()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-17", context,
                stream => new ItemAggregate(stream));
            var assertion = await builder.GivenNoPriorEvents().Then();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                assertion.ShouldHaveState((Func<ItemAggregate, bool>)null!));
        }
    }

    public class ShouldHavePropertyMethod : AggregateAssertionTests
    {
        [Fact]
        public async Task Should_throw_when_exception_was_caught()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-18", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.FailWithException());
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveProperty(agg => agg.Name, "Test"));
            Assert.Contains("exception was thrown", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_property_does_not_match()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-19", context,
                stream => new ItemAggregate(stream));

            var assertion = await builder
                .Given(new ItemCreated("ActualName"))
                .Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveProperty(agg => agg.Name, "ExpectedName"));
            Assert.Contains("Expected property to be 'ExpectedName'", ex.Message);
            Assert.Contains("found 'ActualName'", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_selector_is_null()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-20", context,
                stream => new ItemAggregate(stream));
            var assertion = await builder.GivenNoPriorEvents().Then();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                assertion.ShouldHaveProperty((Func<ItemAggregate, string>)null!, "Test"));
        }
    }

    public class ShouldThrowMethod : AggregateAssertionTests
    {
        [Fact]
        public async Task Should_throw_when_no_exception_was_thrown()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-21", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.Create("Test"));
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldThrow<InvalidOperationException>());
            Assert.Contains("no exception was thrown", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_wrong_exception_type()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-22", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.FailWithException());
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldThrow<ArgumentException>());
            Assert.Contains("ArgumentException", ex.Message);
            Assert.Contains("InvalidOperationException", ex.Message);
        }

        [Fact]
        public async Task ShouldHaveThrown_should_delegate_to_ShouldThrow()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-23", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.FailWithException());
            var assertion = await builderAfterWhen.Then();

            // Act & Assert - ShouldHaveThrown should work the same as ShouldThrow
            assertion.ShouldHaveThrown<InvalidOperationException>();
        }
    }

    public class ShouldNotThrowMethod : AggregateAssertionTests
    {
        [Fact]
        public async Task Should_throw_when_exception_was_thrown()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-24", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.FailWithException());
            var assertion = await builderAfterWhen.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldNotThrow());
            Assert.Contains("no exception to be thrown", ex.Message);
        }
    }

    public class MethodChaining : AggregateAssertionTests
    {
        [Fact]
        public async Task Should_allow_chaining_multiple_assertions()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = AggregateTestBuilder<ItemAggregate>.For(
                "item", "test-25", context,
                stream => new ItemAggregate(stream));

            var builderAfterWhen = await builder
                .When(async agg => await agg.Create("ChainTest"));
            var assertion = await builderAfterWhen.Then();

            // Act & Assert - All methods should return the assertion for chaining
            assertion
                .ShouldHaveAppended<ItemCreated>()
                .ShouldNotHaveAppended<ItemDeleted>()
                .ShouldHaveAppendedCount(1)
                .ShouldHaveAppendedAtLeast(1)
                .ShouldContainEvent<ItemCreated>(e => e.Name == "ChainTest")
                .ShouldHaveState(agg => agg.Name == "ChainTest")
                .ShouldHaveState(agg => Assert.Equal("ChainTest", agg.Name))
                .ShouldHaveProperty(agg => agg.Name, "ChainTest")
                .ShouldNotThrow();
        }
    }
}
