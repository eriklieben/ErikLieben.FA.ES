using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing.Builders;
using ErikLieben.FA.ES.Testing.Time;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Builders;

/// <summary>
/// Tests for TimeTestingExtensions class.
/// </summary>
public partial class TimeTestingExtensionsTests
{
    [EventName("TimestampedEvent")]
    private record TimestampedEvent(DateTimeOffset Timestamp);

    [JsonSerializable(typeof(TimestampedEvent))]
    private partial class TimeExtTimeEventsJsonContext : JsonSerializerContext { }

    private class TimeAggregate : Aggregate
    {
        public TimeAggregate(IEventStream stream) : base(stream)
        {
            stream.EventTypeRegistry.Add(
                typeof(TimestampedEvent),
                "TimestampedEvent",
                TimeExtTimeEventsJsonContext.Default.TimestampedEvent);
        }

        public DateTimeOffset? LastEventTime { get; private set; }

        public async Task RecordTime(DateTimeOffset time)
        {
            await Stream.Session(context =>
                Fold(context.Append(new TimestampedEvent(time))));
        }

        private void When(TimestampedEvent @event) => LastEventTime = @event.Timestamp;

        public override void Fold(IEvent @event)
        {
            if (@event is JsonEvent jsonEvent && jsonEvent.EventType == "TimestampedEvent")
            {
                var created = JsonSerializer.Deserialize(jsonEvent.Payload, TimeExtTimeEventsJsonContext.Default.TimestampedEvent);
                if (created != null) When(created);
            }
        }
    }

    private static TestContext CreateTestContextWithClock()
    {
        var provider = new SimpleServiceProvider();
        var testClock = new TestClock();
        return TestSetup.GetContext(provider, testClock, _ => typeof(DummyFactory));
    }

