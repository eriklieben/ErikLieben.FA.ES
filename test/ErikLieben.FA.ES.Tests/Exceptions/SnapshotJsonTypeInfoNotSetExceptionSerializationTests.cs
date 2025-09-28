using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class SnapshotJsonTypeInfoNotSetExceptionSerializationTests
{
    public class Constructor
    {
        [Fact]
        public void Should_set_default_message_and_error_code()
        {
            // Arrange & Act
            var sut = new SnapshotJsonTypeInfoNotSetException();

            // Assert
            Assert.Equal("[ELFAES-CFG-0002] Snapshot JsonInfo type should be set to deserialize the snapshot type", sut.Message);
            Assert.Equal("ELFAES-CFG-0002", sut.ErrorCode);
        }
    }
}
