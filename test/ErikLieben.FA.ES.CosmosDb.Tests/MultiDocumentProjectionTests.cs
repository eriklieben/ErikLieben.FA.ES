using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class MultiDocumentProjectionTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_with_zero_pending_documents()
        {
            // Arrange & Act
            var sut = new TestMultiDocumentProjection();

            // Assert
            Assert.Equal(0, sut.PendingDocumentCount);
        }

        [Fact]
        public void Should_inherit_from_projection()
        {
            // Arrange & Act
            var sut = new TestMultiDocumentProjection();

            // Assert
            Assert.IsAssignableFrom<Projection>(sut);
        }
    }

    public class AppendDocumentMethod
    {
        [Fact]
        public void Should_increment_pending_document_count()
        {
            // Arrange
            var sut = new TestMultiDocumentProjection();
            var document = new TestDocument { Id = "1", Data = "test" };

            // Act
            sut.TestAppendDocument(document);

            // Assert
            Assert.Equal(1, sut.PendingDocumentCount);
        }

        [Fact]
        public void Should_throw_when_document_is_null()
        {
            // Arrange
            var sut = new TestMultiDocumentProjection();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.TestAppendDocument<TestDocument>(null!));
        }

        [Fact]
        public void Should_allow_multiple_documents()
        {
            // Arrange
            var sut = new TestMultiDocumentProjection();
            var doc1 = new TestDocument { Id = "1", Data = "test1" };
            var doc2 = new TestDocument { Id = "2", Data = "test2" };
            var doc3 = new TestDocument { Id = "3", Data = "test3" };

            // Act
            sut.TestAppendDocument(doc1);
            sut.TestAppendDocument(doc2);
            sut.TestAppendDocument(doc3);

            // Assert
            Assert.Equal(3, sut.PendingDocumentCount);
        }

        [Fact]
        public void Should_allow_different_document_types()
        {
            // Arrange
            var sut = new TestMultiDocumentProjection();
            var doc1 = new TestDocument { Id = "1", Data = "test" };
            var doc2 = new AnotherDocument { Key = "key", Value = 42 };

            // Act
            sut.TestAppendDocument(doc1);
            sut.TestAppendDocument(doc2);

            // Assert
            Assert.Equal(2, sut.PendingDocumentCount);
        }
    }

    public class CheckpointProperty
    {
        [Fact]
        public void Should_have_default_checkpoint()
        {
            // Arrange & Act
            var sut = new TestMultiDocumentProjection();

            // Assert
            Assert.NotNull(sut.Checkpoint);
        }

        [Fact]
        public void Should_allow_setting_checkpoint()
        {
            // Arrange
            var sut = new TestMultiDocumentProjection();
            var checkpoint = new ErikLieben.FA.ES.Checkpoint();

            // Act
            sut.Checkpoint = checkpoint;

            // Assert
            Assert.Same(checkpoint, sut.Checkpoint);
        }
    }

    public class ToJsonMethod
    {
        [Fact]
        public void Should_serialize_checkpoint_to_json()
        {
            // Arrange
            var sut = new TestMultiDocumentProjection();

            // Act
            var json = sut.ToJson();

            // Assert
            Assert.NotNull(json);
            Assert.Contains("$checkpoint", json);
        }
    }

    private class TestMultiDocumentProjection : MultiDocumentProjection
    {
        public void TestAppendDocument<T>(T document) where T : class
            => AppendDocument(document);
    }

    private class TestDocument
    {
        public string Id { get; set; } = string.Empty;
        public string? Data { get; set; }
    }

    private class AnotherDocument
    {
        public string Key { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
