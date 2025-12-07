using System;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob
{
    public class BlobEventStreamTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_initialize_with_valid_parameters()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var streamDependencies = Substitute.For<IStreamDependencies>();

                // Act
                var sut = new BlobEventStream(document, streamDependencies);

                // Assert
                Assert.NotNull(sut);
                // Since Document and StreamDependencies properties are protected in BaseEventStream,
                // we can verify only that the constructor doesn't throw exceptions
            }

            [Fact]
            public void Should_throw_exception_when_document_is_null()
            {
                // Arrange
                IObjectDocumentWithMethods document = null!;
                var streamDependencies = Substitute.For<IStreamDependencies>();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new BlobEventStream(document, streamDependencies));
            }

            [Fact]
            public void Should_throw_exception_when_streamDependencies_is_null()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                IStreamDependencies streamDependencies = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new BlobEventStream(document, streamDependencies));
            }
        }
    }
}
