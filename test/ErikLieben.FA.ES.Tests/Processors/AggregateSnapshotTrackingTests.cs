using ErikLieben.FA.ES.Processors;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Processors;

public class AggregateSnapshotTrackingTests
{
    public class InitialState
    {
        [Fact]
        public void Should_have_zero_events_since_last_snapshot()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Assert
            Assert.Equal(0, sut.EventsSinceLastSnapshot);
        }

        [Fact]
        public void Should_have_zero_total_events_processed()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Assert
            Assert.Equal(0, sut.TotalEventsProcessed);
        }

        [Fact]
        public void Should_have_null_last_snapshot_version()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Assert
            Assert.Null(sut.LastSnapshotVersion);
        }
    }

    public class RecordEventsAppendedMethod
    {
        [Fact]
        public void Should_increment_events_since_last_snapshot()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Act
            sut.RecordEventsAppended(3);

            // Assert
            Assert.Equal(3, sut.EventsSinceLastSnapshot);
        }

        [Fact]
        public void Should_increment_total_events_processed()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Act
            sut.RecordEventsAppended(3);

            // Assert
            Assert.Equal(3, sut.TotalEventsProcessed);
        }

        [Fact]
        public void Should_accumulate_across_multiple_calls()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Act
            sut.RecordEventsAppended(3);
            sut.RecordEventsAppended(5);

            // Assert
            Assert.Equal(8, sut.EventsSinceLastSnapshot);
            Assert.Equal(8, sut.TotalEventsProcessed);
        }
    }

    public class RecordEventsFoldedMethod
    {
        [Fact]
        public void Should_increment_total_events_processed()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Act
            sut.RecordEventsFolded(4);

            // Assert
            Assert.Equal(4, sut.TotalEventsProcessed);
        }

        [Fact]
        public void Should_increment_events_since_last_snapshot()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Act
            sut.RecordEventsFolded(4);

            // Assert
            Assert.Equal(4, sut.EventsSinceLastSnapshot);
        }

        [Fact]
        public void Should_accumulate_with_appended_events()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Act
            sut.RecordEventsAppended(3);
            sut.RecordEventsFolded(2);

            // Assert
            Assert.Equal(5, sut.EventsSinceLastSnapshot);
            Assert.Equal(5, sut.TotalEventsProcessed);
        }
    }

    public class RecordSnapshotCreatedMethod
    {
        [Fact]
        public void Should_set_last_snapshot_version()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Act
            sut.RecordSnapshotCreated(10);

            // Assert
            Assert.Equal(10, sut.LastSnapshotVersion);
        }

        [Fact]
        public void Should_reset_events_since_last_snapshot()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);
            sut.RecordEventsAppended(5);

            // Act
            sut.RecordSnapshotCreated(10);

            // Assert
            Assert.Equal(0, sut.EventsSinceLastSnapshot);
        }

        [Fact]
        public void Should_not_reset_total_events_processed()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);
            sut.RecordEventsAppended(5);

            // Act
            sut.RecordSnapshotCreated(10);

            // Assert
            Assert.Equal(5, sut.TotalEventsProcessed);
        }

        [Fact]
        public void Should_update_last_snapshot_version_on_subsequent_calls()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Act
            sut.RecordSnapshotCreated(10);
            sut.RecordEventsAppended(3);
            sut.RecordSnapshotCreated(13);

            // Assert
            Assert.Equal(13, sut.LastSnapshotVersion);
            Assert.Equal(0, sut.EventsSinceLastSnapshot);
        }
    }

    public class ResetFromSnapshotMethod
    {
        [Fact]
        public void Should_set_last_snapshot_version()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Act
            sut.ResetFromSnapshot(20);

            // Assert
            Assert.Equal(20, sut.LastSnapshotVersion);
        }

        [Fact]
        public void Should_reset_events_since_last_snapshot_to_zero()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);
            sut.RecordEventsAppended(10);

            // Act
            sut.ResetFromSnapshot(20);

            // Assert
            Assert.Equal(0, sut.EventsSinceLastSnapshot);
        }

        [Fact]
        public void Should_set_total_events_processed_to_snapshot_version()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Act
            sut.ResetFromSnapshot(20);

            // Assert
            Assert.Equal(20, sut.TotalEventsProcessed);
        }

        [Fact]
        public void Should_allow_normal_tracking_after_reset()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();
            var sut = new TestAggregate(stream);

            // Act
            sut.ResetFromSnapshot(20);
            sut.RecordEventsAppended(5);

            // Assert
            Assert.Equal(20, sut.LastSnapshotVersion);
            Assert.Equal(5, sut.EventsSinceLastSnapshot);
            Assert.Equal(25, sut.TotalEventsProcessed);
        }
    }

    public class EventStreamProperty
    {
        [Fact]
        public void Should_expose_event_stream_via_public_property()
        {
            // Arrange
            var stream = Substitute.For<IEventStream>();

            // Act
            var sut = new TestAggregate(stream);

            // Assert
            Assert.Same(stream, sut.EventStream);
        }
    }

    private class TestAggregate : Aggregate
    {
        public TestAggregate(IEventStream stream) : base(stream)
        {
        }
    }
}
