using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.Builders;
using ErikLieben.FA.ES.Testing.InMemory;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Builders;

public partial class AggregateTestBuilderTests
{
    [EventName("TestCreated")]
    private record TestCreated(string Name);

    [EventName("TestUpdated")]
    private record TestUpdated(string NewName);

    [EventName("TestCompleted")]
    private record TestCompleted(string Outcome);

    [JsonSerializable(typeof(TestCreated))]
    [JsonSerializable(typeof(TestUpdated))]
    [JsonSerializable(typeof(TestCompleted))]
    private partial class TestEventsJsonContext : JsonSerializerContext { }

    private class TestAggregate : Aggregate
    {
        public TestAggregate(IEventStream stream) : base(stream)
        {
            // Register event types for this aggregate
            stream.EventTypeRegistry.Add(
                typeof(TestCreated),
                "TestCreated",
                TestEventsJsonContext.Default.TestCreated);
            stream.EventTypeRegistry.Add(
                typeof(TestUpdated),
                "TestUpdated",
                TestEventsJsonContext.Default.TestUpdated);
            stream.EventTypeRegistry.Add(
                typeof(TestCompleted),
                "TestCompleted",
                TestEventsJsonContext.Default.TestCompleted);
        }

        public string? Name { get; private set; }
        public bool IsCompleted { get; private set; }
        public string? Outcome { get; private set; }

        public async Task Create(string name)
        {
            await Stream.Session(context =>
                Fold(context.Append(new TestCreated(name))));
        }

        public async Task Update(string newName)
        {
            await Stream.Session(context =>
                Fold(context.Append(new TestUpdated(newName))));
        }

        public async Task Complete(string outcome)
        {
            await Stream.Session(context =>
                Fold(context.Append(new TestCompleted(outcome))));
        }

        public async Task FailingCommand()
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("Command failed intentionally");
        }

        private void When(TestCreated @event)
        {
            Name = @event.Name;
        }

        private void When(TestUpdated @event)
        {
            Name = @event.NewName;
        }

        private void When(TestCompleted @event)
        {
            IsCompleted = true;
            Outcome = @event.Outcome;
        }

        // Override Fold to dispatch events from the stream (simulates generated code)
        public override void Fold(IEvent @event)
        {
            if (@event is JsonEvent jsonEvent)
            {
                switch (jsonEvent.EventType)
                {
                    case "TestCreated":
                        var created = JsonSerializer.Deserialize(
                            jsonEvent.Payload,
                            TestEventsJsonContext.Default.TestCreated);
                        if (created != null) When(created);
                        break;
                    case "TestUpdated":
                        var updated = JsonSerializer.Deserialize(
                            jsonEvent.Payload,
                            TestEventsJsonContext.Default.TestUpdated);
                        if (updated != null) When(updated);
                        break;
                    case "TestCompleted":
                        var completed = JsonSerializer.Deserialize(
                            jsonEvent.Payload,
                            TestEventsJsonContext.Default.TestCompleted);
                        if (completed != null) When(completed);
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
        public IBase Create(IEventStream eventStream) => new TestAggregate(eventStream);
        public IBase Create(ErikLieben.FA.ES.Documents.IObjectDocument document) => throw new NotImplementedException();
    }

    [Fact]
    public void For_ShouldCreateBuilder_WithValidParameters()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "123",
            context,
            stream => new TestAggregate(stream));

        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public async Task Given_ShouldSetupInitialState_WithDomainEvents()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "given-test-1",
            context,
            stream => new TestAggregate(stream));

        // Act
        var aggregate = await builder
            .Given(new TestCreated("Initial Name"))
            .ThenAggregate();

