using System;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Exceptions
{
    public class BlobExceptionsSerializationTests
    {
        public class BlobDocumentNotFoundExceptionBasics
        {
            [Fact]
            public void Should_set_message_and_code()
            {
                // Arrange & Act
                var sut = new BlobDocumentNotFoundException("missing");

                // Assert
                Assert.Equal("[ELFAES-FILE-0001] missing", sut.Message);
            }
        }

        public class BlobDocumentStoreContainerNotFoundExceptionBasics
        {
            [Fact]
            public void Should_set_message_and_code()
            {
                // Arrange & Act
                var sut = new BlobDocumentStoreContainerNotFoundException("container missing");

                // Assert
                Assert.Equal("[ELFAES-FILE-0002] container missing", sut.Message);
            }
        }

        public class BlobDataStoreProcessingExceptionBasics
        {
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
