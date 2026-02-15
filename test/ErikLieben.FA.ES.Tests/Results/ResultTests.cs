using ErikLieben.FA.ES.Results;

namespace ErikLieben.FA.ES.Tests.Results;

public class ResultTests
{
    public class SuccessMethod
    {
        [Fact]
        public void Should_create_successful_result()
        {
            // Arrange & Act
            var sut = Result.Success();

            // Assert
            Assert.True(sut.IsSuccess);
            Assert.False(sut.IsFailure);
            Assert.Null(sut.Error);
        }
    }

    public class FailureMethod
    {
        [Fact]
        public void Should_create_failed_result_with_error()
        {
            // Arrange
            var error = new Error("TEST", "Test error");

            // Act
            var sut = Result.Failure(error);

            // Assert
            Assert.False(sut.IsSuccess);
            Assert.True(sut.IsFailure);
            Assert.Equal(error, sut.Error);
        }

        [Fact]
        public void Should_create_failed_result_with_code_and_message()
        {
            // Arrange & Act
            var sut = Result.Failure("ERR_CODE", "Error message");

            // Assert
            Assert.False(sut.IsSuccess);
            Assert.True(sut.IsFailure);
            Assert.NotNull(sut.Error);
            Assert.Equal("ERR_CODE", sut.Error!.Code);
            Assert.Equal("Error message", sut.Error.Message);
        }
    }

    public class ImplicitConversion
    {
        [Fact]
        public void Should_convert_error_to_failed_result()
        {
            // Arrange
            var error = new Error("IMPLICIT", "Implicit error");

            // Act
            Result sut = error;

            // Assert
            Assert.True(sut.IsFailure);
            Assert.Equal(error, sut.Error);
        }
    }
}
