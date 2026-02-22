#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryStreamTagStoreTests
{
    public class SetAsync
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => store.SetAsync(null!, "tag"));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_tag_is_null()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => store.SetAsync(document, null!));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_tag_is_empty()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => store.SetAsync(document, ""));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_tag_is_whitespace()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => store.SetAsync(document, "   "));
        }

        [Fact]
        public async Task Should_add_tag_to_stream()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1", "order");

            // Act
            await store.SetAsync(document, "important");

            // Assert
            var result = await store.GetAsync("order", "important");
            Assert.Contains("stream-1", result);
        }

        [Fact]
        public async Task Should_not_add_duplicate_tag()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1", "order");

            // Act
            await store.SetAsync(document, "important");
            await store.SetAsync(document, "important");

            // Assert
            var result = await store.GetAsync("order", "important");
            Assert.Single(result);
        }

        [Fact]
        public async Task Should_add_multiple_tags_to_same_stream()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1", "order");

            // Act
            await store.SetAsync(document, "tag1");
            await store.SetAsync(document, "tag2");

            // Assert
            var result1 = await store.GetAsync("order", "tag1");
            var result2 = await store.GetAsync("order", "tag2");
            Assert.Contains("stream-1", result1);
            Assert.Contains("stream-1", result2);
        }

        [Fact]
        public async Task Should_add_same_tag_to_multiple_streams()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document1 = CreateTestDocument("1", "stream-1", "order");
            var document2 = CreateTestDocument("2", "stream-2", "order");

            // Act
            await store.SetAsync(document1, "important");
            await store.SetAsync(document2, "important");

            // Assert
            var result = await store.GetAsync("order", "important");
            Assert.Contains("stream-1", result);
            Assert.Contains("stream-2", result);
            Assert.Equal(2, result.Count());
        }
    }

    public class GetAsync
    {
        [Fact]
        public async Task Should_return_empty_when_no_streams_have_tag()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();

            // Act
            var result = await store.GetAsync("order", "nonexistent");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_return_stream_identifiers_with_matching_tag()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document1 = CreateTestDocument("1", "stream-1", "order");
            var document2 = CreateTestDocument("2", "stream-2", "order");
            var document3 = CreateTestDocument("3", "stream-3", "order");

            await store.SetAsync(document1, "active");
            await store.SetAsync(document2, "active");
            await store.SetAsync(document3, "inactive");

            // Act
            var activeStreams = await store.GetAsync("order", "active");
            var inactiveStreams = await store.GetAsync("order", "inactive");

            // Assert
            Assert.Equal(2, activeStreams.Count());
            Assert.Contains("stream-1", activeStreams);
            Assert.Contains("stream-2", activeStreams);
            Assert.Single(inactiveStreams);
            Assert.Contains("stream-3", inactiveStreams);
        }

        [Fact]
        public async Task Should_filter_by_object_name()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var orderDocument = CreateTestDocument("1", "order-stream-1", "order");
            var customerDocument = CreateTestDocument("2", "customer-stream-1", "customer");

            await store.SetAsync(orderDocument, "important");
            await store.SetAsync(customerDocument, "important");

            // Act
            var orderStreams = await store.GetAsync("order", "important");
            var customerStreams = await store.GetAsync("customer", "important");

            // Assert
            Assert.Single(orderStreams);
            Assert.Contains("order-stream-1", orderStreams);
            Assert.Single(customerStreams);
            Assert.Contains("customer-stream-1", customerStreams);
        }
    }

    public class RemoveAsync
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => store.RemoveAsync(null!, "tag"));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_tag_is_null()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => store.RemoveAsync(document, null!));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_tag_is_empty()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => store.RemoveAsync(document, ""));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_tag_is_whitespace()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => store.RemoveAsync(document, "   "));
        }

        [Fact]
        public async Task Should_remove_tag_from_stream()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1", "order");

            await store.SetAsync(document, "tag1");
            await store.SetAsync(document, "tag2");

            // Act
            await store.RemoveAsync(document, "tag1");

            // Assert
            var tag1Results = await store.GetAsync("order", "tag1");
            var tag2Results = await store.GetAsync("order", "tag2");
            Assert.Empty(tag1Results);
            Assert.Single(tag2Results);
        }

        [Fact]
        public async Task Should_not_throw_when_tag_does_not_exist()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1", "order");

            // Act - should not throw
            await store.RemoveAsync(document, "nonexistent");

            // Assert
            Assert.True(true);
        }

        [Fact]
        public async Task Should_not_throw_when_stream_has_no_tags()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document = CreateTestDocument("1", "stream-1", "order");

            // Act - should not throw
            await store.RemoveAsync(document, "any-tag");

            // Assert
            Assert.True(true);
        }

        [Fact]
        public async Task Should_not_affect_other_streams_with_same_tag()
        {
            // Arrange
            var store = new InMemoryStreamTagStore();
            var document1 = CreateTestDocument("1", "stream-1", "order");
            var document2 = CreateTestDocument("2", "stream-2", "order");

            await store.SetAsync(document1, "shared-tag");
            await store.SetAsync(document2, "shared-tag");

            // Act
            await store.RemoveAsync(document1, "shared-tag");

            // Assert
            var results = await store.GetAsync("order", "shared-tag");
            Assert.Single(results);
            Assert.Contains("stream-2", results);
            Assert.DoesNotContain("stream-1", results);
        }

        private static InMemoryEventStreamDocument CreateTestDocument(
            string objectId,
            string streamIdentifier,
            string objectName = "test")
        {
            return new InMemoryEventStreamDocument(
                objectId,
                objectName,
                new StreamInformation
                {
                    StreamIdentifier = streamIdentifier,
                    StreamType = "inMemory",
                    DocumentTagType = "inMemory",
                    EventStreamTagType = "inMemory",
                },
                [],
                "1.0.0");
        }
    }

    private static InMemoryEventStreamDocument CreateTestDocument(
        string objectId,
        string streamIdentifier,
        string objectName = "test")
    {
        return new InMemoryEventStreamDocument(
            objectId,
            objectName,
            new StreamInformation
            {
                StreamIdentifier = streamIdentifier,
                StreamType = "inMemory",
                DocumentTagType = "inMemory",
                EventStreamTagType = "inMemory",
            },
            [],
            "1.0.0");
    }
}
