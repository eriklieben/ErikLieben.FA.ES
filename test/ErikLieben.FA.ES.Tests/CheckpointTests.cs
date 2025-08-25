using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.Tests;

public class CheckpointTests
{
    [Fact]
    public void Should_create_empty_checkpoint()
    {
        // Arrange & Act
        var sut = new Checkpoint();

        // Assert
        Assert.Empty(sut);
    }

    [Fact]
    public void Should_add_item_to_checkpoint()
    {
        // Arrange
        var sut = new Checkpoint();
        var objectId = new ObjectIdentifier("TestObject", "123");
        var versionId = new VersionIdentifier("stream1", 1);

        // Act
        sut.Add(objectId, versionId);

        // Assert
        Assert.Single(sut);
        Assert.True(sut.ContainsKey(objectId));
        Assert.Equal(versionId, sut[objectId]);
    }

    [Fact]
    public void Should_remove_item_from_checkpoint()
    {
        // Arrange
        var sut = new Checkpoint();
        var objectId = new ObjectIdentifier("TestObject", "123");
        var versionId = new VersionIdentifier("stream1", 1);
        sut.Add(objectId, versionId);

        // Act
        bool result = sut.Remove(objectId);

        // Assert
        Assert.True(result);
        Assert.Empty(sut);
    }

    [Fact]
    public void Should_update_existing_item_in_checkpoint()
    {
        // Arrange
        var sut = new Checkpoint();
        var objectId = new ObjectIdentifier("TestObject", "123");
        var initialVersionId = new VersionIdentifier("stream1", 1);
        var updatedVersionId = new VersionIdentifier("stream1", 2);
        sut.Add(objectId, initialVersionId);

        // Act
        sut[objectId] = updatedVersionId;

        // Assert
        Assert.Single(sut);
        Assert.Equal(updatedVersionId, sut[objectId]);
    }

    [Fact]
    public void Should_clear_all_items_from_checkpoint()
    {
        // Arrange
        var sut = new Checkpoint();
        var objectId1 = new ObjectIdentifier("TestObject", "123");
        var versionId1 = new VersionIdentifier("stream1", 1);
        var objectId2 = new ObjectIdentifier("TestObject", "456");
        var versionId2 = new VersionIdentifier("stream2", 2);
        sut.Add(objectId1, versionId1);
        sut.Add(objectId2, versionId2);

        // Act
        sut.Clear();

        // Assert
        Assert.Empty(sut);
    }

    [Fact]
    public void Should_check_if_key_exists_in_checkpoint()
    {
        // Arrange
        var sut = new Checkpoint();
        var objectId = new ObjectIdentifier("TestObject", "123");
        var versionId = new VersionIdentifier("stream1", 1);
        sut.Add(objectId, versionId);
        var nonExistentObjectId = new ObjectIdentifier("TestObject", "456");

        // Act & Assert
        Assert.True(sut.ContainsKey(objectId));
        Assert.False(sut.ContainsKey(nonExistentObjectId));
    }

    [Fact]
    public void Should_try_get_value_from_checkpoint()
    {
        // Arrange
        var sut = new Checkpoint();
        var objectId = new ObjectIdentifier("TestObject", "123");
        var versionId = new VersionIdentifier("stream1", 1);
        sut.Add(objectId, versionId);
        var nonExistentObjectId = new ObjectIdentifier("TestObject", "456");

        // Act & Assert
        Assert.True(sut.TryGetValue(objectId, out var retrievedVersionId));
        Assert.Equal(versionId, retrievedVersionId);

        Assert.False(sut.TryGetValue(nonExistentObjectId, out var _));
    }

    [Fact]
    public void Should_enumerate_key_value_pairs()
    {
        // Arrange
        var sut = new Checkpoint();
        var objectId1 = new ObjectIdentifier("TestObject", "123");
        var versionId1 = new VersionIdentifier("stream1", 1);
        var objectId2 = new ObjectIdentifier("TestObject", "456");
        var versionId2 = new VersionIdentifier("stream2", 2);
        sut.Add(objectId1, versionId1);
        sut.Add(objectId2, versionId2);

        // Act
        var itemCount = 0;
        foreach (var kvp in sut)
        {
            itemCount++;
            Assert.True(kvp.Key == objectId1 || kvp.Key == objectId2);
            if (kvp.Key == objectId1)
                Assert.Equal(versionId1, kvp.Value);
            else
                Assert.Equal(versionId2, kvp.Value);
        }

        // Assert
        Assert.Equal(2, itemCount);
    }
}
