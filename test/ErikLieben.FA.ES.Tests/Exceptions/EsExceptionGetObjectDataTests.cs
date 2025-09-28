using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class EsExceptionGetObjectDataTests
{
    public class Basic
    {
        [Fact]
        public void Should_expose_error_code_property()
        {
            // Arrange & Act
            var sut = new UnableToFindDocumentFactoryException("oops");

            // Assert
            Assert.Equal("ELFAES-CFG-0004", sut.ErrorCode);
        }
    }
}
