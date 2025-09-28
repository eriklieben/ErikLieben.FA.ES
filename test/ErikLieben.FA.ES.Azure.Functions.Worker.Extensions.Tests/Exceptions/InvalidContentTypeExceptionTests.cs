using System;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class InvalidContentTypeExceptionTests
{
    public class Constructor
    {
        [Fact]
        public void Should_set_message_correctly()
        {
            // Arrange
            var actual = "text/plain";
            var expected = "application/json";
            var inner = new InvalidOperationException("Inner");

            // Act
            var sut = new InvalidContentTypeException(actual, expected, inner);

            // Assert
            Assert.Equal("[ELFAES-VAL-0003] Unexpected content-type 'text/plain'. Only 'application/json' is supported.", sut.Message);
        }

        [Fact]
        public void Should_set_inner_exception_correctly()
        {
            // Arrange
            var actual = "text/plain";
            var expected = "application/json";
            var inner = new InvalidOperationException("Inner");

            // Act
            var sut = new InvalidContentTypeException(actual, expected, inner);

            // Assert
            Assert.Same(inner, sut.InnerException);
        }

        [Fact]
        public void Should_inherit_from_exception()
        {
            // Arrange
            var actual = "text/plain";
            var expected = "application/json";

            // Act
            var sut = new InvalidContentTypeException(actual, expected);

            // Assert
            Assert.IsAssignableFrom<Exception>(sut);
        }
    }
}
