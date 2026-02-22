using ErikLieben.FA.ES.EventStreamManagement.Core;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Core;

public class MigrationExceptionTests
{
    public class ConstructorWithMessage
    {
        [Fact]
        public void Should_create_exception_with_message()
        {
            // Arrange
            const string message = "Migration failed";

            // Act
            var sut = new MigrationException(message);

            // Assert
            Assert.Equal(message, sut.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Simple error")]
        [InlineData("Complex error with details: stream X to stream Y failed at position 1234")]
        public void Should_preserve_various_messages(string message)
        {
            // Arrange & Act
            var sut = new MigrationException(message);

            // Assert
            Assert.Equal(message, sut.Message);
        }

        [Fact]
        public void Should_have_null_inner_exception()
        {
            // Arrange
            const string message = "Test";

            // Act
            var sut = new MigrationException(message);

            // Assert
            Assert.Null(sut.InnerException);
        }
    }

    public class ConstructorWithMessageAndInnerException
    {
        [Fact]
        public void Should_create_exception_with_message_and_inner_exception()
        {
            // Arrange
            const string message = "Migration failed";
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var sut = new MigrationException(message, innerException);

            // Assert
            Assert.Equal(message, sut.Message);
            Assert.Same(innerException, sut.InnerException);
        }

        [Fact]
        public void Should_preserve_inner_exception_details()
        {
            // Arrange
            var innerException = new ArgumentException("Invalid argument", "param");

            // Act
            var sut = new MigrationException("Outer message", innerException);

            // Assert
            Assert.IsType<ArgumentException>(sut.InnerException);
            Assert.Equal("Invalid argument (Parameter 'param')", sut.InnerException.Message);
        }

        [Fact]
        public void Should_allow_null_inner_exception()
        {
            // Arrange & Act
            var sut = new MigrationException("Message", null!);

            // Assert
            Assert.Null(sut.InnerException);
        }
    }

    public class InheritanceTests
    {
        [Fact]
        public void Should_inherit_from_Exception()
        {
            // Arrange & Act
            var sut = new MigrationException("test");

            // Assert
            Assert.IsType<MigrationException>(sut);
        }

        [Fact]
        public void Should_be_catchable_as_Exception()
        {
            // Arrange
            Exception? caught = null;

            // Act
            try
            {
                throw new MigrationException("Test");
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            // Assert
            Assert.NotNull(caught);
            Assert.IsType<MigrationException>(caught);
        }
    }
}
