using ErikLieben.FA.ES.Results;

namespace ErikLieben.FA.ES.Tests.Results;

public class ErrorTests
{
    public class Constructor
    {
        [Fact]
        public void Should_store_code_and_message()
        {
            // Arrange & Act
            var sut = new Error("TEST_CODE", "Test message");

            // Assert
            Assert.Equal("TEST_CODE", sut.Code);
            Assert.Equal("Test message", sut.Message);
        }
    }

    public class UnknownField
    {
        [Fact]
        public void Should_have_unknown_code()
        {
            // Arrange & Act
            var sut = Error.Unknown;

            // Assert
            Assert.Equal("UNKNOWN", sut.Code);
            Assert.Equal("An unknown error occurred", sut.Message);
        }
    }

    public class NullValueField
    {
        [Fact]
        public void Should_have_null_value_code()
        {
            // Arrange & Act
            var sut = Error.NullValue;

            // Assert
            Assert.Equal("NULL_VALUE", sut.Code);
            Assert.Equal("A null value was provided", sut.Message);
        }
    }

    public class FromExceptionMethod
    {
        [Fact]
        public void Should_create_error_from_exception()
        {
            // Arrange
            var exception = new InvalidOperationException("Something went wrong");

            // Act
            var sut = Error.FromException(exception);

            // Assert
            Assert.Equal("EXCEPTION.InvalidOperationException", sut.Code);
            Assert.Equal("Something went wrong", sut.Message);
        }

        [Fact]
        public void Should_include_exception_type_name_in_code()
        {
            // Arrange
            var exception = new ArgumentNullException("paramName");

            // Act
            var sut = Error.FromException(exception);

            // Assert
            Assert.StartsWith("EXCEPTION.ArgumentNullException", sut.Code);
        }

        [Fact]
        public void Should_use_exception_message()
        {
            // Arrange
            var message = "Custom exception message";
            var exception = new Exception(message);

            // Act
            var sut = Error.FromException(exception);

            // Assert
            Assert.Equal("EXCEPTION.Exception", sut.Code);
            Assert.Equal(message, sut.Message);
        }
    }

    public class RecordEquality
    {
        [Fact]
        public void Should_be_equal_when_code_and_message_match()
        {
            // Arrange
            var error1 = new Error("CODE", "Message");
            var error2 = new Error("CODE", "Message");

            // Act & Assert
            Assert.Equal(error1, error2);
        }

        [Fact]
        public void Should_not_be_equal_when_code_differs()
        {
            // Arrange
            var error1 = new Error("CODE_A", "Message");
            var error2 = new Error("CODE_B", "Message");

            // Act & Assert
            Assert.NotEqual(error1, error2);
        }

        [Fact]
        public void Should_not_be_equal_when_message_differs()
        {
            // Arrange
            var error1 = new Error("CODE", "Message A");
            var error2 = new Error("CODE", "Message B");

            // Act & Assert
            Assert.NotEqual(error1, error2);
        }
    }
}
