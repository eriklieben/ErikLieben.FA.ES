#pragma warning disable CS0618 // Type or member is obsolete - testing deprecated API intentionally

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryDocumentTagStoreTests
{
    [Fact]
    public async Task Should_throw_when_document_or_tag_invalid()
    {
        // Arrange
        var store = new InMemoryDocumentTagStore();
        var doc = new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation(),
            [],
            "1.0.0");

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SetAsync(null!, "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SetAsync(doc, " "));
    }

    [Fact]
    public async Task SetAsync_should_not_throw_for_valid_inputs()
    {
        // Arrange
        var store = new InMemoryDocumentTagStore();
        var doc = new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation
            {
                StreamConnectionName = "inMemory",
                SnapShotConnectionName = "inMemory",
                DocumentTagConnectionName = "inMemory",
                StreamTagConnectionName = "inMemory",
                StreamIdentifier = "1-0000000000",
                StreamType = "inMemory",
                DocumentTagType = "inMemory",
                CurrentStreamVersion = -1,
            },
            [],
            "1.0.0");

        // Act
        await store.SetAsync(doc, "tag1");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task GetAsync_should_return_empty_when_key_not_present()
    {
        // Arrange
        var store = new InMemoryDocumentTagStore();

        // Act
        var result = await store.GetAsync("order", "tag1");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SetAsync_should_add_tag_when_list_exists_and_skip_duplicates()
    {
        // Arrange
        var store = new InMemoryDocumentTagStore();
        var doc = new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation(),
            [],
            "1.0.0");

        // Pre-populate the private Tags dictionary under the document id
        var field = typeof(InMemoryDocumentTagStore).GetField("Tags", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var dict = (Dictionary<string, List<string>>)field!.GetValue(store)!;
        dict[doc.ObjectId] = ["existing"];

        // Act
        await store.SetAsync(doc, "new-tag"); // should add
        await store.SetAsync(doc, "existing"); // should not duplicate

        // Assert - GetAsync uses objectName key, which differs from ObjectId. Validate internal list instead.
        Assert.Equal(2, dict[doc.ObjectId].Count);
        Assert.Contains("existing", dict[doc.ObjectId]);
        Assert.Contains("new-tag", dict[doc.ObjectId]);
    }

    [Fact]
    public async Task RemoveAsync_should_throw_when_document_is_null()
    {
        // Arrange
        var store = new InMemoryDocumentTagStore();

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.RemoveAsync(null!, "x"));
    }

    [Fact]
    public async Task RemoveAsync_should_throw_when_tag_is_null_or_whitespace()
    {
        // Arrange
        var store = new InMemoryDocumentTagStore();
        var doc = new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation(),
            [],
            "1.0.0");

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.RemoveAsync(doc, null!));
        await Assert.ThrowsAsync<ArgumentException>(() => store.RemoveAsync(doc, " "));
    }

    [Fact]
    public async Task RemoveAsync_should_remove_tag_from_document()
    {
        // Arrange
        var store = new InMemoryDocumentTagStore();
        var doc = new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation(),
            [],
            "1.0.0");

        // Pre-populate the private Tags dictionary
        var field = typeof(InMemoryDocumentTagStore).GetField("Tags", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var dict = (Dictionary<string, List<string>>)field!.GetValue(store)!;
        dict[doc.ObjectId] = ["tag1", "tag2"];

        // Act
        await store.RemoveAsync(doc, "tag1");

        // Assert
        Assert.Single(dict[doc.ObjectId]);
        Assert.Contains("tag2", dict[doc.ObjectId]);
        Assert.DoesNotContain("tag1", dict[doc.ObjectId]);
    }

    [Fact]
    public async Task RemoveAsync_should_not_throw_when_tag_does_not_exist()
    {
        // Arrange
        var store = new InMemoryDocumentTagStore();
        var doc = new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation(),
            [],
            "1.0.0");

        // Act - should not throw
        await store.RemoveAsync(doc, "nonexistent");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task RemoveAsync_should_not_throw_when_document_has_no_tags()
    {
        // Arrange
        var store = new InMemoryDocumentTagStore();
        var doc = new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation(),
            [],
            "1.0.0");

        // Pre-populate the private Tags dictionary with empty list
        var field = typeof(InMemoryDocumentTagStore).GetField("Tags", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var dict = (Dictionary<string, List<string>>)field!.GetValue(store)!;
        dict[doc.ObjectId] = [];

        // Act - should not throw
        await store.RemoveAsync(doc, "any-tag");

        // Assert
        Assert.Empty(dict[doc.ObjectId]);
    }
}
