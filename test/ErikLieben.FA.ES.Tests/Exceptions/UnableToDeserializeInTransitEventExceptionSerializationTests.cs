using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class UnableToDeserializeInTransitEventExceptionSerializationTests
{
    public class Constructor
    {
        [Fact]
        public void Should_set_default_message_and_code()
        {
            // Arrange & Act
            var sut = new UnableToDeserializeInTransitEventException();

            // Assert
            Assert.Equal("[ELFAES-VAL-0001] Unable to deserialize to event, value is 'null'", sut.Message);
            Assert.Equal("ELFAES-VAL-0001", sut.ErrorCode);
        }
    }
}