        // Assert
        Assert.Equal("Initial Name", aggregate.Name);
    }

    [Fact]
    public async Task Given_ShouldSetupInitialState_WithMultipleEvents()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "given-test-2",
            context,
            stream => new TestAggregate(stream));

        // Act
        var aggregate = await builder
            .Given(
                new TestCreated("First Name"),
                new TestUpdated("Updated Name"))
            .ThenAggregate();

        // Assert
        Assert.Equal("Updated Name", aggregate.Name);
    }

    [Fact]
    public async Task When_ShouldExecuteAsyncCommand()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "when-test-1",
            context,
            stream => new TestAggregate(stream));

        // Act
        var builderAfterWhen = await builder
            .When(async agg => await agg.Create("New Aggregate"));
        var assertion = await builderAfterWhen.Then();

        // Assert
        assertion.ShouldHaveAppended<TestCreated>();
    }

    [Fact]
    public async Task When_ShouldCaptureException_WhenCommandFails()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "when-test-2",
            context,
            stream => new TestAggregate(stream));

        // Act
        var builderAfterWhen = await builder
            .When(async agg => await agg.FailingCommand());
        var assertion = await builderAfterWhen.Then();

        // Assert
        assertion.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task Then_ShouldReturnAssertionWithAggregate()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "then-test-1",
            context,
            stream => new TestAggregate(stream));

        // Act
        var assertion = await builder
            .Given(new TestCreated("Test"))
            .Then();

        // Assert
        Assert.NotNull(assertion.Aggregate);
        Assert.Equal("Test", assertion.Aggregate.Name);
    }

    [Fact]
    public async Task GivenNoPriorEvents_ShouldReturnBuilder()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "no-events-test",
            context,
            stream => new TestAggregate(stream));

        // Act
        var aggregate = await builder
            .GivenNoPriorEvents()
            .ThenAggregate();

        // Assert
        Assert.Null(aggregate.Name);
        Assert.False(aggregate.IsCompleted);
    }

    [Fact]
    public async Task ShouldHaveAppended_WithPayload_ShouldPass_WhenEventMatches()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "appended-test-1",
            context,
            stream => new TestAggregate(stream));

        // Act
        var builderAfterWhen = await builder
            .When(async agg => await agg.Create("My Aggregate"));
        var assertion = await builderAfterWhen.Then();

        // Assert - should not throw
        assertion.ShouldHaveAppended(new TestCreated("My Aggregate"));
    }

    [Fact]
    public async Task ShouldNotHaveAppended_ShouldPass_WhenEventNotPresent()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "not-appended-test",
            context,
            stream => new TestAggregate(stream));

        // Act
        var builderAfterWhen = await builder
            .When(async agg => await agg.Create("Test"));
        var assertion = await builderAfterWhen.Then();

        // Assert
        assertion.ShouldNotHaveAppended<TestCompleted>();
    }

    [Fact]
    public async Task ShouldHaveAppendedCount_ShouldPass_WhenCountMatches()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "count-test",
            context,
            stream => new TestAggregate(stream));

        // Act
        var builderAfterWhen = await builder
            .When(async agg => await agg.Create("Test"));
        var assertion = await builderAfterWhen.Then();

        // Assert
        assertion.ShouldHaveAppendedCount(1);
    }

    [Fact]
    public async Task ShouldHaveState_WithAction_ShouldPass_WhenStateMatches()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "state-action-test",
            context,
            stream => new TestAggregate(stream));

        // Act
        var assertion = await builder
            .Given(new TestCreated("Test Name"))
            .Then();

        // Assert
        assertion.ShouldHaveState(agg => Assert.Equal("Test Name", agg.Name));
    }

    [Fact]
    public async Task ShouldHaveState_WithPredicate_ShouldPass_WhenPredicateReturnsTrue()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "state-predicate-test",
            context,
            stream => new TestAggregate(stream));

        // Act
        var assertion = await builder
            .Given(new TestCreated("Test Name"))
            .Then();

        // Assert
        assertion.ShouldHaveState(agg => agg.Name == "Test Name");
    }

    [Fact]
    public async Task ShouldHaveProperty_ShouldPass_WhenPropertyMatches()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "property-test",
            context,
            stream => new TestAggregate(stream));

        // Act
        var assertion = await builder
            .Given(new TestCreated("Property Test"))
            .Then();

        // Assert
        assertion.ShouldHaveProperty(agg => agg.Name, "Property Test");
    }

    [Fact]
    public async Task ShouldNotThrow_ShouldPass_WhenNoExceptionThrown()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "no-throw-test",
            context,
            stream => new TestAggregate(stream));

        // Act
        var builderAfterWhen = await builder
            .When(async agg => await agg.Create("Test"));
        var assertion = await builderAfterWhen.Then();

        // Assert
        assertion.ShouldNotThrow();
    }

    [Fact]
    public async Task ShouldNotHaveAppendedAnyEvents_ShouldPass_WhenNoEventsAppended()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "no-events-appended-test",
            context,
            stream => new TestAggregate(stream));

        // Act
        var assertion = await builder
            .GivenNoPriorEvents()
            .Then();

        // Assert
        assertion.ShouldNotHaveAppendedAnyEvents();
    }

    [Fact]
    public async Task FluentThen_ShouldAllowInlineAssertions()
    {
        // Arrange
        var context = CreateTestContext();

        // Act & Assert - using extension method for fluent syntax
        await AggregateTestBuilder<TestAggregate>.For(
            "test",
            "fluent-test",
            context,
            stream => new TestAggregate(stream))
            .When(async agg => await agg.Create("Fluent Test"))
            .Then(assertion =>
            {
                assertion.ShouldHaveAppended<TestCreated>();
                assertion.ShouldHaveProperty(agg => agg.Name, "Fluent Test");
            });
    }

    [Fact]
    public async Task ShouldContainEvent_ShouldPass_WhenEventMatchesPredicate()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "contain-event-test",
            context,
            stream => new TestAggregate(stream));

        // Act
        var builderAfterWhen = await builder
            .When(async agg => await agg.Create("Predicate Test"));
        var assertion = await builderAfterWhen.Then();

        // Assert
        assertion.ShouldContainEvent<TestCreated>(e => e.Name.Contains("Predicate"));
    }

    [Fact]
    public async Task ShouldHaveAppendedAtLeast_ShouldPass_WhenMinimumMet()
    {
        // Arrange
        var context = CreateTestContext();
        var builder = AggregateTestBuilder<TestAggregate>.For(
            "test",
            "at-least-test",
            context,
            stream => new TestAggregate(stream));

        // Act
        var builderAfterWhen = await builder
            .When(async agg => await agg.Create("Test"));
        var assertion = await builderAfterWhen.Then();

        // Assert
        assertion.ShouldHaveAppendedAtLeast(1);
    }
}
