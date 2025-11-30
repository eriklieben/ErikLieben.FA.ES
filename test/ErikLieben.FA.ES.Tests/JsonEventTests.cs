using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Exceptions;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests;

public class JsonEventTests
{
    [Fact]
    public void Should_be_null_When_From_is_called_with_non_JsonEvent()
    {
        // Arrange
        var @event = Substitute.For<IEvent>();

        // Act
        var result = JsonEvent.From(@event);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Should_return_JsonEvent_When_From_is_called_with_JsonEvent()
    {
        // Arrange
        var jsonEvent = new JsonEvent { Payload = "{}", EventType = "Test", EventVersion = 1 };

        // Act
        var result = JsonEvent.From(jsonEvent);

        // Assert
        Assert.Equal(jsonEvent, result);
    }

    [Fact]
    public void Should_throw_ArgumentNullException_When_ToEvent_is_called_with_null_typeInfo()
    {
        // Arrange
        var @event = Substitute.For<IEvent>();
        JsonTypeInfo<object>? typeInfo = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => JsonEvent.ToEvent(@event, typeInfo));
    }

    public class ToOfT
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_event_is_null()
        {
            // Act
           var exception =
               Assert.Throws<ArgumentNullException>(() => JsonEvent.To(null!, new TestContext().String));

            // Assert
            Assert.Equal("@event", exception.ParamName);
            Assert.Equal("Value cannot be null. (Parameter '@event')", exception.Message);
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_event_payload_is_null()
        {
            // Arrange
            var @event = Substitute.For<IEvent>();
            @event.Payload.Returns((string?)null!);

            // Act
           var exception =
               Assert.Throws<ArgumentNullException>(() => JsonEvent.To(@event, new TestContext().String));

            // Assert
            Assert.Equal("@event?.Payload", exception.ParamName);
            Assert.Equal("Value cannot be null. (Parameter '@event?.Payload')", exception.Message);
        }


        [Fact]
        public void Should_throw_ArgumentNullException_when_typeInfo_is_null()
        {
            // Arrange
            var @event = Substitute.For<IEvent>();
            @event.Payload.Returns(string.Empty);

            // Act
           var exception =
               Assert.Throws<ArgumentNullException>(() => JsonEvent.To<string>(@event, null!));

            // Assert
            Assert.Equal("typeInfo", exception.ParamName);
            Assert.Equal("Value cannot be null. (Parameter 'typeInfo')", exception.Message);
        }

        [Fact]
        public void Should_throw_UnableToDeserializeInTransitEventException_when_deserialization_fails()
        {
            // Arrange
            var invalidPayloadEvent = new JsonEvent
            {
                Payload = "null",
                EventType = "TestEvent",
                EventVersion = 1
            };

            // Act
            var exception = Assert.Throws<UnableToDeserializeInTransitEventException>(
                () => JsonEvent.To(invalidPayloadEvent, new TestContext().String)
            );

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(
                "[ELFAES-VAL-0001] Unable to deserialize to event, value is 'null'",
                exception.Message);
        }


        [Fact]
        public void Should_return_event_when_deserialization_succeeds()
        {
            // Arrange
            var @event = new JsonEvent
            {
                Payload = "[\"a\",\"b\"]",
                EventType = "TestEvent",
                EventVersion = 1,
            };

            // Act
            var result = JsonEvent.To(@event, new TestContext().ListString);

            // Assert
            Assert.NotNull(result);
        }
    }

    public class SchemaVersionTests
    {
        [Fact]
        public void Should_default_to_1_when_not_set()
        {
            // Arrange & Act
            var jsonEvent = new JsonEvent { Payload = "{}", EventType = "Test", EventVersion = 1 };

            // Assert
            Assert.Equal(1, jsonEvent.SchemaVersion);
        }

        [Fact]
        public void Should_return_1_when_internal_value_is_0()
        {
            // Arrange
            var jsonEvent = new JsonEvent { Payload = "{}", EventType = "Test", EventVersion = 1 };
            jsonEvent.SchemaVersionForSerialization = 0;

            // Act & Assert
            Assert.Equal(1, jsonEvent.SchemaVersion);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(100)]
        public void Should_return_actual_value_for_versions_greater_than_1(int version)
        {
            // Arrange
            var jsonEvent = new JsonEvent { Payload = "{}", EventType = "Test", EventVersion = 1, SchemaVersion = version };

            // Act & Assert
            Assert.Equal(version, jsonEvent.SchemaVersion);
        }

