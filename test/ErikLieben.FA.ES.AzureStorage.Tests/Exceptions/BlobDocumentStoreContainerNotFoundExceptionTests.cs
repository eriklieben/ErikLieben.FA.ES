using ErikLieben.FA.ES.AzureStorage.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Exceptions
{
    public class BlobDocumentStoreContainerNotFoundExceptionTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_set_message_correctly()
            {
                // Arrange
                string expectedMessage = "Test error message";
                var innerException = new InvalidOperationException("Inner exception");

                // Act
                var sut = new BlobDocumentStoreContainerNotFoundException(expectedMessage, innerException);

                // Assert
                Assert.Equal("[ELFAES-FILE-0002] " + expectedMessage, sut.Message);
            }

            [Fact]
            public void Should_set_inner_exception_correctly()
            {
                // Arrange
                string message = "Test error message";
                var expectedInnerException = new InvalidOperationException("Inner exception");

                // Act
                var sut = new BlobDocumentStoreContainerNotFoundException(message, expectedInnerException);

                // Assert
                Assert.Same(expectedInnerException, sut.InnerException);
            }

            [Fact]
            public void Should_inherit_from_exception()
            {
                // Arrange
                string message = "Test error message";
                var innerException = new InvalidOperationException("Inner exception");

                // Act
                var sut = new BlobDocumentStoreContainerNotFoundException(message, innerException);

                // Assert
                Assert.IsType<Exception>(sut, exactMatch: false);
            }
        }
    }
}
