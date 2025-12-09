using System;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.EventStream;
using Xunit;

namespace ErikLieben.FA.ES.Tests.EventStream;

public class EventTypeRegistryTests
{
    public class EventTypeInfoTests
    {
        [Fact]
        public void Should_create_record_with_all_properties()
        {
            // Arrange
            var type = typeof(TestEvent);
            var eventName = "test.event";
            var schemaVersion = 2;
            var jsonTypeInfo = TestEventContext.Default.TestEvent;

            // Act
            var info = new EventTypeInfo(type, eventName, schemaVersion, jsonTypeInfo);

            // Assert
            Assert.Equal(type, info.Type);
            Assert.Equal(eventName, info.EventName);
            Assert.Equal(schemaVersion, info.SchemaVersion);
            Assert.Equal(jsonTypeInfo, info.JsonTypeInfo);
        }
    }

    public class EventTypeKeyTests
    {
        [Fact]
        public void Should_create_key_with_name_and_version()
        {
            // Act
            var key = new EventTypeKey("test.event", 2);

            // Assert
            Assert.Equal("test.event", key.EventName);
            Assert.Equal(2, key.SchemaVersion);
        }

        [Fact]
        public void Should_be_equal_when_name_and_version_match()
        {
            // Arrange
            var key1 = new EventTypeKey("test.event", 2);
            var key2 = new EventTypeKey("test.event", 2);

            // Assert
            Assert.Equal(key1, key2);
            Assert.True(key1 == key2);
        }

        [Fact]
        public void Should_not_be_equal_when_version_differs()
        {
            // Arrange
            var key1 = new EventTypeKey("test.event", 1);
            var key2 = new EventTypeKey("test.event", 2);

            // Assert
            Assert.NotEqual(key1, key2);
        }

        [Fact]
        public void Should_not_be_equal_when_name_differs()
        {
            // Arrange
            var key1 = new EventTypeKey("test.event.a", 1);
            var key2 = new EventTypeKey("test.event.b", 1);

            // Assert
            Assert.NotEqual(key1, key2);
        }
    }

    public class AddTests
    {
        [Fact]
        public void Should_add_event_with_default_schema_version_1()
        {
            // Arrange
            var registry = new EventTypeRegistry();

            // Act
            registry.Add(typeof(TestEvent), "test.event", TestEventContext.Default.TestEvent);

            // Assert
            var info = registry.GetByType(typeof(TestEvent));
            Assert.NotNull(info);
            Assert.Equal(1, info.SchemaVersion);
        }

        [Fact]
        public void Should_add_event_with_explicit_schema_version()
        {
            // Arrange
            var registry = new EventTypeRegistry();

            // Act
            registry.Add(typeof(TestEvent), "test.event", 3, TestEventContext.Default.TestEvent);

            // Assert
            var info = registry.GetByType(typeof(TestEvent));
            Assert.NotNull(info);
            Assert.Equal(3, info.SchemaVersion);
        }

