using ErikLieben.FA.ES.EventStreamManagement.Transformation;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Transformation;

public class EventFilteredExceptionTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_exception_with_message()
        {
            // Arrange
            const string message = "Event was filtered";

            // Act
            var sut = new EventFilteredException(message);

            // Assert
            Assert.NotNull(sut);
            Assert.Equal(message, sut.Message);
        }

        [Theory]
        [InlineData("Event TestEvent v1 was filtered out")]
        [InlineData("Custom filter message")]
        [InlineData("")]
        public void Should_preserve_various_messages(string message)
        {
            // Arrange & Act
            var sut = new EventFilteredException(message);

            // Assert
            Assert.Equal(message, sut.Message);
        }
    }

    public class InheritanceTests
    {
        [Fact]
        public void Should_inherit_from_Exception()
        {
            // Arrange & Act
            var sut = new EventFilteredException("test");

            // Assert
            Assert.IsAssignableFrom<Exception>(sut);
        }
    }
}
