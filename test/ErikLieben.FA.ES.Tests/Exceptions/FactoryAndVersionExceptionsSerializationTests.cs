using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class FactoryAndVersionExceptionsSerializationTests
{
    public class UnableToFindDocumentFactoryExceptionSerialization
    {
        [Fact]
        public void Should_roundtrip_via_serialization_constructor()
        {
            // Arrange
            var original = new UnableToFindDocumentFactoryException("no factory");
            var info = new SerializationInfo(typeof(UnableToFindDocumentFactoryException), new FormatterConverter());
            var context = new StreamingContext(StreamingContextStates.All);

            // Act
            original.GetObjectData(info, context);
            var sut = (UnableToFindDocumentFactoryException)Activator.CreateInstance(
                typeof(UnableToFindDocumentFactoryException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { info, context },
                culture: null)!;

            // Assert
            Assert.Equal("[ELFAES-CFG-0004] no factory", sut.Message);
        }
    }

    public class UnableToFindDocumentTagFactoryExceptionSerialization
    {
        [Fact]
        public void Should_roundtrip_via_serialization_constructor()
        {
            // Arrange
            var original = new UnableToFindDocumentTagFactoryException("no tag factory");
            var info = new SerializationInfo(typeof(UnableToFindDocumentTagFactoryException), new FormatterConverter());
            var context = new StreamingContext(StreamingContextStates.All);

            // Act
            original.GetObjectData(info, context);
            var sut = (UnableToFindDocumentTagFactoryException)Activator.CreateInstance(
                typeof(UnableToFindDocumentTagFactoryException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { info, context },
                culture: null)!;

            // Assert
            Assert.Equal("[ELFAES-CFG-0005] no tag factory", sut.Message);
        }
    }

    public class VersionTokenStreamMismatchExceptionSerialization
    {
        [Fact]
        public void Should_roundtrip_via_serialization_constructor_and_preserve_properties()
        {
            // Arrange
            var original = new VersionTokenStreamMismatchException("A","B");
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
            Assert.Equal("A", sut.LeftObjectIdentifier);
            Assert.Equal("B", sut.RightObjectIdentifier);
            Assert.Equal("[ELFAES-VAL-0004] Version token stream mismatch: 'A' vs 'B'.", sut.Message);
        }

        [Fact]
        public void Should_support_inner_exception_constructor()
        {
            // Arrange
            var inner = new InvalidOperationException("inner");

            // Act
            var sut = new VersionTokenStreamMismatchException("L","R", inner);

            // Assert
            Assert.Same(inner, sut.InnerException);
        }
    }

    public class UnableToCreateEventStreamForStreamTypeExceptionCtor
    {
        [Fact]
        public void Should_support_inner_exception_constructor_and_preserve_properties()
        {
            // Arrange
            var inner = new InvalidOperationException("inner");

            // Act
            var sut = new UnableToCreateEventStreamForStreamTypeException("X","Y", inner);

            // Assert
            Assert.Same(inner, sut.InnerException);
            Assert.Equal("X", sut.StreamType);
            Assert.Equal("Y", sut.FallbackStreamType);
        }
    }
}
