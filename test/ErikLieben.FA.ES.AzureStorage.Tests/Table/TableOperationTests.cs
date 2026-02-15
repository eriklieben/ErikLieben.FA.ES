#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Table;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableOperationTests
{
    public class UpsertMethod
    {
        [Fact]
        public void Should_create_upsert_operation_with_entity()
        {
            // Arrange
            var entity = new TestTableEntity
            {
                PartitionKey = "pk",
                RowKey = "rk"
            };

            // Act
            var sut = TableOperation.Upsert(entity);

            // Assert
            Assert.Equal(TableOperationType.Upsert, sut.Type);
            Assert.Equal("pk", sut.PartitionKey);
            Assert.Equal("rk", sut.RowKey);
            Assert.Same(entity, sut.Entity);
        }

        [Fact]
        public void Should_throw_when_entity_is_null()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => TableOperation.Upsert<TestTableEntity>(null!));
        }
    }

    public class DeleteMethod
    {
        [Fact]
        public void Should_create_delete_operation_with_keys()
        {
            // Arrange
            string partitionKey = "pk";
            string rowKey = "rk";

            // Act
            var sut = TableOperation.Delete(partitionKey, rowKey);

            // Assert
            Assert.Equal(TableOperationType.Delete, sut.Type);
            Assert.Equal("pk", sut.PartitionKey);
            Assert.Equal("rk", sut.RowKey);
            Assert.Null(sut.Entity);
        }

        [Fact]
        public void Should_throw_when_partition_key_is_null()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => TableOperation.Delete(null!, "rk"));
        }

        [Fact]
        public void Should_throw_when_row_key_is_null()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => TableOperation.Delete("pk", null!));
        }
    }

    private class TestTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
