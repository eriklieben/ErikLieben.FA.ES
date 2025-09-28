using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class UnableToFindDocumentFactoryExceptionSerializationTests
{
    public class Constructor
    {
        [Fact]
        public void Should_set_message_and_error_code()
        {
            // Arrange
            var message = "msg";

            // Act
            var sut = new UnableToFindDocumentFactoryException(message);

            // Assert
            Assert.StartsWith("[ELFAES-CFG-0004] ", sut.Message);
            Assert.Equal("ELFAES-CFG-0004", sut.ErrorCode);
        }
    }
}
