using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Exceptions
{
    public class BlobExceptionsSerializationTests
    {
        public class BlobDocumentNotFoundExceptionSerialization
        {
            [Fact]
            public void Should_roundtrip_via_serialization_constructor()
            {
                // Arrange
                var original = new BlobDocumentNotFoundException("missing");
                var info = new SerializationInfo(typeof(BlobDocumentNotFoundException), new FormatterConverter());
                var context = new StreamingContext(StreamingContextStates.All);

                // Act
                original.GetObjectData(info, context);
                var sut = (BlobDocumentNotFoundException)Activator.CreateInstance(
                    typeof(BlobDocumentNotFoundException),
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    args: new object[] { info, context },
                    culture: null)!;

                // Assert
                Assert.Equal("[ELFAES-FILE-0001] missing", sut.Message);
                Assert.Equal("ELFAES-FILE-0001", info.GetString(nameof(EsException.ErrorCode)));
            }

            [Fact]
            public void Should_have_serializable_attribute()
            {
                // Arrange & Act
                var attr = typeof(BlobDocumentNotFoundException).GetCustomAttributes(typeof(SerializableAttribute), false);

                // Assert
                Assert.NotEmpty(attr);
            }
        }

        public class BlobDocumentStoreContainerNotFoundExceptionSerialization
        {
            [Fact]
            public void Should_roundtrip_via_serialization_constructor()
            {
                // Arrange
                var original = new BlobDocumentStoreContainerNotFoundException("container missing");
                var info = new SerializationInfo(typeof(BlobDocumentStoreContainerNotFoundException), new FormatterConverter());
                var context = new StreamingContext(StreamingContextStates.All);

                // Act
                original.GetObjectData(info, context);
                var sut = (BlobDocumentStoreContainerNotFoundException)Activator.CreateInstance(
                    typeof(BlobDocumentStoreContainerNotFoundException),
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    args: new object[] { info, context },
                    culture: null)!;

                // Assert
                Assert.Equal("[ELFAES-FILE-0002] container missing", sut.Message);
                Assert.Equal("ELFAES-FILE-0002", info.GetString(nameof(EsException.ErrorCode)));
            }

            [Fact]
            public void Should_have_serializable_attribute()
            {
                // Arrange & Act
                var attr = typeof(BlobDocumentStoreContainerNotFoundException).GetCustomAttributes(typeof(SerializableAttribute), false);

                // Assert
                Assert.NotEmpty(attr);
            }
        }

        public class BlobDataStoreProcessingExceptionSerialization
        {
            [Fact]
            public void Should_roundtrip_via_serialization_constructor()
            {
                // Arrange
                var original = new BlobDataStoreProcessingException("processing failed");
                var info = new SerializationInfo(typeof(BlobDataStoreProcessingException), new FormatterConverter());
                var context = new StreamingContext(StreamingContextStates.All);

                // Act
                original.GetObjectData(info, context);
                var sut = (BlobDataStoreProcessingException)Activator.CreateInstance(
                    typeof(BlobDataStoreProcessingException),
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    args: new object[] { info, context },
                    culture: null)!;

                // Assert
                Assert.Equal("[ELFAES-EXT-0001] processing failed", sut.Message);
                Assert.Equal("ELFAES-EXT-0001", info.GetString(nameof(EsException.ErrorCode)));
            }

            [Fact]
            public void Should_support_inner_exception_constructor()
            {
                // Arrange
                var inner = new InvalidOperationException("inner");

                // Act
                var sut = new BlobDataStoreProcessingException("boom", inner);

                // Assert
                Assert.Same(inner, sut.InnerException);
                Assert.Equal("[ELFAES-EXT-0001] boom", sut.Message);
            }
        }
    }
}
