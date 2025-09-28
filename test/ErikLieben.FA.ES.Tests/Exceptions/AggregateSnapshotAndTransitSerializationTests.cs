using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class AggregateSnapshotAndTransitSerializationTests
{
    public class AggregateJsonTypeInfoNotSetExceptionBasics
    {
        [Fact]
        public void Should_support_message_overload()
        {
            // Arrange
            var sut = new AggregateJsonTypeInfoNotSetException("custom");

            // Assert
            Assert.Equal("[ELFAES-CFG-0001] custom", sut.Message);
        }
    }

    public class SnapshotJsonTypeInfoNotSetExceptionBasics
    {
        [Fact]
        public void Should_support_message_overload()
        {
            // Arrange
            var sut = new SnapshotJsonTypeInfoNotSetException("custom");

            // Assert
            Assert.Equal("[ELFAES-CFG-0002] custom", sut.Message);
        }
    }

    public class UnableToDeserializeInTransitEventExceptionBasics
    {
        [Fact]
        public void Should_support_message_overload()
        {
            // Arrange
            var sut = new UnableToDeserializeInTransitEventException("other");

            // Assert
            Assert.Equal("[ELFAES-VAL-0001] other", sut.Message);
        }
    }
}
