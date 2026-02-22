using System.Text.Json;
using ErikLieben.FA.ES.CosmosDb.Model;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Model;

public class CosmosDbJsonEventTests
{
    public class Payload
    {
        [Fact]
        public void Should_return_empty_object_when_base_payload_is_null()
        {
            var sut = new CosmosDbJsonEvent { EventType = "Test", EventVersion = 0 };
            Assert.Equal("{}", sut.Payload);
        }

        [Fact]
        public void Should_return_set_payload()
        {
            var sut = new CosmosDbJsonEvent { EventType = "Test", EventVersion = 0, Payload = "{\"test\":123}" };
            Assert.Equal("{\"test\":123}", sut.Payload);
        }
    }

    public class FromMethod
    {
        [Fact]
        public void Should_return_null_when_event_is_not_json_event()
        {
            var nonJsonEvent = NSubstitute.Substitute.For<IEvent>();
            var result = CosmosDbJsonEvent.From(nonJsonEvent);
            Assert.Null(result);
        }

        [Fact]
        public void Should_return_same_instance_when_already_cosmos_db_json_event()
        {
            var cosmosDbEvent = new CosmosDbJsonEvent { EventType = "Test", EventVersion = 0 };
            var result = CosmosDbJsonEvent.From(cosmosDbEvent);
            Assert.Same(cosmosDbEvent, result);
        }

        [Fact]
        public void Should_convert_json_event_to_cosmos_db_json_event()
        {
            var jsonEvent = new JsonEvent
            {
                EventType = "TestEvent",
                EventVersion = 5,
                SchemaVersion = 2,
                Payload = "{\"data\":\"test\"}"
            };

            var result = CosmosDbJsonEvent.From(jsonEvent);

            Assert.NotNull(result);
            Assert.Equal("TestEvent", result.EventType);
            Assert.Equal(5, result.EventVersion);
            Assert.Equal(2, result.SchemaVersion);
            Assert.Equal("{\"data\":\"test\"}", result.Payload);
        }
    }

    public class FromEntityMethod
    {
        [Fact]
        public void Should_convert_entity_to_cosmos_db_json_event()
        {
            var entity = new CosmosDbEventEntity
            {
                Id = "stream_1",
                StreamId = "stream",
                Version = 1,
                EventType = "TestEvent",
                SchemaVersion = 2,
                Data = "{\"value\":42}",
                Timestamp = DateTimeOffset.UtcNow
            };

            var result = CosmosDbJsonEvent.FromEntity(entity);

            Assert.Equal("TestEvent", result.EventType);
            Assert.Equal(1, result.EventVersion);
            Assert.Equal(2, result.SchemaVersion);
            Assert.Equal("{\"value\":42}", result.Payload);
        }

        [Fact]
        public void Should_handle_empty_data()
        {
            var entity = new CosmosDbEventEntity
            {
                Id = "stream_1",
                StreamId = "stream",
                Version = 1,
                EventType = "TestEvent",
                Data = ""
            };

            var result = CosmosDbJsonEvent.FromEntity(entity);

            Assert.NotNull(result);
            Assert.Equal("", result.Payload);
        }
    }

    public class ToEntityMethod
    {
        [Fact]
        public void Should_convert_cosmos_db_json_event_to_entity()
        {
            var sut = new CosmosDbJsonEvent
            {
                EventType = "TestEvent",
                EventVersion = 5,
                SchemaVersion = 2,
                Payload = "{\"data\":\"test\"}"
            };

            var result = sut.ToEntity("test-stream");

            Assert.Equal("test-stream", result.StreamId);
            Assert.Equal(5, result.Version);
            Assert.Equal("TestEvent", result.EventType);
            Assert.Equal(2, result.SchemaVersion);
            Assert.Equal("{\"data\":\"test\"}", result.Data);
        }

        [Fact]
        public void Should_preserve_original_timestamp_when_flag_is_true()
        {
            var originalTimestamp = new DateTimeOffset(2023, 1, 15, 10, 30, 0, TimeSpan.Zero);
            var sut = new CosmosDbJsonEvent
            {
                EventType = "TestEvent",
                EventVersion = 1,
                OriginalTimestamp = originalTimestamp
            };

            var result = sut.ToEntity("test-stream", preserveTimestamp: true);

            Assert.Equal(originalTimestamp, result.Timestamp);
        }

        [Fact]
        public void Should_use_current_timestamp_when_preserve_is_false()
        {
            var sut = new CosmosDbJsonEvent
            {
                EventType = "TestEvent",
                EventVersion = 1,
                OriginalTimestamp = new DateTimeOffset(2023, 1, 15, 10, 30, 0, TimeSpan.Zero)
            };

            var before = DateTimeOffset.UtcNow;
            var result = sut.ToEntity("test-stream", preserveTimestamp: false);
            var after = DateTimeOffset.UtcNow;

            Assert.InRange(result.Timestamp, before, after);
        }

        [Fact]
        public void Should_create_correct_id_format()
        {
            var sut = new CosmosDbJsonEvent
            {
                EventType = "TestEvent",
                EventVersion = 42
            };

            var result = sut.ToEntity("test-stream");

            Assert.Equal("test-stream_00000000000000000042", result.Id);
        }
    }
}
