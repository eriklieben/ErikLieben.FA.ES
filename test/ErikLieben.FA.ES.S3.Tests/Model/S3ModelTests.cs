using System.Text;
using System.Text.Json;
using ErikLieben.FA.ES.S3.Model;
using NSubstitute;

namespace ErikLieben.FA.ES.S3.Tests.Model;

public class S3ModelTests
{
    public class S3DataStoreDocumentTests
    {
        [Fact]
        public void Should_have_default_events_list()
        {
            var doc = new S3DataStoreDocument
            {
                ObjectId = "test",
                ObjectName = "test",
                LastObjectDocumentHash = "*"
            };
            Assert.NotNull(doc.Events);
            Assert.Empty(doc.Events);
        }

        [Fact]
        public void Should_set_object_id()
        {
            var doc = new S3DataStoreDocument
            {
                ObjectId = "test-id",
                ObjectName = "test",
                LastObjectDocumentHash = "*"
            };
            Assert.Equal("test-id", doc.ObjectId);
        }

        [Fact]
        public void Should_set_object_name()
        {
            var doc = new S3DataStoreDocument
            {
                ObjectId = "test",
                ObjectName = "test-name",
                LastObjectDocumentHash = "*"
            };
            Assert.Equal("test-name", doc.ObjectName);
        }

        [Fact]
        public void Should_default_terminated_to_false()
        {
            var doc = new S3DataStoreDocument
            {
                ObjectId = "test",
                ObjectName = "test",
                LastObjectDocumentHash = "*"
            };
            Assert.False(doc.Terminated);
        }

        [Fact]
        public void Should_default_schema_version()
        {
            var doc = new S3DataStoreDocument
            {
                ObjectId = "test",
                ObjectName = "test",
                LastObjectDocumentHash = "*"
            };
            Assert.Equal("1.0.0", doc.SchemaVersion);
        }
    }

    public class S3DocumentTagStoreDocumentTests
    {
        [Fact]
        public void Should_have_default_object_ids_list()
        {
            var doc = new S3DocumentTagStoreDocument { Tag = "test" };
            Assert.NotNull(doc.ObjectIds);
            Assert.Empty(doc.ObjectIds);
        }

        [Fact]
        public void Should_set_tag()
        {
            var doc = new S3DocumentTagStoreDocument { Tag = "my-tag" };
            Assert.Equal("my-tag", doc.Tag);
        }

        [Fact]
        public void Should_default_schema_version()
        {
            var doc = new S3DocumentTagStoreDocument { Tag = "test" };
            Assert.Equal("1.0.0", doc.SchemaVersion);
        }
    }

    public class S3JsonEventTests
    {
        public class From
        {
            [Fact]
            public void Should_return_null_when_event_is_not_json_event()
            {
                var nonJsonEvent = Substitute.For<IEvent>();

                var result = S3JsonEvent.From(nonJsonEvent);

                Assert.Null(result);
            }

            [Fact]
            public void Should_return_new_s3_json_event_from_json_event()
            {
                var jsonEvent = new JsonEvent
                {
                    EventType = "Test.Created",
                    EventVersion = 1,
                    Payload = "{\"name\":\"test\"}",
                    SchemaVersion = 2
                };
                jsonEvent.ActionMetadata = new ActionMetadata(CorrelationId: "corr-1");
                jsonEvent.Metadata = new Dictionary<string, string> { ["key"] = "value" };

                var result = S3JsonEvent.From(jsonEvent);

                Assert.NotNull(result);
                Assert.Equal("Test.Created", result!.EventType);
                Assert.Equal(1, result.EventVersion);
                Assert.Equal("{\"name\":\"test\"}", result.Payload);
                Assert.Equal(2, result.SchemaVersion);
                Assert.Equal("corr-1", result.ActionMetadata.CorrelationId);
                Assert.Equal("value", result.Metadata["key"]);
                Assert.True(result.Timestamp <= DateTimeOffset.UtcNow);
                Assert.True(result.Timestamp > DateTimeOffset.UtcNow.AddSeconds(-5));
            }

            [Fact]
            public void Should_preserve_timestamp_when_flag_set()
            {
                var originalTimestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
                var s3Event = new S3JsonEvent
                {
                    Timestamp = originalTimestamp,
                    EventType = "Test.Created",
                    EventVersion = 1,
                    Payload = "{\"name\":\"test\"}"
                };

                var result = S3JsonEvent.From(s3Event, preserveTimestamp: true);

                Assert.NotNull(result);
                Assert.Equal(originalTimestamp, result!.Timestamp);
            }

            [Fact]
            public void Should_set_new_timestamp_when_flag_false()
            {
                var originalTimestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
                var s3Event = new S3JsonEvent
                {
                    Timestamp = originalTimestamp,
                    EventType = "Test.Created",
                    EventVersion = 1,
                    Payload = "{\"name\":\"test\"}"
                };

                var result = S3JsonEvent.From(s3Event, preserveTimestamp: false);

                Assert.NotNull(result);
                Assert.NotEqual(originalTimestamp, result!.Timestamp);
                Assert.True(result.Timestamp <= DateTimeOffset.UtcNow);
                Assert.True(result.Timestamp > DateTimeOffset.UtcNow.AddSeconds(-5));
            }

            [Fact]
            public void Should_use_empty_payload_when_null()
            {
                var jsonEvent = new JsonEvent
                {
                    EventType = "Test.Created",
                    EventVersion = 1,
                    Payload = null
                };

                var result = S3JsonEvent.From(jsonEvent);

                Assert.NotNull(result);
                Assert.Equal("{}", result!.Payload);
            }
        }

        public class Payload
        {
            [Fact]
            public void Should_return_empty_json_object_when_base_payload_is_null()
            {
                var s3Event = new S3JsonEvent
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    EventType = "Test.Created",
                    EventVersion = 1
                };
                // Set base payload to null via the base record property
                ((JsonEvent)s3Event).Payload = null;

                Assert.Equal("{}", s3Event.Payload);
            }

            [Fact]
            public void Should_return_actual_payload_when_set()
            {
                var s3Event = new S3JsonEvent
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    EventType = "Test.Created",
                    EventVersion = 1,
                    Payload = "{\"id\":42}"
                };

                Assert.Equal("{\"id\":42}", s3Event.Payload);
            }
        }
    }

    public class S3PayloadConverterTests
    {
        [Fact]
        public void Should_read_json_object_as_raw_text()
        {
            var json = "{\"payload\":{\"name\":\"test\",\"count\":5}}"u8;
            var reader = new Utf8JsonReader(json);

            // Advance to the "payload" property name
            reader.Read(); // StartObject
            reader.Read(); // PropertyName "payload"
            reader.Read(); // StartObject (the value)

            var converter = new S3PayloadConverter();
            var result = converter.Read(ref reader, typeof(string), new JsonSerializerOptions());

            Assert.Equal("{\"name\":\"test\",\"count\":5}", result);
        }

        [Fact]
        public void Should_write_json_string_as_json_object()
        {
            var payloadString = "{\"name\":\"test\",\"count\":5}";

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            var converter = new S3PayloadConverter();
            converter.Write(writer, payloadString, new JsonSerializerOptions());
            writer.Flush();

            var written = Encoding.UTF8.GetString(stream.ToArray());

            // The output should be a JSON object, not a double-quoted string
            Assert.Equal("{\"name\":\"test\",\"count\":5}", written);
            Assert.DoesNotContain("\"{\\'", written); // Not double-escaped
            Assert.StartsWith("{", written);
        }
    }
}
