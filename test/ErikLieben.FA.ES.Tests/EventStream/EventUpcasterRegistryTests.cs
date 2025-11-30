using System;
using ErikLieben.FA.ES.EventStream;
using Xunit;

namespace ErikLieben.FA.ES.Tests.EventStream;

public class EventUpcasterRegistryTests
{
    public class EventUpcasterTests
    {
        [Fact]
        public void Should_create_record_with_all_properties()
        {
            // Arrange
            Func<object, object> upcastFunc = obj => obj;

            // Act
            var upcaster = new EventUpcaster(1, 2, upcastFunc);

            // Assert
            Assert.Equal(1, upcaster.FromVersion);
            Assert.Equal(2, upcaster.ToVersion);
            Assert.Same(upcastFunc, upcaster.Upcast);
        }
    }

    public class EventUpcasterKeyTests
    {
        [Fact]
        public void Should_create_key_with_name_and_version()
        {
            // Act
            var key = new EventUpcasterKey("test.event", 1);

            // Assert
            Assert.Equal("test.event", key.EventName);
            Assert.Equal(1, key.FromVersion);
        }

        [Fact]
        public void Should_be_equal_when_name_and_version_match()
        {
            // Arrange
            var key1 = new EventUpcasterKey("test.event", 1);
            var key2 = new EventUpcasterKey("test.event", 1);

            // Assert
            Assert.Equal(key1, key2);
            Assert.True(key1 == key2);
        }

        [Fact]
        public void Should_not_be_equal_when_version_differs()
        {
            // Arrange
            var key1 = new EventUpcasterKey("test.event", 1);
            var key2 = new EventUpcasterKey("test.event", 2);

            // Assert
            Assert.NotEqual(key1, key2);
        }
    }

    public class AddTests
    {
        [Fact]
        public void Should_add_upcaster()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();

            // Act
            registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2, v1 => new OrderCreatedV2(v1.OrderId, ""));

