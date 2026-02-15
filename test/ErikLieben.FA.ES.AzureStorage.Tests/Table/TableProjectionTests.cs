#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.Projections;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableProjectionTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_with_zero_pending_operations()
        {
            // Arrange & Act
            var sut = new TestTableProjection();

            // Assert
            Assert.Equal(0, sut.PendingOperationCount);
        }

        [Fact]
        public void Should_inherit_from_projection()
        {
            // Arrange & Act
            var sut = new TestTableProjection();

            // Assert
            Assert.IsType<TestTableProjection>(sut);
        }
    }

    public class UpsertEntityMethod
    {
        [Fact]
        public void Should_increment_pending_operation_count()
        {
            // Arrange
            var sut = new TestTableProjection();
            var entity = new TestTableEntity
            {
                PartitionKey = "pk",
                RowKey = "rk"
            };

            // Act
            sut.TestUpsertEntity(entity);

            // Assert
            Assert.Equal(1, sut.PendingOperationCount);
        }

        [Fact]
        public void Should_throw_when_entity_is_null()
        {
            // Arrange
            var sut = new TestTableProjection();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.TestUpsertEntity<TestTableEntity>(null!));
        }

        [Fact]
        public void Should_allow_multiple_upserts()
        {
            // Arrange
            var sut = new TestTableProjection();
            var entity1 = new TestTableEntity { PartitionKey = "pk1", RowKey = "rk1" };
            var entity2 = new TestTableEntity { PartitionKey = "pk2", RowKey = "rk2" };

            // Act
            sut.TestUpsertEntity(entity1);
            sut.TestUpsertEntity(entity2);

            // Assert
            Assert.Equal(2, sut.PendingOperationCount);
        }
    }

    public class DeleteEntityMethod
    {
        [Fact]
        public void Should_increment_pending_operation_count()
        {
            // Arrange
            var sut = new TestTableProjection();

            // Act
            sut.TestDeleteEntity("pk", "rk");

            // Assert
            Assert.Equal(1, sut.PendingOperationCount);
        }

        [Fact]
        public void Should_throw_when_partition_key_is_null()
        {
            // Arrange
            var sut = new TestTableProjection();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.TestDeleteEntity(null!, "rk"));
        }

        [Fact]
        public void Should_throw_when_row_key_is_null()
        {
            // Arrange
            var sut = new TestTableProjection();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.TestDeleteEntity("pk", null!));
        }
    }

    public class CheckpointProperty
    {
        [Fact]
        public void Should_have_default_checkpoint()
        {
            // Arrange & Act
            var sut = new TestTableProjection();

            // Assert
            Assert.NotNull(sut.Checkpoint);
        }

        [Fact]
        public void Should_allow_setting_checkpoint()
        {
            // Arrange
            var sut = new TestTableProjection();
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
            var sut = new TestTableProjection();

            // Act
            var json = sut.ToJson();

            // Assert
            Assert.NotNull(json);
            Assert.Contains("$checkpoint", json);
        }
    }

    private class TestTableProjection : TableProjection
    {
        public void TestUpsertEntity<TEntity>(TEntity entity) where TEntity : ITableEntity
            => UpsertEntity(entity);

        public void TestDeleteEntity(string partitionKey, string rowKey)
            => DeleteEntity(partitionKey, rowKey);
    }

    private class TestTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
