using System;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class UnableToCreateEventStreamForStreamTypeExceptionSerializationTests
{
    public class Constructor
    {
        [Fact]
        public void Should_preserve_properties_in_public_constructors()
        {
            // Arrange
            var streamType = "PrimaryType";
            var fallbackType = "FallbackType";

            // Act
            var sut1 = new UnableToCreateEventStreamForStreamTypeException(streamType, fallbackType);
            var sut2 = new UnableToCreateEventStreamForStreamTypeException(streamType, fallbackType, new Exception("x"));

            // Assert
            Assert.Equal(streamType, sut1.StreamType);
            Assert.Equal(fallbackType, sut1.FallbackStreamType);
            Assert.Contains(streamType, sut1.Message);
            Assert.Contains(fallbackType, sut1.Message);
            Assert.Equal(streamType, sut2.StreamType);
            Assert.Equal(fallbackType, sut2.FallbackStreamType);
            Assert.NotNull(sut2.InnerException);
        }
    }
}