    private static TestContext CreateTestContextWithoutClock()
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
        public IBase Create(IEventStream eventStream) => new TimeAggregate(eventStream);
        public IBase Create(ErikLieben.FA.ES.Documents.IObjectDocument document) =>
            throw new NotImplementedException();
    }

    public class AtTimeMethod : TimeTestingExtensionsTests
    {
        [Fact]
        public void Should_set_clock_to_specified_time()
        {
            // Arrange
            var context = CreateTestContextWithClock();
            var targetTime = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);
            var builder = AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-1", context,
                stream => new TimeAggregate(stream));

            // Act
            var result = builder.AtTime(targetTime);

            // Assert
            Assert.Same(builder, result);
            Assert.Equal(targetTime, context.TestClock!.Now);
        }

        [Fact]
        public async Task Should_set_clock_from_task()
        {
            // Arrange
            var context = CreateTestContextWithClock();
            var targetTime = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);
            var builderTask = Task.FromResult(AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-2", context,
                stream => new TimeAggregate(stream)));

            // Act
            var result = await builderTask.AtTime(targetTime);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(targetTime, context.TestClock!.Now);
        }

        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            // Arrange
            var targetTime = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);
            AggregateTestBuilder<TimeAggregate>? nullBuilder = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                nullBuilder!.AtTime(targetTime));
        }

        [Fact]
        public void Should_throw_when_no_test_clock_configured()
        {
            // Arrange
            var context = CreateTestContextWithoutClock();
            var builder = AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-3", context,
                stream => new TimeAggregate(stream));
            var targetTime = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                builder.AtTime(targetTime));
            Assert.Contains("Test clock is not configured", ex.Message);
        }
    }

    public class AdvanceTimeByMethod : TimeTestingExtensionsTests
    {
        [Fact]
        public void Should_advance_clock_by_duration()
        {
            // Arrange
            var context = CreateTestContextWithClock();
            var initialTime = new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.Zero);
            context.TestClock!.SetTime(initialTime);
            var builder = AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-4", context,
                stream => new TimeAggregate(stream));

            // Act
            var result = builder.AdvanceTimeBy(TimeSpan.FromHours(2));

            // Assert
            Assert.Same(builder, result);
            Assert.Equal(initialTime.AddHours(2), context.TestClock.Now);
        }

        [Fact]
        public async Task Should_advance_clock_from_task()
        {
            // Arrange
            var context = CreateTestContextWithClock();
            var initialTime = new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.Zero);
            context.TestClock!.SetTime(initialTime);
            var builderTask = Task.FromResult(AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-5", context,
                stream => new TimeAggregate(stream)));

            // Act
            var result = await builderTask.AdvanceTimeBy(TimeSpan.FromMinutes(30));

            // Assert
            Assert.NotNull(result);
            Assert.Equal(initialTime.AddMinutes(30), context.TestClock.Now);
        }

        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            // Arrange
            AggregateTestBuilder<TimeAggregate>? nullBuilder = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                nullBuilder!.AdvanceTimeBy(TimeSpan.FromHours(1)));
        }

        [Fact]
        public void Should_throw_when_no_test_clock_configured()
        {
            // Arrange
            var context = CreateTestContextWithoutClock();
            var builder = AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-6", context,
                stream => new TimeAggregate(stream));

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                builder.AdvanceTimeBy(TimeSpan.FromHours(1)));
            Assert.Contains("Test clock is not configured", ex.Message);
        }
    }

    public class FreezeTimeMethod : TimeTestingExtensionsTests
    {
        [Fact]
        public void Should_freeze_clock()
        {
            // Arrange
            var context = CreateTestContextWithClock();
            var builder = AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-7", context,
                stream => new TimeAggregate(stream));

            // Act
            var result = builder.FreezeTime();

            // Assert
            Assert.Same(builder, result);
            Assert.True(context.TestClock!.IsFrozen);
        }

        [Fact]
        public async Task Should_freeze_clock_from_task()
        {
            // Arrange
            var context = CreateTestContextWithClock();
            var builderTask = Task.FromResult(AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-8", context,
                stream => new TimeAggregate(stream)));

            // Act
            var result = await builderTask.FreezeTime();

            // Assert
            Assert.NotNull(result);
            Assert.True(context.TestClock!.IsFrozen);
        }

        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            // Arrange
            AggregateTestBuilder<TimeAggregate>? nullBuilder = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                nullBuilder!.FreezeTime());
        }

        [Fact]
        public void Should_throw_when_no_test_clock_configured()
        {
            // Arrange
            var context = CreateTestContextWithoutClock();
            var builder = AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-9", context,
                stream => new TimeAggregate(stream));

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                builder.FreezeTime());
            Assert.Contains("Test clock is not configured", ex.Message);
        }
    }

    public class UnfreezeTimeMethod : TimeTestingExtensionsTests
    {
        [Fact]
        public void Should_unfreeze_clock()
        {
            // Arrange
            var context = CreateTestContextWithClock();
            context.TestClock!.Freeze();
            var builder = AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-10", context,
                stream => new TimeAggregate(stream));

            // Act
            var result = builder.UnfreezeTime();

            // Assert
            Assert.Same(builder, result);
            Assert.False(context.TestClock.IsFrozen);
        }

        [Fact]
        public async Task Should_unfreeze_clock_from_task()
        {
            // Arrange
            var context = CreateTestContextWithClock();
            context.TestClock!.Freeze();
            var builderTask = Task.FromResult(AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-11", context,
                stream => new TimeAggregate(stream)));

            // Act
            var result = await builderTask.UnfreezeTime();

            // Assert
            Assert.NotNull(result);
            Assert.False(context.TestClock.IsFrozen);
        }

        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            // Arrange
            AggregateTestBuilder<TimeAggregate>? nullBuilder = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                nullBuilder!.UnfreezeTime());
        }

        [Fact]
        public void Should_throw_when_no_test_clock_configured()
        {
            // Arrange
            var context = CreateTestContextWithoutClock();
            var builder = AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-12", context,
                stream => new TimeAggregate(stream));

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                builder.UnfreezeTime());
            Assert.Contains("Test clock is not configured", ex.Message);
        }
    }

    public class ChainedTimeMethods : TimeTestingExtensionsTests
    {
        [Fact]
        public void Should_allow_chaining_time_operations()
        {
            // Arrange
            var context = CreateTestContextWithClock();
            var initialTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var builder = AggregateTestBuilder<TimeAggregate>.For(
                "time", "test-13", context,
                stream => new TimeAggregate(stream));

            // Act
            var result = builder
                .AtTime(initialTime)
                .FreezeTime()
                .AdvanceTimeBy(TimeSpan.FromDays(1))
                .UnfreezeTime();

            // Assert
            Assert.Same(builder, result);
            Assert.Equal(initialTime.AddDays(1), context.TestClock!.Now);
            Assert.False(context.TestClock.IsFrozen);
        }
    }
}
