using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryDocumentStoreTests
{
    [Fact]
    public async Task Create_and_Get_should_roundtrip_document()
    {
        // Arrange
        var store = new InMemoryDocumentStore();

        // Act
        var created = await store.CreateAsync("order", "1");
        var fetched = await store.GetAsync("order", "1");

        // Assert
        Assert.Equal(created.ObjectId, fetched.ObjectId);
        Assert.Equal(created.ObjectName, fetched.ObjectName);
    }

    [Fact]
    public async Task Create_should_throw_if_document_already_exists()
    {
        // Arrange
        var store = new InMemoryDocumentStore();
        await store.CreateAsync("order", "1");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.CreateAsync("order", "1"));
    }

    [Fact]
    public async Task Set_should_update_existing_document()
    {
        // Arrange
        var store = new InMemoryDocumentStore();
        var doc = await store.CreateAsync("order", "1");
        doc.Active.StreamType = "changed";

        // Act
        await store.SetAsync(doc);
        var fetched = await store.GetAsync("order", "1");

        // Assert
        Assert.Equal("changed", fetched.Active.StreamType);
    }
}
