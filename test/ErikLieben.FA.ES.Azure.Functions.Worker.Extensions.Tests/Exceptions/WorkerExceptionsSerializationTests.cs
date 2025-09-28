using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Exceptions;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class WorkerExceptionsSerializationTests
{
    public class InvalidBindingSourceExceptionSerialization
    {
        [Fact]
        public void Should_roundtrip_via_serialization_constructor()
        {
            // Arrange
            var original = new InvalidBindingSourceException("X", "Y");
            var info = new SerializationInfo(typeof(InvalidBindingSourceException), new FormatterConverter());
            var context = new StreamingContext(StreamingContextStates.All);

            // Act
            original.GetObjectData(info, context);
            var sut = (InvalidBindingSourceException)Activator.CreateInstance(
                typeof(InvalidBindingSourceException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { info, context },
                culture: null)!;

            // Assert
            Assert.Contains("Unexpected binding source", sut.Message);
            Assert.Equal("ELFAES-VAL-0002", info.GetString(nameof(EsException.ErrorCode)));
        }
    }

    public class InvalidContentTypeExceptionSerialization
    {
        [Fact]
        public void Should_roundtrip_via_serialization_constructor()
        {
            // Arrange
            var original = new InvalidContentTypeException("A", "B");
            var info = new SerializationInfo(typeof(InvalidContentTypeException), new FormatterConverter());
            var context = new StreamingContext(StreamingContextStates.All);

            // Act
            original.GetObjectData(info, context);
            var sut = (InvalidContentTypeException)Activator.CreateInstance(
                typeof(InvalidContentTypeException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { info, context },
                culture: null)!;

            // Assert
            Assert.Contains("Unexpected content-type", sut.Message);
            Assert.Equal("ELFAES-VAL-0003", info.GetString(nameof(EsException.ErrorCode)));
        }
    }
}
