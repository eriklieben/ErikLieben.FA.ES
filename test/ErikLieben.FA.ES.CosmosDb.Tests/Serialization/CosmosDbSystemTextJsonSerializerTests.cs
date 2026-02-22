using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.CosmosDb.Model;
using ErikLieben.FA.ES.CosmosDb.Serialization;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Serialization;

public class CosmosDbSystemTextJsonSerializerTests
{
    public class ConstructorTests
    {
        [Fact]
        public void Should_create_instance_with_default_options()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();

            Assert.NotNull(serializer);
        }

        [Fact]
        public void Should_throw_when_options_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbSystemTextJsonSerializer(null!));
        }

        [Fact]
        public void Should_create_instance_with_custom_options()
        {
            var options = new JsonSerializerOptions();

            var serializer = new CosmosDbSystemTextJsonSerializer(options);

            Assert.NotNull(serializer);
        }
    }

    public class CreateDefaultOptionsMethod
    {
        [Fact]
        public void Should_return_non_null_options()
        {
            var options = CosmosDbSystemTextJsonSerializer.CreateDefaultOptions();

            Assert.NotNull(options);
        }

        [Fact]
        public void Should_use_camel_case_naming_policy()
        {
            var options = CosmosDbSystemTextJsonSerializer.CreateDefaultOptions();

            Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        }

        [Fact]
        public void Should_ignore_null_values_when_writing()
        {
            var options = CosmosDbSystemTextJsonSerializer.CreateDefaultOptions();

            Assert.Equal(JsonIgnoreCondition.WhenWritingNull, options.DefaultIgnoreCondition);
        }

        [Fact]
        public void Should_not_write_indented()
        {
            var options = CosmosDbSystemTextJsonSerializer.CreateDefaultOptions();

            Assert.False(options.WriteIndented);
        }

        [Fact]
        public void Should_have_type_info_resolver()
        {
            var options = CosmosDbSystemTextJsonSerializer.CreateDefaultOptions();

            Assert.NotNull(options.TypeInfoResolver);
        }
    }

    public class FromStreamMethod
    {
        [Fact]
        public void Should_return_default_for_null_stream()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();

            // The using block inside FromStream will handle null by returning default
            // We need to wrap null in a way that passes the using statement
            // Actually, the implementation checks for null inside using (stream), so null stream returns default
            var result = serializer.FromStream<CosmosDbEventEntity>(null!);

            Assert.Null(result);
        }

        [Fact]
        public void Should_return_default_for_empty_stream()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var stream = new MemoryStream();

            var result = serializer.FromStream<CosmosDbEventEntity>(stream);

            Assert.Null(result);
        }

        [Fact]
        public void Should_deserialize_valid_json_stream()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var entity = new CosmosDbEventEntity
            {
                Id = "test-id",
                StreamId = "stream-1",
                Version = 5,
                EventType = "TestEvent",
                Data = "{\"key\":\"value\"}"
            };

            var json = JsonSerializer.Serialize(entity, CosmosDbSystemTextJsonSerializer.CreateDefaultOptions());
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var result = serializer.FromStream<CosmosDbEventEntity>(stream);

            Assert.NotNull(result);
            Assert.Equal("test-id", result.Id);
            Assert.Equal("stream-1", result.StreamId);
            Assert.Equal(5, result.Version);
            Assert.Equal("TestEvent", result.EventType);
        }

        [Fact]
        public void Should_handle_seekable_stream_not_at_beginning()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var entity = new CosmosDbEventEntity
            {
                Id = "test-id",
                StreamId = "stream-1",
                Version = 1,
                EventType = "TestEvent",
                Data = "{}"
            };

            var json = JsonSerializer.Serialize(entity, CosmosDbSystemTextJsonSerializer.CreateDefaultOptions());
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            stream.Position = stream.Length; // Move to end

            var result = serializer.FromStream<CosmosDbEventEntity>(stream);

            Assert.NotNull(result);
            Assert.Equal("test-id", result.Id);
        }

        [Fact]
        public void Should_dispose_stream_after_reading()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var json = "{\"id\":\"test\",\"streamId\":\"s1\",\"version\":0,\"eventType\":\"E\",\"data\":\"{}\"}";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            serializer.FromStream<CosmosDbEventEntity>(stream);

            // Stream should be disposed - attempting to read should throw
            Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
        }

        [Fact]
        public void Should_handle_seekable_empty_stream_with_length_zero()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var stream = new MemoryStream(Array.Empty<byte>());

            var result = serializer.FromStream<CosmosDbEventEntity>(stream);

            Assert.Null(result);
        }
    }

    public class ToStreamMethod
    {
        [Fact]
        public void Should_serialize_entity_to_stream()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var entity = new CosmosDbEventEntity
            {
                Id = "test-id",
                StreamId = "stream-1",
                Version = 3,
                EventType = "TestEvent",
                Data = "{\"value\":42}"
            };

            using var stream = serializer.ToStream(entity);

            Assert.NotNull(stream);
            Assert.True(stream.Length > 0);
            Assert.Equal(0, stream.Position);
        }

        [Fact]
        public void Should_produce_valid_json()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var entity = new CosmosDbEventEntity
            {
                Id = "test-id",
                StreamId = "stream-1",
                Version = 3,
                EventType = "TestEvent",
                Data = "{}"
            };

            using var stream = serializer.ToStream(entity);
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("id", out _));
            Assert.True(doc.RootElement.TryGetProperty("streamId", out _));
        }

        [Fact]
        public void Should_set_stream_position_to_zero()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var entity = new CosmosDbEventEntity { Id = "test" };

            using var stream = serializer.ToStream(entity);

            Assert.Equal(0, stream.Position);
        }

        [Fact]
        public void Should_round_trip_entity()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var original = new CosmosDbEventEntity
            {
                Id = "round-trip-id",
                StreamId = "stream-42",
                Version = 10,
                EventType = "RoundTripEvent",
                Data = "{\"foo\":\"bar\"}",
                Timestamp = DateTimeOffset.Parse("2025-01-01T00:00:00Z")
            };

            using var stream = serializer.ToStream(original);
            var result = serializer.FromStream<CosmosDbEventEntity>(stream);

            Assert.NotNull(result);
            Assert.Equal(original.Id, result.Id);
            Assert.Equal(original.StreamId, result.StreamId);
            Assert.Equal(original.Version, result.Version);
            Assert.Equal(original.EventType, result.EventType);
            Assert.Equal(original.Data, result.Data);
        }
    }

    public class SerializeMemberNameMethod
    {
        [Fact]
        public void Should_throw_when_member_info_is_null()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();

            Assert.Throws<ArgumentNullException>(() =>
                serializer.SerializeMemberName(null!));
        }

        [Fact]
        public void Should_return_json_property_name_when_attribute_present()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var property = typeof(CosmosDbEventEntity).GetProperty(nameof(CosmosDbEventEntity.Type))!;

            var result = serializer.SerializeMemberName(property);

            Assert.Equal("_type", result);
        }

        [Fact]
        public void Should_apply_camel_case_naming_policy_when_no_attribute()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var property = typeof(CosmosDbEventEntity).GetProperty(nameof(CosmosDbEventEntity.StreamId))!;

            var result = serializer.SerializeMemberName(property);

            Assert.Equal("streamId", result);
        }

        [Fact]
        public void Should_return_original_name_when_no_attribute_and_no_policy()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null
            };
            var serializer = new CosmosDbSystemTextJsonSerializer(options);
            // Use a property without [JsonPropertyName] attribute
            var property = typeof(PlainTestDto).GetProperty(nameof(PlainTestDto.MyProperty))!;

            var result = serializer.SerializeMemberName(property);

            Assert.Equal("MyProperty", result);
        }

        [Fact]
        public void Should_return_explicit_name_for_id_property()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var property = typeof(CosmosDbEventEntity).GetProperty(nameof(CosmosDbEventEntity.Id))!;

            var result = serializer.SerializeMemberName(property);

            Assert.Equal("id", result);
        }

        [Fact]
        public void Should_handle_projection_document_properties()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var property = typeof(ProjectionDocument).GetProperty(nameof(ProjectionDocument.ProjectionName))!;

            var result = serializer.SerializeMemberName(property);

            Assert.Equal("projectionName", result);
        }

        [Fact]
        public void Should_handle_checkpoint_document_properties()
        {
            var serializer = new CosmosDbSystemTextJsonSerializer();
            var property = typeof(CheckpointDocument).GetProperty(nameof(CheckpointDocument.Fingerprint))!;

            var result = serializer.SerializeMemberName(property);

            Assert.Equal("fingerprint", result);
        }
    }

    /// <summary>
    /// A plain DTO without any JSON attributes for testing the naming policy fallback.
    /// </summary>
    private class PlainTestDto
    {
        public string MyProperty { get; set; } = string.Empty;
    }
}
