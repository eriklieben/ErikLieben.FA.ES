using System;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class InvalidBindingSourceExceptionTests
{
    public class Constructor
    {
        [Fact]
        public void Should_set_message_correctly()
        {
            // Arrange
            var actual = "Body";
            var expected = "Query";
            var inner = new InvalidOperationException("Inner");

            // Act
            var sut = new InvalidBindingSourceException(actual, expected, inner);

            // Assert
            Assert.Equal("[ELFAES-VAL-0002] Unexpected binding source 'Body'. Only 'Query' is supported.", sut.Message);
        }

        [Fact]
        public void Should_set_inner_exception_correctly()
        {
            // Arrange
            var actual = "Body";
            var expected = "Query";
            var inner = new InvalidOperationException("Inner");

            // Act
            var sut = new InvalidBindingSourceException(actual, expected, inner);

            // Assert
            Assert.Same(inner, sut.InnerException);
        }

        [Fact]
        public void Should_inherit_from_exception()
        {
            // Arrange
            var actual = "Body";
            var expected = "Query";

            // Act
            var sut = new InvalidBindingSourceException(actual, expected);

            // Assert
            Assert.IsAssignableFrom<Exception>(sut);
        }
    }
}
