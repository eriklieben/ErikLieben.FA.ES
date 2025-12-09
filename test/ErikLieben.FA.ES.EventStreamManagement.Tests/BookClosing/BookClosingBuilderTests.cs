using ErikLieben.FA.ES.EventStreamManagement.BookClosing;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.BookClosing;

public class BookClosingBuilderTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_instance()
        {
            // Arrange & Act
            var sut = new BookClosingBuilder();

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class ReasonMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BookClosingBuilder();

            // Act
            var result = sut.Reason("Migration completed");

            // Assert
            Assert.Same(sut, result);
        }

        [Theory]
        [InlineData("Migration to new stream")]
        [InlineData("Stream deprecated")]
        [InlineData("Data archival")]
        [InlineData("")]
        public void Should_accept_various_reasons(string reason)
        {
            // Arrange
            var sut = new BookClosingBuilder();

            // Act
            var result = sut.Reason(reason);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class CreateSnapshotMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BookClosingBuilder();

            // Act
            var result = sut.CreateSnapshot();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class ArchiveToStorageMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BookClosingBuilder();

            // Act
            var result = sut.ArchiveToStorage("/archives/2024");

            // Assert
            Assert.Same(sut, result);
        }

        [Theory]
        [InlineData("/archives")]
        [InlineData("https://storage.blob.core.windows.net/archives")]
        [InlineData("s3://bucket/archives")]
        public void Should_accept_various_storage_locations(string location)
        {
            // Arrange
            var sut = new BookClosingBuilder();

            // Act
            var result = sut.ArchiveToStorage(location);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class MarkAsDeletedMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BookClosingBuilder();

            // Act
            var result = sut.MarkAsDeleted();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithMetadataMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BookClosingBuilder();

            // Act
            var result = sut.WithMetadata("key", "value");

            // Assert
            Assert.Same(sut, result);
        }

        [Fact]
        public void Should_accept_multiple_metadata_entries()
        {
            // Arrange
            var sut = new BookClosingBuilder();

            // Act
            sut.WithMetadata("key1", "value1")
               .WithMetadata("key2", 42)
               .WithMetadata("key3", true);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_accept_various_value_types()
        {
            // Arrange
            var sut = new BookClosingBuilder();

            // Act
            sut.WithMetadata("string", "value")
               .WithMetadata("int", 123)
               .WithMetadata("bool", false)
               .WithMetadata("object", new { X = 1 });

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class FluentChainingTests
    {
        [Fact]
        public void Should_support_full_fluent_configuration()
        {
            // Arrange & Act
            var sut = new BookClosingBuilder()
                .Reason("Migration completed successfully")
                .CreateSnapshot()
                .ArchiveToStorage("/archives/2024")
                .MarkAsDeleted()
                .WithMetadata("migratedTo", "new-stream")
                .WithMetadata("migratedAt", DateTimeOffset.UtcNow);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class InterfaceImplementation
    {
        [Fact]
        public void Should_implement_IBookClosingBuilder()
        {
            // Arrange & Act
            var sut = new BookClosingBuilder();

            // Assert
            Assert.IsAssignableFrom<IBookClosingBuilder>(sut);
        }
    }
}