            // Assert
            Assert.True(registry.TryGetUpcaster("order.created", 1, out var upcaster));
            Assert.NotNull(upcaster);
            Assert.Equal(1, upcaster.FromVersion);
            Assert.Equal(2, upcaster.ToVersion);
        }

        [Fact]
        public void Should_throw_when_adding_to_frozen_registry()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Freeze();

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2, v1 => new OrderCreatedV2(v1.OrderId, "")));
            Assert.Contains("frozen registry", exception.Message);
        }

        [Fact]
        public void Should_add_multiple_upcasters_for_chain()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();

            // Act
            registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2, v1 => new OrderCreatedV2(v1.OrderId, ""));
            registry.Add<OrderCreatedV2, OrderCreatedV3>("order.created", 2, 3, v2 => new OrderCreatedV3(v2.OrderId, v2.Description, DateTime.UtcNow));

            // Assert
            Assert.True(registry.TryGetUpcaster("order.created", 1, out _));
            Assert.True(registry.TryGetUpcaster("order.created", 2, out _));
        }
    }

    public class TryGetUpcasterTests
    {
        [Fact]
        public void Should_return_true_when_found_before_freeze()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2, v1 => new OrderCreatedV2(v1.OrderId, "default"));

            // Act
            var result = registry.TryGetUpcaster("order.created", 1, out var upcaster);

            // Assert
            Assert.True(result);
            Assert.NotNull(upcaster);
        }

        [Fact]
        public void Should_return_true_when_found_after_freeze()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2, v1 => new OrderCreatedV2(v1.OrderId, "default"));
            registry.Freeze();

            // Act
            var result = registry.TryGetUpcaster("order.created", 1, out var upcaster);

            // Assert
            Assert.True(result);
            Assert.NotNull(upcaster);
        }

        [Fact]
        public void Should_return_false_when_not_found()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();

            // Act
            var result = registry.TryGetUpcaster("order.created", 1, out var upcaster);

            // Assert
            Assert.False(result);
            Assert.Null(upcaster);
        }

        [Fact]
        public void Should_execute_upcast_function()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2,
                v1 => new OrderCreatedV2(v1.OrderId, "Upcasted"));
            registry.TryGetUpcaster("order.created", 1, out var upcaster);

            var v1Event = new OrderCreatedV1("ORD-123");

            // Act
            var result = upcaster!.Upcast(v1Event);

            // Assert
            var v2Event = Assert.IsType<OrderCreatedV2>(result);
            Assert.Equal("ORD-123", v2Event.OrderId);
            Assert.Equal("Upcasted", v2Event.Description);
        }
    }

    public class FreezeTests
    {
        [Fact]
        public void Should_allow_multiple_freeze_calls()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2, v1 => new OrderCreatedV2(v1.OrderId, ""));

            // Act & Assert - should not throw
            registry.Freeze();
            registry.Freeze();
        }

        [Fact]
        public void Should_preserve_upcasters_after_freeze()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2, v1 => new OrderCreatedV2(v1.OrderId, ""));

            // Act
            registry.Freeze();

            // Assert
            Assert.True(registry.TryGetUpcaster("order.created", 1, out _));
        }
    }

    public class UpcastToVersionTests
    {
        [Fact]
        public void Should_upcast_single_version()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2,
                v1 => new OrderCreatedV2(v1.OrderId, "Upcasted"));
            var v1Event = new OrderCreatedV1("ORD-123");

            // Act
            var (data, version) = registry.UpcastToVersion("order.created", 1, 2, v1Event);

            // Assert
            Assert.Equal(2, version);
            var v2Event = Assert.IsType<OrderCreatedV2>(data);
            Assert.Equal("ORD-123", v2Event.OrderId);
            Assert.Equal("Upcasted", v2Event.Description);
        }

        [Fact]
        public void Should_upcast_through_chain_of_versions()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2,
                v1 => new OrderCreatedV2(v1.OrderId, "From V1"));
            registry.Add<OrderCreatedV2, OrderCreatedV3>("order.created", 2, 3,
                v2 => new OrderCreatedV3(v2.OrderId, v2.Description, new DateTime(2024, 1, 1)));
            var v1Event = new OrderCreatedV1("ORD-123");

            // Act
            var (data, version) = registry.UpcastToVersion("order.created", 1, 3, v1Event);

            // Assert
            Assert.Equal(3, version);
            var v3Event = Assert.IsType<OrderCreatedV3>(data);
            Assert.Equal("ORD-123", v3Event.OrderId);
            Assert.Equal("From V1", v3Event.Description);
            Assert.Equal(new DateTime(2024, 1, 1), v3Event.CreatedAt);
        }

        [Fact]
        public void Should_return_original_when_no_upcaster_found()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            var v1Event = new OrderCreatedV1("ORD-123");

            // Act
            var (data, version) = registry.UpcastToVersion("order.created", 1, 2, v1Event);

            // Assert
            Assert.Equal(1, version); // Version unchanged
            Assert.Same(v1Event, data); // Same object returned
        }

        [Fact]
        public void Should_stop_at_gap_in_version_chain()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2,
                v1 => new OrderCreatedV2(v1.OrderId, "From V1"));
            // Note: No upcaster from v2 to v3
            var v1Event = new OrderCreatedV1("ORD-123");

            // Act
            var (data, version) = registry.UpcastToVersion("order.created", 1, 3, v1Event);

            // Assert
            Assert.Equal(2, version); // Stopped at v2
            var v2Event = Assert.IsType<OrderCreatedV2>(data);
            Assert.Equal("ORD-123", v2Event.OrderId);
        }

        [Fact]
        public void Should_return_original_when_already_at_target_version()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Add<OrderCreatedV1, OrderCreatedV2>("order.created", 1, 2,
                v1 => new OrderCreatedV2(v1.OrderId, "From V1"));
            var v2Event = new OrderCreatedV2("ORD-123", "Already V2");

            // Act
            var (data, version) = registry.UpcastToVersion("order.created", 2, 2, v2Event);

            // Assert
            Assert.Equal(2, version);
            Assert.Same(v2Event, data);
        }
    }
}

// Test event types for upcaster tests
public record OrderCreatedV1(string OrderId);
public record OrderCreatedV2(string OrderId, string Description);
public record OrderCreatedV3(string OrderId, string Description, DateTime CreatedAt);
