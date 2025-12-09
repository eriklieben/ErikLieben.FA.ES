using ErikLieben.FA.ES.EventStreamManagement.Verification;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Verification;

public class TransformationFailureTests
{
    public class Properties
    {
        [Fact]
        public void Should_have_event_version()
        {
            // Arrange & Act
            var sut = new TransformationFailure
            {
                EventVersion = 42,
                EventName = "TestEvent",
                Error = "Test error"
            };

            // Assert
            Assert.Equal(42, sut.EventVersion);
        }

        [Fact]
        public void Should_have_event_name()
        {
            // Arrange & Act
            var sut = new TransformationFailure
            {
                EventVersion = 1,
                EventName = "UserCreatedEvent",
                Error = "Test error"
            };

            // Assert
            Assert.Equal("UserCreatedEvent", sut.EventName);
        }

        [Fact]
        public void Should_have_error()
        {
            // Arrange & Act
            var sut = new TransformationFailure
            {
                EventVersion = 1,
                EventName = "TestEvent",
                Error = "Serialization failed: invalid JSON"
            };

            // Assert
            Assert.Equal("Serialization failed: invalid JSON", sut.Error);
        }

        [Fact]
        public void Should_default_event_version_to_zero()
        {
            // Arrange & Act
            var sut = new TransformationFailure
            {
                EventName = "TestEvent",
                Error = "Test error"
            };

            // Assert
            Assert.Equal(0, sut.EventVersion);
        }
    }

    public class RecordEquality
    {
        [Fact]
        public void Should_be_equal_when_same_values()
        {
            // Arrange
            var failure1 = new TransformationFailure
            {
                EventVersion = 1,
                EventName = "TestEvent",
                Error = "Error message"
            };
            var failure2 = new TransformationFailure
            {
                EventVersion = 1,
                EventName = "TestEvent",
                Error = "Error message"
            };

            // Assert
            Assert.Equal(failure1, failure2);
        }

        [Fact]
        public void Should_not_be_equal_when_different_version()
        {
            // Arrange
            var failure1 = new TransformationFailure
            {
                EventVersion = 1,
                EventName = "TestEvent",
                Error = "Error message"
            };
            var failure2 = new TransformationFailure
            {
                EventVersion = 2,
                EventName = "TestEvent",
                Error = "Error message"
            };

            // Assert
            Assert.NotEqual(failure1, failure2);
        }

        [Fact]
        public void Should_not_be_equal_when_different_event_name()
        {
            // Arrange
            var failure1 = new TransformationFailure
            {
                EventVersion = 1,
                EventName = "EventA",
                Error = "Error message"
            };
            var failure2 = new TransformationFailure
            {
                EventVersion = 1,
                EventName = "EventB",
                Error = "Error message"
            };

            // Assert
            Assert.NotEqual(failure1, failure2);
        }

        [Fact]
        public void Should_not_be_equal_when_different_error()
        {
            // Arrange
            var failure1 = new TransformationFailure
            {
                EventVersion = 1,
                EventName = "TestEvent",
                Error = "Error A"
            };
            var failure2 = new TransformationFailure
            {
                EventVersion = 1,
                EventName = "TestEvent",
                Error = "Error B"
            };

            // Assert
            Assert.NotEqual(failure1, failure2);
        }
    }
}
