using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Exceptions;
using NSubstitute;

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

    public class ToOfT()
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
