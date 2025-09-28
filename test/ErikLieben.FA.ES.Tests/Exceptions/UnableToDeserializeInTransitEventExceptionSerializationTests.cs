using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class UnableToDeserializeInTransitEventExceptionSerializationTests
{
    public class Serialization
    {
        [Fact]
        public void Should_roundtrip_via_serialization_constructor()
        {
            // Arrange
            var original = new UnableToDeserializeInTransitEventException();
            var info = new SerializationInfo(typeof(UnableToDeserializeInTransitEventException), new FormatterConverter());
            var context = new StreamingContext(StreamingContextStates.All);

            // Act
            original.GetObjectData(info, context);
            var sut = (UnableToDeserializeInTransitEventException)Activator.CreateInstance(
                typeof(UnableToDeserializeInTransitEventException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { info, context },
                culture: null)!;

            // Assert
            Assert.IsAssignableFrom<Exception>(sut);
            Assert.Equal("ELFAES-VAL-0001", info.GetString(nameof(EsException.ErrorCode)));
            Assert.Equal("[ELFAES-VAL-0001] Unable to deserialize to event, value is 'null'", sut.Message);
        }

        [Fact]
        public void Should_include_error_code_in_serialization_info()
        {
            // Arrange
            var sut = new UnableToDeserializeInTransitEventException();
            var info = new SerializationInfo(typeof(UnableToDeserializeInTransitEventException), new FormatterConverter());

            // Act
            sut.GetObjectData(info, new StreamingContext(StreamingContextStates.All));

            // Assert
            Assert.Equal("ELFAES-VAL-0001", info.GetString(nameof(EsException.ErrorCode)));
        }
    }
}
