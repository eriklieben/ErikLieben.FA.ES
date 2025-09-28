using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class EsExceptionGetObjectDataTests
{
    public class GetObjectData
    {
        [Fact]
        public void Should_add_error_code_to_serialization_info()
        {
            // Arrange
            var sut = new UnableToFindDocumentFactoryException("oops");
            var info = new SerializationInfo(typeof(UnableToFindDocumentFactoryException), new FormatterConverter());

            // Act
            sut.GetObjectData(info, new StreamingContext(StreamingContextStates.All));

            // Assert
            Assert.Equal("ELFAES-CFG-0004", info.GetString(nameof(EsException.ErrorCode)));
        }
    }
}
