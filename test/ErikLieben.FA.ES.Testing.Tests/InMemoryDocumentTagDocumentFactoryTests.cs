#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryDocumentTagDocumentFactoryTests
{
    public class CreateDocumentTagStoreNoParameters
    {
        [Fact]
        public void Should_return_in_memory_document_tag_store()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();

            // Act
            var result = factory.CreateDocumentTagStore();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<InMemoryDocumentTagStore>(result);
        }

        [Fact]
        public void Should_return_new_instance_each_call()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();

            // Act
            var result1 = factory.CreateDocumentTagStore();
            var result2 = factory.CreateDocumentTagStore();

            // Assert
            Assert.NotSame(result1, result2);
        }
    }

    public class CreateDocumentTagStoreWithDocument
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_document_is_null()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => factory.CreateDocumentTagStore((IObjectDocument)null!));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_document_tag_type_is_null()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();
            var document = new InMemoryEventStreamDocument(
                "1",
                "order",
                new StreamInformation { DocumentTagType = null! },
                [],
                "1.0.0");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => factory.CreateDocumentTagStore(document));
        }

        [Fact]
        public void Should_return_in_memory_document_tag_store()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();
            var document = CreateTestDocument();

            // Act
            var result = factory.CreateDocumentTagStore(document);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<InMemoryDocumentTagStore>(result);
        }

        [Fact]
        public void Should_return_new_instance_each_call()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();
            var document = CreateTestDocument();

            // Act
            var result1 = factory.CreateDocumentTagStore(document);
            var result2 = factory.CreateDocumentTagStore(document);

            // Assert
            Assert.NotSame(result1, result2);
        }
    }

    public class CreateDocumentTagStoreWithType
    {
        [Fact]
        public void Should_return_in_memory_document_tag_store()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();

            // Act
            var result = factory.CreateDocumentTagStore("anyType");

            // Assert
            Assert.NotNull(result);
            Assert.IsType<InMemoryDocumentTagStore>(result);
        }

        [Fact]
        public void Should_return_new_instance_each_call()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();

            // Act
            var result1 = factory.CreateDocumentTagStore("type1");
            var result2 = factory.CreateDocumentTagStore("type1");

            // Assert
            Assert.NotSame(result1, result2);
        }
    }

    public class CreateStreamTagStoreNoParameters
    {
        [Fact]
        public void Should_return_in_memory_stream_tag_store()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();

            // Act
            var result = factory.CreateStreamTagStore();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<InMemoryStreamTagStore>(result);
        }

        [Fact]
        public void Should_return_new_instance_each_call()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();

            // Act
            var result1 = factory.CreateStreamTagStore();
            var result2 = factory.CreateStreamTagStore();

            // Assert
            Assert.NotSame(result1, result2);
        }
    }

    public class CreateStreamTagStoreWithDocument
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_document_is_null()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => factory.CreateStreamTagStore(null!));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_event_stream_tag_type_is_null()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();
            var document = new InMemoryEventStreamDocument(
                "1",
                "order",
                new StreamInformation
                {
                    DocumentTagType = "inMemory",
                    EventStreamTagType = null!
                },
                [],
                "1.0.0");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => factory.CreateStreamTagStore(document));
        }

        [Fact]
        public void Should_return_in_memory_stream_tag_store()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();
            var document = CreateTestDocument();

            // Act
            var result = factory.CreateStreamTagStore(document);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<InMemoryStreamTagStore>(result);
        }

        [Fact]
        public void Should_return_new_instance_each_call()
        {
            // Arrange
            var factory = new InMemoryDocumentTagDocumentFactory();
            var document = CreateTestDocument();

            // Act
            var result1 = factory.CreateStreamTagStore(document);
            var result2 = factory.CreateStreamTagStore(document);

            // Assert
            Assert.NotSame(result1, result2);
        }
    }

    private static InMemoryEventStreamDocument CreateTestDocument()
    {
        return new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation
            {
                StreamIdentifier = "1-0000000000",
                StreamType = "inMemory",
                DocumentTagType = "inMemory",
                EventStreamTagType = "inMemory",
            },
            [],
            "1.0.0");
    }
}
