using ErikLieben.FA.ES.AzureStorage.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Exceptions;

public class TableDocumentStoreTableNotFoundExceptionTests
{
    public class Constructor
    {
        [Fact]
        public void Should_set_message_when_provided()
        {
            // Arrange
            var errorMessage = "Table not found";

            // Act
            var sut = new TableDocumentStoreTableNotFoundException(errorMessage);

            // Assert
            Assert.Equal("[ELFAES-EXT-0011] " + errorMessage, sut.Message);
        }

        [Fact]
        public void Should_inherit_from_exception()
        {
            // Arrange
            var errorMessage = "Table not found";

            // Act
            var sut = new TableDocumentStoreTableNotFoundException(errorMessage);

            // Assert
            Assert.IsType<Exception>(sut, exactMatch: false);
        }

        [Theory]
        [InlineData(null!)]
        [InlineData("")]
        [InlineData("The table 'eventstream' was not found in the storage account")]
        public void Should_accept_any_message_parameter(string? message)
        {
            // Act & Assert
            var exception = Record.Exception(() => new TableDocumentStoreTableNotFoundException(message!));

            Assert.Null(exception);
        }

        [Fact]
        public void Should_pass_message_to_base_exception()
        {
            // Arrange
            var errorMessage = "Table not found";

            // Act
            Exception sut = new TableDocumentStoreTableNotFoundException(errorMessage);

            // Assert
            Assert.Equal("[ELFAES-EXT-0011] " + errorMessage, sut.Message);
        }
    }

    public class ConstructorWithInnerException
    {
        [Fact]
        public void Should_set_message_and_inner_exception()
        {
            // Arrange
            var errorMessage = "Table not found";
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var sut = new TableDocumentStoreTableNotFoundException(errorMessage, innerException);

            // Assert
            Assert.Equal("[ELFAES-EXT-0011] " + errorMessage, sut.Message);
            Assert.Same(innerException, sut.InnerException);
        }

        [Fact]
        public void Should_allow_null_inner_exception()
        {
            // Arrange
            var errorMessage = "Table not found";

            // Act
            var sut = new TableDocumentStoreTableNotFoundException(errorMessage, null!);

            // Assert
            Assert.Null(sut.InnerException);
        }
    }
}