        [Fact]
        public void Should_store_0_internally_when_setting_version_1()
        {
            // Arrange
            var jsonEvent = new JsonEvent { Payload = "{}", EventType = "Test", EventVersion = 1 };

            // Act
            jsonEvent.SchemaVersion = 1;

            // Assert
            Assert.Equal(0, jsonEvent.SchemaVersionForSerialization);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(100)]
        public void Should_store_actual_value_internally_for_versions_greater_than_1(int version)
        {
            // Arrange
            var jsonEvent = new JsonEvent { Payload = "{}", EventType = "Test", EventVersion = 1 };

            // Act
            jsonEvent.SchemaVersion = version;

            // Assert
            Assert.Equal(version, jsonEvent.SchemaVersionForSerialization);
        }

        [Fact]
        public void Should_not_serialize_schema_version_when_version_is_1()
        {
            // Arrange
            var jsonEvent = new JsonEvent { Payload = "{}", EventType = "Test", EventVersion = 1, SchemaVersion = 1 };

            // Act
            var json = System.Text.Json.JsonSerializer.Serialize(jsonEvent);

            // Assert
            Assert.DoesNotContain("schemaVersion", json);
        }

        [Fact]
        public void Should_serialize_schema_version_when_version_is_greater_than_1()
        {
            // Arrange
            var jsonEvent = new JsonEvent { Payload = "{}", EventType = "Test", EventVersion = 1, SchemaVersion = 2 };

            // Act
            var json = System.Text.Json.JsonSerializer.Serialize(jsonEvent);

            // Assert
            Assert.Contains("\"schemaVersion\":2", json);
        }

        [Fact]
        public void Should_deserialize_schema_version_1_when_not_present_in_json()
        {
            // Arrange - note: "type" is the JSON property name for EventType
            var json = """{"payload":"{}","type":"Test","version":1}""";

            // Act
            var jsonEvent = System.Text.Json.JsonSerializer.Deserialize<JsonEvent>(json);

            // Assert
            Assert.NotNull(jsonEvent);
            Assert.Equal(1, jsonEvent.SchemaVersion);
        }

        [Fact]
        public void Should_deserialize_schema_version_when_present_in_json()
        {
            // Arrange - note: "type" is the JSON property name for EventType
            var json = """{"payload":"{}","type":"Test","version":1,"schemaVersion":3}""";

            // Act
            var jsonEvent = System.Text.Json.JsonSerializer.Deserialize<JsonEvent>(json);

            // Assert
            Assert.NotNull(jsonEvent);
            Assert.Equal(3, jsonEvent.SchemaVersion);
        }
    }

    public class ToEventOfT
    {
        [Fact]
        public void Should_convert_event_to_generic_event_with_expected_values()
        {
            // Arrange
            var @event = Substitute.For<IEvent>();
            @event.Payload.Returns("Payload");
            @event.EventType.Returns("TestEvent");
            @event.ExternalSequencer.Returns("1234567890");
            @event.EventVersion.Returns(1);
            @event.ActionMetadata.Returns(new ActionMetadata());
            @event.Metadata.Returns(new Dictionary<string, string>());
            var data = new { Id = 1, Name = "TestData" };

            // Act
            var result = JsonEvent.ToEvent(@event, data);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Payload", result.Payload);
            Assert.Equal("TestEvent", result.EventType);
            Assert.Equal("1234567890", result.ExternalSequencer);
            Assert.Equal(1, result.EventVersion);
            Assert.Equal(new Dictionary<string, string>(), result.Metadata);
            Assert.Equal(new ActionMetadata(), result.ActionMetadata);
            Assert.Equal(data, result.Data());
        }


        [Fact]
        public void Should_convert_event_to_generic_event_with_expected_values_when_payload_is_list_string()
        {
            // Arrange
            var @event = Substitute.For<IEvent>();
            @event.Payload.Returns("[\"1\",\"2\",\"3\"]");
            @event.EventType.Returns("TestEvent");
            @event.ExternalSequencer.Returns("1234567890");
            @event.EventVersion.Returns(1);
            @event.ActionMetadata.Returns(new ActionMetadata());
            @event.Metadata.Returns(new Dictionary<string, string>());

            // Act
            var result = JsonEvent.ToEvent(@event, new TestContext().ListString);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("[\"1\",\"2\",\"3\"]", result.Payload);
            Assert.Equal("TestEvent", result.EventType);
            Assert.Equal("1234567890", result.ExternalSequencer);
            Assert.Equal(1, result.EventVersion);
            Assert.Equal(new Dictionary<string, string>(), result.Metadata);
            Assert.Equal(new ActionMetadata(), result.ActionMetadata);
            Assert.Equal(["1", "2", "3"], result.Data());
        }
    }
}


[JsonSerializable(typeof(List<String>))]
public partial class TestContext : JsonSerializerContext
{
}
