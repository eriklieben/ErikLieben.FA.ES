using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class AggregateJsonTypeInfoNotSetExceptionSerializationTests
{
    public class Serialization
    {
        [Fact]
        public void Should_roundtrip_via_serialization_constructor()
        {
            // Arrange
            var original = new AggregateJsonTypeInfoNotSetException();
            var info = new SerializationInfo(typeof(AggregateJsonTypeInfoNotSetException), new FormatterConverter());
            var context = new StreamingContext(StreamingContextStates.All);

            // Act
            original.GetObjectData(info, context);
            var sut = (AggregateJsonTypeInfoNotSetException)Activator.CreateInstance(
                typeof(AggregateJsonTypeInfoNotSetException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { info, context },
                culture: null)!;

            // Assert
            Assert.IsAssignableFrom<Exception>(sut);
            Assert.Equal("ELFAES-CFG-0001", info.GetString(nameof(EsException.ErrorCode)));
            Assert.Equal("[ELFAES-CFG-0001] Aggregate JsonInfo type should be set to deserialize the aggregate type", sut.Message);
        }

        [Fact]
        public void Should_include_error_code_in_serialization_info()
        {
            // Arrange
            var sut = new AggregateJsonTypeInfoNotSetException();
            var info = new SerializationInfo(typeof(AggregateJsonTypeInfoNotSetException), new FormatterConverter());

            // Act
            sut.GetObjectData(info, new StreamingContext(StreamingContextStates.All));

            // Assert
            Assert.Equal("ELFAES-CFG-0001", info.GetString(nameof(EsException.ErrorCode)));
        }
    }
}
