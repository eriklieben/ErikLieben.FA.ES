using ErikLieben.FA.ES.AzureStorage.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Exceptions
{
    public class BlobDataStoreProcessingExceptionTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_set_message_when_provided()
            {
                // Arrange
                var errorMessage = "Test error message";

                // Act
                var sut = new BlobDataStoreProcessingException(errorMessage);

                // Assert
                Assert.Equal("[ELFAES-EXT-0001] " + errorMessage, sut.Message);
            }

            [Fact]
            public void Should_inherit_from_exception()
            {
                // Arrange
                var errorMessage = "Test error message";

                // Act
                var sut = new BlobDataStoreProcessingException(errorMessage);

                // Assert
                Assert.IsType<Exception>(sut, exactMatch: false);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("Custom error message")]
            public void Should_accept_any_message_parameter(string message)
            {
                // Act & Assert
                var exception = Record.Exception(() => new BlobDataStoreProcessingException(message));

                Assert.Null(exception);
            }

            [Fact]
            public void Should_pass_message_to_base_exception()
            {
                // Arrange
                var errorMessage = "Test error message";

                // Act
                Exception sut = new BlobDataStoreProcessingException(errorMessage);

                // Assert
                Assert.Equal("[ELFAES-EXT-0001] " + errorMessage, sut.Message);
            }
        }
    }
}
