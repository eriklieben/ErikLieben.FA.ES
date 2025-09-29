using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryDocumentTagStoreTests
{
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
}
