using ErikLieben.FA.ES.CosmosDb.Model;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Model;

public class CosmosDbDocumentEntityTests
{
    public class CreateIdMethod
    {
        [Fact]
        public void Should_create_id_with_lowercase_object_name()
        {
            var result = CosmosDbDocumentEntity.CreateId("TestObject", "test-id-123");
            Assert.Equal("testobject_test-id-123", result);
        }

        [Fact]
        public void Should_preserve_object_id_case()
        {
            var result = CosmosDbDocumentEntity.CreateId("Order", "Order-ABC-123");
            Assert.Equal("order_Order-ABC-123", result);
        }
    }

    public class DefaultValues
    {
        [Fact]
        public void Should_have_empty_id_by_default()
        {
            var entity = new CosmosDbDocumentEntity();
            Assert.Equal(string.Empty, entity.Id);
        }

        [Fact]
        public void Should_have_document_type_by_default()
        {
            var entity = new CosmosDbDocumentEntity();
            Assert.Equal("document", entity.Type);
        }

        [Fact]
        public void Should_have_empty_terminated_streams_by_default()
        {
            var entity = new CosmosDbDocumentEntity();
            Assert.Empty(entity.TerminatedStreams);
        }

        [Fact]
        public void Should_have_default_active_stream_info()
        {
            var entity = new CosmosDbDocumentEntity();
            Assert.NotNull(entity.Active);
        }
    }
}

public class CosmosDbStreamInfoTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_cosmosdb_stream_type_by_default()
        {
            var info = new CosmosDbStreamInfo();
            Assert.Equal("cosmosdb", info.StreamType);
        }

        [Fact]
        public void Should_have_cosmosdb_document_type_by_default()
        {
            var info = new CosmosDbStreamInfo();
            Assert.Equal("cosmosdb", info.DocumentType);
        }

        [Fact]
        public void Should_have_cosmosdb_data_store_by_default()
        {
            var info = new CosmosDbStreamInfo();
            Assert.Equal("cosmosdb", info.DataStore);
        }

        [Fact]
        public void Should_have_recent_created_at_by_default()
        {
            var before = DateTimeOffset.UtcNow;
            var info = new CosmosDbStreamInfo();
            var after = DateTimeOffset.UtcNow;
            Assert.InRange(info.CreatedAt, before, after);
        }

        [Fact]
        public void Should_have_zero_stream_version_by_default()
        {
            var info = new CosmosDbStreamInfo();
            Assert.Equal(0, info.CurrentStreamVersion);
        }

        [Fact]
        public void Should_have_cosmosdb_document_tag_store_by_default()
        {
            var info = new CosmosDbStreamInfo();
            Assert.Equal("cosmosdb", info.DocumentTagStore);
        }

        [Fact]
        public void Should_have_cosmosdb_stream_tag_store_by_default()
        {
            var info = new CosmosDbStreamInfo();
            Assert.Equal("cosmosdb", info.StreamTagStore);
        }

        [Fact]
        public void Should_have_cosmosdb_snapshot_store_by_default()
        {
            var info = new CosmosDbStreamInfo();
            Assert.Equal("cosmosdb", info.SnapShotStore);
        }
    }
}

public class CosmosDbTerminatedStreamInfoTests
{
    public class Properties
    {
        [Fact]
        public void Should_allow_setting_stream_identifier()
        {
            var info = new CosmosDbTerminatedStreamInfo { StreamIdentifier = "old-stream" };
            Assert.Equal("old-stream", info.StreamIdentifier);
        }

        [Fact]
        public void Should_allow_setting_final_version()
        {
            var info = new CosmosDbTerminatedStreamInfo { FinalVersion = 100 };
            Assert.Equal(100, info.FinalVersion);
        }

        [Fact]
        public void Should_allow_setting_reason()
        {
            var info = new CosmosDbTerminatedStreamInfo { Reason = "Migrated to new format" };
            Assert.Equal("Migrated to new format", info.Reason);
        }
    }
}

public class CosmosDbSnapshotEntityTests
{
    public class CreateIdMethod
    {
        [Fact]
        public void Should_create_id_without_name()
        {
            var result = CosmosDbSnapshotEntity.CreateId("stream-123", 42);
            Assert.Equal("stream-123_00000000000000000042", result);
        }

        [Fact]
        public void Should_create_id_with_name()
        {
            var result = CosmosDbSnapshotEntity.CreateId("stream-123", 42, "MyAggregate");
            Assert.Equal("stream-123_00000000000000000042_MyAggregate", result);
        }

