using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class UnableToCreateEventStreamForStreamTypeExceptionSerializationTests
{
    public class Serialization
    {
        [Fact]
        public void Should_roundtrip_properties_via_serialization_constructor()
        {
            // Arrange
            var streamType = "PrimaryType";
            var fallbackType = "FallbackType";
            var original = new UnableToCreateEventStreamForStreamTypeException(streamType, fallbackType);
            var info = new SerializationInfo(typeof(UnableToCreateEventStreamForStreamTypeException), new FormatterConverter());
            var context = new StreamingContext(StreamingContextStates.All);

            // Act
            original.GetObjectData(info, context);
            var sut = (UnableToCreateEventStreamForStreamTypeException)Activator.CreateInstance(
                typeof(UnableToCreateEventStreamForStreamTypeException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { info, context },
                culture: null)!;

            // Assert
            Assert.Equal(streamType, sut.StreamType);
            Assert.Equal(fallbackType, sut.FallbackStreamType);
            Assert.Equal(
                $"[ELFAES-CFG-0003] Unable to create EventStream of the type {streamType} or {fallbackType}. Is your configuration correct?",
                sut.Message);
        }

        [Fact]
        public void Should_include_properties_in_SerializationInfo_via_GetObjectData()
        {
            // Arrange
            var sut = new UnableToCreateEventStreamForStreamTypeException("A", "B");
            var info = new SerializationInfo(typeof(UnableToCreateEventStreamForStreamTypeException), new FormatterConverter());

            // Act
            sut.GetObjectData(info, new StreamingContext(StreamingContextStates.All));

            // Assert
            Assert.Equal("ELFAES-CFG-0003", info.GetString(nameof(EsException.ErrorCode)));
            Assert.Equal("A", info.GetString(nameof(UnableToCreateEventStreamForStreamTypeException.StreamType)));
            Assert.Equal("B", info.GetString(nameof(UnableToCreateEventStreamForStreamTypeException.FallbackStreamType)));
        }
    }
}
