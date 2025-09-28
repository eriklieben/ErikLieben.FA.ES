using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class VersionTokenStreamMismatchExceptionSerializationTests
{
    public class Serialization
    {
        [Fact]
        public void Should_roundtrip_properties_via_serialization_constructor()
        {
            // Arrange
            var left = "ObjectA__id";
            var right = "ObjectB__id";
            var original = new VersionTokenStreamMismatchException(left, right);
            var info = new SerializationInfo(typeof(VersionTokenStreamMismatchException), new FormatterConverter());
            var context = new StreamingContext(StreamingContextStates.All);

            // Act
            original.GetObjectData(info, context);
            var sut = (VersionTokenStreamMismatchException)Activator.CreateInstance(
                typeof(VersionTokenStreamMismatchException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { info, context },
                culture: null)!;

            // Assert
            Assert.Equal(left, sut.LeftObjectIdentifier);
            Assert.Equal(right, sut.RightObjectIdentifier);
            Assert.Equal("ELFAES-VAL-0004", info.GetString(nameof(EsException.ErrorCode)));
            Assert.Equal($"[ELFAES-VAL-0004] Version token stream mismatch: '{left}' vs '{right}'.", sut.Message);
        }

        [Fact]
        public void Should_include_properties_in_serialization_info_via_GetObjectData()
        {
            // Arrange
            var sut = new VersionTokenStreamMismatchException("L", "R");
            var info = new SerializationInfo(typeof(VersionTokenStreamMismatchException), new FormatterConverter());

            // Act
            sut.GetObjectData(info, new StreamingContext(StreamingContextStates.All));

            // Assert
            Assert.Equal("ELFAES-VAL-0004", info.GetString(nameof(EsException.ErrorCode)));
            Assert.Equal("L", info.GetString(nameof(VersionTokenStreamMismatchException.LeftObjectIdentifier)));
            Assert.Equal("R", info.GetString(nameof(VersionTokenStreamMismatchException.RightObjectIdentifier)));
        }
    }
}