        [Fact]
        public void Should_create_id_with_null_name()
        {
            var result = CosmosDbSnapshotEntity.CreateId("stream-123", 0, null);
            Assert.Equal("stream-123_00000000000000000000", result);
        }

        [Fact]
        public void Should_create_id_with_empty_name()
        {
            var result = CosmosDbSnapshotEntity.CreateId("stream-123", 1, "");
            Assert.Equal("stream-123_00000000000000000001", result);
        }

        [Fact]
        public void Should_pad_version_to_20_digits()
        {
            var result = CosmosDbSnapshotEntity.CreateId("s", 12345);
            Assert.Equal("s_00000000000000012345", result);
        }
    }

    public class DefaultValues
    {
        [Fact]
        public void Should_have_snapshot_type_by_default()
        {
            var entity = new CosmosDbSnapshotEntity();
            Assert.Equal("snapshot", entity.Type);
        }

        [Fact]
        public void Should_have_recent_created_at_by_default()
        {
            var before = DateTimeOffset.UtcNow;
            var entity = new CosmosDbSnapshotEntity();
            var after = DateTimeOffset.UtcNow;
            Assert.InRange(entity.CreatedAt, before, after);
        }
    }
}

public class CosmosDbTagEntityTests
{
    public class CreateIdMethod
    {
        [Fact]
        public void Should_create_id_with_all_parts()
        {
            var result = CosmosDbTagEntity.CreateId("document", "order", "priority", "order-123");
            Assert.Equal("document_order_priority_order-123", result);
        }

        [Fact]
        public void Should_create_stream_tag_id()
        {
            var result = CosmosDbTagEntity.CreateId("stream", "project", "status", "proj-456");
            Assert.Equal("stream_project_status_proj-456", result);
        }
    }

    public class CreateTagKeyMethod
    {
        [Fact]
        public void Should_create_tag_key_with_lowercase_object_name()
        {
            var result = CosmosDbTagEntity.CreateTagKey("OrderItem", "important");
            Assert.Equal("orderitem_important", result);
        }

        [Fact]
        public void Should_preserve_tag_case()
        {
            var result = CosmosDbTagEntity.CreateTagKey("Order", "Priority-High");
            Assert.Equal("order_Priority-High", result);
        }
    }

    public class DefaultValues
    {
        [Fact]
        public void Should_have_tag_type_by_default()
        {
            var entity = new CosmosDbTagEntity();
            Assert.Equal("tag", entity.Type);
        }

        [Fact]
        public void Should_have_recent_created_at_by_default()
        {
            var before = DateTimeOffset.UtcNow;
            var entity = new CosmosDbTagEntity();
            var after = DateTimeOffset.UtcNow;
            Assert.InRange(entity.CreatedAt, before, after);
        }
    }
}

public class CosmosDbEventEntityTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_empty_id_by_default()
        {
            var entity = new CosmosDbEventEntity();
            Assert.Equal(string.Empty, entity.Id);
        }

        [Fact]
        public void Should_have_event_type_by_default()
        {
            var entity = new CosmosDbEventEntity();
            Assert.Equal("event", entity.Type);
        }

        [Fact]
        public void Should_have_zero_version_by_default()
        {
            var entity = new CosmosDbEventEntity();
            Assert.Equal(0, entity.Version);
        }

        [Fact]
        public void Should_have_null_ttl_by_default()
        {
            var entity = new CosmosDbEventEntity();
            Assert.Null(entity.Ttl);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_allow_setting_ttl()
        {
            var entity = new CosmosDbEventEntity { Ttl = 3600 };
            Assert.Equal(3600, entity.Ttl);
        }

        [Fact]
        public void Should_allow_setting_schema_version()
        {
            var entity = new CosmosDbEventEntity { SchemaVersion = 2 };
            Assert.Equal(2, entity.SchemaVersion);
        }

        [Fact]
        public void Should_allow_setting_event_type()
        {
            var entity = new CosmosDbEventEntity { EventType = "OrderCreated" };
            Assert.Equal("OrderCreated", entity.EventType);
        }

        [Fact]
        public void Should_allow_setting_data()
        {
            var entity = new CosmosDbEventEntity { Data = "{\"orderId\":123}" };
            Assert.Equal("{\"orderId\":123}", entity.Data);
        }
    }
}
