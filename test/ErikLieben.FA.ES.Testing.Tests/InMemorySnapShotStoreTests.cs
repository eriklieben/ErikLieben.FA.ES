#pragma warning disable CS0618 // Type or member is obsolete - tests verify behavior of legacy properties
#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemorySnapShotStoreTests
{
    private class Dummy : IBase { public int V { get; set; } public Task Fold() => Task.CompletedTask; public void Fold(IEvent @event) { } public void ProcessSnapshot(object snapshot) { } }

    private static InMemoryEventStreamDocument CreateDoc(string streamIdentifier = "1-0000000000")
        => new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation
            {
                StreamConnectionName = "inMemory",
                SnapShotConnectionName = "inMemory",
                DocumentTagConnectionName = "inMemory",
                StreamTagConnectionName = "inMemory",
                StreamIdentifier = streamIdentifier,
                StreamType = "inMemory",
                DocumentTagType = "inMemory",
                CurrentStreamVersion = -1,
            },
            [],
            "1.0.0");

    private static JsonTypeInfo<Dummy> TypeInfo => JsonTypeInfo.CreateJsonTypeInfo<Dummy>(new())!;

    #region SetAsync and GetAsync

    [Fact]
    public async Task Set_and_Get_should_roundtrip_snapshot()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        var obj = new Dummy { V = 5 };

        // Act
        await store.SetAsync(obj, TypeInfo, doc, version: 10);
        var fetched = await store.GetAsync(TypeInfo, doc, version: 10);
        var fetchedGeneric = await store.GetAsync<Dummy>(TypeInfo, doc, version: 10);

        // Assert
        Assert.Same(obj, fetched);
        Assert.Same(obj, fetchedGeneric);
    }

    [Fact]
    public async Task Get_should_return_null_when_snapshot_missing()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();

        // Act
        var result = await store.GetAsync(TypeInfo, doc, 1);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_generic_should_return_null_when_snapshot_missing()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();

        // Act
        var result = await store.GetAsync<Dummy>(TypeInfo, doc, version: 99);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_should_overwrite_existing_snapshot_at_same_version()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        var obj1 = new Dummy { V = 1 };
        var obj2 = new Dummy { V = 2 };

        // Act
        await store.SetAsync(obj1, TypeInfo, doc, version: 5);
        await store.SetAsync(obj2, TypeInfo, doc, version: 5);
        var result = await store.GetAsync<Dummy>(TypeInfo, doc, version: 5);

        // Assert
        Assert.Same(obj2, result);
    }

    [Fact]
    public async Task SetAsync_with_name_should_store_separately_from_unnamed()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        var unnamed = new Dummy { V = 10 };
        var named = new Dummy { V = 20 };

        // Act
        await store.SetAsync(unnamed, TypeInfo, doc, version: 5);
        await store.SetAsync(named, TypeInfo, doc, version: 5, name: "backup");
        var fetchedUnnamed = await store.GetAsync<Dummy>(TypeInfo, doc, version: 5);
        var fetchedNamed = await store.GetAsync<Dummy>(TypeInfo, doc, version: 5, name: "backup");

        // Assert
        Assert.Same(unnamed, fetchedUnnamed);
        Assert.Same(named, fetchedNamed);
    }

    [Fact]
    public async Task GetAsync_with_name_should_return_null_when_only_unnamed_exists()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        var obj = new Dummy { V = 1 };
        await store.SetAsync(obj, TypeInfo, doc, version: 5);

        // Act
        var result = await store.GetAsync<Dummy>(TypeInfo, doc, version: 5, name: "nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_nongeneric_with_name_should_return_correct_snapshot()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        var named = new Dummy { V = 42 };
        await store.SetAsync(named, TypeInfo, doc, version: 3, name: "special");

        // Act
        var result = await store.GetAsync(TypeInfo, doc, version: 3, name: "special");

        // Assert
        Assert.Same(named, result);
    }

    [Fact]
    public async Task GetAsync_nongeneric_with_name_should_return_null_when_missing()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();

        // Act
        var result = await store.GetAsync(TypeInfo, doc, version: 3, name: "missing");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_multiple_versions_should_be_independently_retrievable()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        var v1 = new Dummy { V = 1 };
        var v2 = new Dummy { V = 2 };
        var v3 = new Dummy { V = 3 };

        // Act
        await store.SetAsync(v1, TypeInfo, doc, version: 10);
        await store.SetAsync(v2, TypeInfo, doc, version: 20);
        await store.SetAsync(v3, TypeInfo, doc, version: 30);

        // Assert
        Assert.Same(v1, await store.GetAsync<Dummy>(TypeInfo, doc, version: 10));
        Assert.Same(v2, await store.GetAsync<Dummy>(TypeInfo, doc, version: 20));
        Assert.Same(v3, await store.GetAsync<Dummy>(TypeInfo, doc, version: 30));
    }

    #endregion

    #region ListSnapshotsAsync

    [Fact]
    public async Task ListSnapshotsAsync_should_return_empty_when_no_snapshots()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();

        // Act
        var result = await store.ListSnapshotsAsync(doc);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ListSnapshotsAsync_should_return_all_snapshots_for_document()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);
        await store.SetAsync(new Dummy { V = 2 }, TypeInfo, doc, version: 10);
        await store.SetAsync(new Dummy { V = 3 }, TypeInfo, doc, version: 15);

        // Act
        var result = await store.ListSnapshotsAsync(doc);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task ListSnapshotsAsync_should_return_snapshots_ordered_by_version_descending()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);
        await store.SetAsync(new Dummy { V = 2 }, TypeInfo, doc, version: 15);
        await store.SetAsync(new Dummy { V = 3 }, TypeInfo, doc, version: 10);

        // Act
        var result = await store.ListSnapshotsAsync(doc);

        // Assert
        Assert.Equal(15, result[0].Version);
        Assert.Equal(10, result[1].Version);
        Assert.Equal(5, result[2].Version);
    }

    [Fact]
    public async Task ListSnapshotsAsync_should_include_named_snapshots()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);
        await store.SetAsync(new Dummy { V = 2 }, TypeInfo, doc, version: 5, name: "backup");

        // Act
        var result = await store.ListSnapshotsAsync(doc);

        // Assert
        Assert.Equal(2, result.Count);
        // One should be named, one unnamed
        Assert.Contains(result, s => s.Name == null);
        Assert.Contains(result, s => s.Name == "backup");
    }

    [Fact]
    public async Task ListSnapshotsAsync_should_not_include_snapshots_from_other_documents()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc1 = CreateDoc("stream-A");
        var doc2 = CreateDoc("stream-B");
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc1, version: 5);
        await store.SetAsync(new Dummy { V = 2 }, TypeInfo, doc2, version: 10);

        // Act
        var result1 = await store.ListSnapshotsAsync(doc1);
        var result2 = await store.ListSnapshotsAsync(doc2);

        // Assert
        Assert.Single(result1);
        Assert.Equal(5, result1[0].Version);
        Assert.Single(result2);
        Assert.Equal(10, result2[0].Version);
    }

    [Fact]
    public async Task ListSnapshotsAsync_metadata_should_have_correct_version()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 42);

        // Act
        var result = await store.ListSnapshotsAsync(doc);

        // Assert
        Assert.Single(result);
        Assert.Equal(42, result[0].Version);
    }

    [Fact]
    public async Task ListSnapshotsAsync_metadata_should_have_createdAt_timestamp()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        var before = DateTimeOffset.UtcNow;
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 1);
        var after = DateTimeOffset.UtcNow;

        // Act
        var result = await store.ListSnapshotsAsync(doc);

        // Assert
        Assert.Single(result);
        Assert.InRange(result[0].CreatedAt, before, after);
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_should_return_true_when_snapshot_exists()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);

        // Act
        var result = await store.DeleteAsync(doc, version: 5);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteAsync_should_return_false_when_snapshot_does_not_exist()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();

        // Act
        var result = await store.DeleteAsync(doc, version: 99);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_should_remove_snapshot_so_get_returns_null()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);

        // Act
        await store.DeleteAsync(doc, version: 5);
        var result = await store.GetAsync<Dummy>(TypeInfo, doc, version: 5);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_with_name_should_only_delete_named_snapshot()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);
        await store.SetAsync(new Dummy { V = 2 }, TypeInfo, doc, version: 5, name: "backup");

        // Act
        var deleted = await store.DeleteAsync(doc, version: 5, name: "backup");
        var unnamed = await store.GetAsync<Dummy>(TypeInfo, doc, version: 5);
        var named = await store.GetAsync<Dummy>(TypeInfo, doc, version: 5, name: "backup");

        // Assert
        Assert.True(deleted);
        Assert.NotNull(unnamed);
        Assert.Null(named);
    }

    [Fact]
    public async Task DeleteAsync_should_not_affect_other_versions()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);
        await store.SetAsync(new Dummy { V = 2 }, TypeInfo, doc, version: 10);

        // Act
        await store.DeleteAsync(doc, version: 5);
        var v5 = await store.GetAsync<Dummy>(TypeInfo, doc, version: 5);
        var v10 = await store.GetAsync<Dummy>(TypeInfo, doc, version: 10);

        // Assert
        Assert.Null(v5);
        Assert.NotNull(v10);
    }

    [Fact]
    public async Task DeleteAsync_should_be_idempotent()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);

        // Act
        var first = await store.DeleteAsync(doc, version: 5);
        var second = await store.DeleteAsync(doc, version: 5);

        // Assert
        Assert.True(first);
        Assert.False(second);
    }

    #endregion

    #region DeleteManyAsync

    [Fact]
    public async Task DeleteManyAsync_should_return_count_of_deleted_snapshots()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);
        await store.SetAsync(new Dummy { V = 2 }, TypeInfo, doc, version: 10);
        await store.SetAsync(new Dummy { V = 3 }, TypeInfo, doc, version: 15);

        // Act
        var deleted = await store.DeleteManyAsync(doc, [5, 10]);

        // Assert
        Assert.Equal(2, deleted);
    }

    [Fact]
    public async Task DeleteManyAsync_should_return_zero_when_none_exist()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();

        // Act
        var deleted = await store.DeleteManyAsync(doc, [1, 2, 3]);

        // Assert
        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task DeleteManyAsync_should_remove_only_specified_versions()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);
        await store.SetAsync(new Dummy { V = 2 }, TypeInfo, doc, version: 10);
        await store.SetAsync(new Dummy { V = 3 }, TypeInfo, doc, version: 15);

        // Act
        await store.DeleteManyAsync(doc, [5, 15]);
        var v5 = await store.GetAsync<Dummy>(TypeInfo, doc, version: 5);
        var v10 = await store.GetAsync<Dummy>(TypeInfo, doc, version: 10);
        var v15 = await store.GetAsync<Dummy>(TypeInfo, doc, version: 15);

        // Assert
        Assert.Null(v5);
        Assert.NotNull(v10);
        Assert.Null(v15);
    }

    [Fact]
    public async Task DeleteManyAsync_should_handle_partial_matches()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);

        // Act - version 5 exists, 99 does not
        var deleted = await store.DeleteManyAsync(doc, [5, 99]);

        // Assert
        Assert.Equal(1, deleted);
    }

    [Fact]
    public async Task DeleteManyAsync_should_handle_empty_version_list()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);

        // Act
        var deleted = await store.DeleteManyAsync(doc, []);

        // Assert
        Assert.Equal(0, deleted);
        // Snapshot should still exist
        var result = await store.GetAsync<Dummy>(TypeInfo, doc, version: 5);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DeleteManyAsync_should_reflect_in_ListSnapshotsAsync()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        await store.SetAsync(new Dummy { V = 1 }, TypeInfo, doc, version: 5);
        await store.SetAsync(new Dummy { V = 2 }, TypeInfo, doc, version: 10);
        await store.SetAsync(new Dummy { V = 3 }, TypeInfo, doc, version: 15);

        // Act
        await store.DeleteManyAsync(doc, [5, 10]);
        var remaining = await store.ListSnapshotsAsync(doc);

        // Assert
        Assert.Single(remaining);
        Assert.Equal(15, remaining[0].Version);
    }

    #endregion
}
