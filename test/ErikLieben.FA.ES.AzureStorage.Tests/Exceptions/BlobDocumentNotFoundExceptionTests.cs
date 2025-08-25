using ErikLieben.FA.ES.AzureStorage.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Exceptions
{
    public class BlobDocumentNotFoundExceptionTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_pass_message_to_base_exception()
            {
                // Arrange
                var message = "Document not found";
                var innerException = new Exception("Inner exception");

                // Act
                var sut = new BlobDocumentNotFoundException(message, innerException);

                // Assert
                Assert.Equal(message, sut.Message);
            }

            [Fact]
            public void Should_pass_inner_exception_to_base_exception()
            {
                // Arrange
                var message = "Document not found";
                var innerException = new Exception("Inner exception");

                // Act
                var sut = new BlobDocumentNotFoundException(message, innerException);

                // Assert
                Assert.Same(innerException, sut.InnerException);
            }

            [Fact]
            public void Should_inherit_from_exception()
            {
                // Arrange
                var message = "Document not found";
                var innerException = new Exception("Inner exception");

                // Act
                var sut = new BlobDocumentNotFoundException(message, innerException);

                // Assert
                Assert.IsAssignableFrom<Exception>(sut);
            }
        }
    }
}
