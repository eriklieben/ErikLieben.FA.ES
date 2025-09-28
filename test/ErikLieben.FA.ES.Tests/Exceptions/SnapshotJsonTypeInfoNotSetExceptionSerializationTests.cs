using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class SnapshotJsonTypeInfoNotSetExceptionSerializationTests
{
    public class Serialization
    {
        [Fact]
        public void Should_roundtrip_via_serialization_constructor()
        {
            // Arrange
            var original = new SnapshotJsonTypeInfoNotSetException();
            var info = new SerializationInfo(typeof(SnapshotJsonTypeInfoNotSetException), new FormatterConverter());
            var context = new StreamingContext(StreamingContextStates.All);

            // Act
            original.GetObjectData(info, context);
            var sut = (SnapshotJsonTypeInfoNotSetException)Activator.CreateInstance(
                typeof(SnapshotJsonTypeInfoNotSetException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { info, context },
                culture: null)!;

            // Assert
            Assert.IsAssignableFrom<Exception>(sut);
            Assert.Equal("ELFAES-CFG-0002", info.GetString(nameof(EsException.ErrorCode)));
            Assert.Equal("[ELFAES-CFG-0002] Snapshot JsonInfo type should be set to deserialize the snapshot type", sut.Message);
        }

        [Fact]
        public void Should_include_error_code_in_serialization_info()
        {
            // Arrange
            var sut = new SnapshotJsonTypeInfoNotSetException();
            var info = new SerializationInfo(typeof(SnapshotJsonTypeInfoNotSetException), new FormatterConverter());

            // Act
            sut.GetObjectData(info, new StreamingContext(StreamingContextStates.All));

            // Assert
            Assert.Equal("ELFAES-CFG-0002", info.GetString(nameof(EsException.ErrorCode)));
        }
    }
}
