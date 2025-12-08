#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios
#pragma warning disable xUnit1012 // Null values in [InlineData] for non-nullable parameters - testing null scenarios

using System;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using Xunit;

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
            [InlineData(null!)]
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
