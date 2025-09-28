using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class AggregateJsonTypeInfoNotSetExceptionSerializationTests
{
    public class Constructor
    {
        [Fact]
        public void Should_set_default_message()
        {
            // Arrange & Act
            var sut = new AggregateJsonTypeInfoNotSetException();

            // Assert
            Assert.Equal("[ELFAES-CFG-0001] Aggregate JsonInfo type should be set to deserialize the aggregate type", sut.Message);
            Assert.Equal("ELFAES-CFG-0001", sut.ErrorCode);
        }
    }
}