        [Fact]
        public void Should_throw_when_adding_to_frozen_registry()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Freeze();

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                registry.Add(typeof(TestEvent), "test.event", TestEventContext.Default.TestEvent));
            Assert.Contains("frozen registry", exception.Message);
        }

        [Fact]
        public void Should_add_multiple_versions_of_same_event_name()
        {
            // Arrange
            var registry = new EventTypeRegistry();

            // Act
            registry.Add(typeof(TestEventV1), "test.event", 1, TestEventContext.Default.TestEventV1);
            registry.Add(typeof(TestEventV2), "test.event", 2, TestEventContext.Default.TestEventV2);

            // Assert
            Assert.True(registry.TryGetByNameAndVersion("test.event", 1, out var v1Info));
            Assert.True(registry.TryGetByNameAndVersion("test.event", 2, out var v2Info));
            Assert.Equal(typeof(TestEventV1), v1Info?.Type);
            Assert.Equal(typeof(TestEventV2), v2Info?.Type);
        }
    }

    public class GetByNameAndVersionTests
    {
        [Fact]
        public void Should_return_info_when_found_before_freeze()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEvent), "test.event", 2, TestEventContext.Default.TestEvent);

            // Act
            var info = registry.GetByNameAndVersion("test.event", 2);

            // Assert
            Assert.NotNull(info);
            Assert.Equal(typeof(TestEvent), info.Type);
            Assert.Equal(2, info.SchemaVersion);
        }

        [Fact]
        public void Should_return_info_when_found_after_freeze()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEvent), "test.event", 2, TestEventContext.Default.TestEvent);
            registry.Freeze();

            // Act
            var info = registry.GetByNameAndVersion("test.event", 2);

            // Assert
            Assert.NotNull(info);
            Assert.Equal(typeof(TestEvent), info.Type);
            Assert.Equal(2, info.SchemaVersion);
        }

        [Fact]
        public void Should_throw_when_not_found_before_freeze()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEvent), "test.event", 1, TestEventContext.Default.TestEvent);

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => registry.GetByNameAndVersion("test.event", 2));
        }

        [Fact]
        public void Should_throw_when_not_found_after_freeze()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEvent), "test.event", 1, TestEventContext.Default.TestEvent);
            registry.Freeze();

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => registry.GetByNameAndVersion("test.event", 2));
        }
    }

    public class TryGetByNameAndVersionTests
    {
        [Fact]
        public void Should_return_true_and_info_when_found_before_freeze()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEvent), "test.event", 2, TestEventContext.Default.TestEvent);

            // Act
            var result = registry.TryGetByNameAndVersion("test.event", 2, out var info);

            // Assert
            Assert.True(result);
            Assert.NotNull(info);
            Assert.Equal(typeof(TestEvent), info.Type);
        }

        [Fact]
        public void Should_return_true_and_info_when_found_after_freeze()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEvent), "test.event", 2, TestEventContext.Default.TestEvent);
            registry.Freeze();

            // Act
            var result = registry.TryGetByNameAndVersion("test.event", 2, out var info);

            // Assert
            Assert.True(result);
            Assert.NotNull(info);
            Assert.Equal(typeof(TestEvent), info.Type);
        }

        [Fact]
        public void Should_return_false_when_not_found_before_freeze()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEvent), "test.event", 1, TestEventContext.Default.TestEvent);

            // Act
            var result = registry.TryGetByNameAndVersion("test.event", 2, out var info);

            // Assert
            Assert.False(result);
            Assert.Null(info);
        }

        [Fact]
        public void Should_return_false_when_not_found_after_freeze()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEvent), "test.event", 1, TestEventContext.Default.TestEvent);
            registry.Freeze();

            // Act
            var result = registry.TryGetByNameAndVersion("test.event", 2, out var info);

            // Assert
            Assert.False(result);
            Assert.Null(info);
        }

        [Fact]
        public void Should_distinguish_between_different_versions()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEventV1), "test.event", 1, TestEventContext.Default.TestEventV1);
            registry.Add(typeof(TestEventV2), "test.event", 2, TestEventContext.Default.TestEventV2);

            // Act
            registry.TryGetByNameAndVersion("test.event", 1, out var v1Info);
            registry.TryGetByNameAndVersion("test.event", 2, out var v2Info);

            // Assert
            Assert.Equal(typeof(TestEventV1), v1Info?.Type);
            Assert.Equal(typeof(TestEventV2), v2Info?.Type);
        }
    }

    public class FreezeTests
    {
        [Fact]
        public void Should_allow_multiple_freeze_calls()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEvent), "test.event", TestEventContext.Default.TestEvent);

            // Act & Assert - should not throw
            registry.Freeze();
            registry.Freeze();

            Assert.True(true);
        }

        [Fact]
        public void Should_preserve_version_lookups_after_freeze()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEventV1), "test.event", 1, TestEventContext.Default.TestEventV1);
            registry.Add(typeof(TestEventV2), "test.event", 2, TestEventContext.Default.TestEventV2);

            // Act
            registry.Freeze();

            // Assert
            Assert.True(registry.TryGetByNameAndVersion("test.event", 1, out _));
            Assert.True(registry.TryGetByNameAndVersion("test.event", 2, out _));
        }
    }

    public class AllTests
    {
        [Fact]
        public void Should_return_all_registered_events_including_multiple_versions()
        {
            // Arrange
            var registry = new EventTypeRegistry();
            registry.Add(typeof(TestEventV1), "test.event", 1, TestEventContext.Default.TestEventV1);
            registry.Add(typeof(TestEventV2), "test.event", 2, TestEventContext.Default.TestEventV2);
            registry.Add(typeof(TestEvent), "other.event", 1, TestEventContext.Default.TestEvent);

            // Act
            var all = registry.All;

            // Assert
            Assert.Equal(3, all.Count());
        }
    }
}

public record TestEvent(string Name);
public record TestEventV1(string Name);
public record TestEventV2(string Name, string Description);

[JsonSerializable(typeof(TestEvent))]
[JsonSerializable(typeof(TestEventV1))]
[JsonSerializable(typeof(TestEventV2))]
internal partial class TestEventContext : JsonSerializerContext
{
}
