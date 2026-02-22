using System;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class VersionTokenStreamMismatchExceptionSerializationTests
{
    public class Constructor
    {
        [Fact]
        public void Should_preserve_properties_and_message()
        {
            // Arrange
            var left = "L";
            var right = "R";

            // Act
            var sut1 = new VersionTokenStreamMismatchException(left, right);
            var sut2 = new VersionTokenStreamMismatchException(left, right, new Exception("inner"));

            // Assert
            Assert.Equal(left, sut1.LeftObjectIdentifier);
            Assert.Equal(right, sut1.RightObjectIdentifier);
            Assert.Contains(left, sut1.Message);
            Assert.Contains(right, sut1.Message);
            Assert.NotNull(sut2.InnerException);
        }
    }
}
