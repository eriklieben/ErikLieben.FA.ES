using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class AggregateSnapshotAndTransitSerializationTests
{
    public class AggregateJsonTypeInfoNotSetExceptionSerialization
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
            Assert.Equal("[ELFAES-CFG-0001] Aggregate JsonInfo type should be set to deserialize the aggregate type", sut.Message);
            Assert.Equal("ELFAES-CFG-0001", info.GetString(nameof(EsException.ErrorCode)));
        }

        [Fact]
        public void Should_support_message_overload()
        {
            // Arrange
            var sut = new AggregateJsonTypeInfoNotSetException("custom");

            // Assert
            Assert.Equal("[ELFAES-CFG-0001] custom", sut.Message);
        }
    }

    public class SnapshotJsonTypeInfoNotSetExceptionSerialization
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
            Assert.Equal("[ELFAES-CFG-0002] Snapshot JsonInfo type should be set to deserialize the snapshot type", sut.Message);
            Assert.Equal("ELFAES-CFG-0002", info.GetString(nameof(EsException.ErrorCode)));
        }

        [Fact]
        public void Should_support_message_overload()
        {
            // Arrange
            var sut = new SnapshotJsonTypeInfoNotSetException("custom");

            // Assert
            Assert.Equal("[ELFAES-CFG-0002] custom", sut.Message);
        }
    }

    public class UnableToDeserializeInTransitEventExceptionSerialization
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
            Assert.Equal("[ELFAES-VAL-0001] Unable to deserialize to event, value is 'null'", sut.Message);
            Assert.Equal("ELFAES-VAL-0001", info.GetString(nameof(EsException.ErrorCode)));
        }

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
