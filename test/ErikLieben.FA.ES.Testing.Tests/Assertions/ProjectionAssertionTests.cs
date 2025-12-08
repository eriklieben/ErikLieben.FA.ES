using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.Testing.Assertions;
using ErikLieben.FA.ES.Testing.Builders;
using ErikLieben.FA.ES.VersionTokenParts;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Assertions;

/// <summary>
/// Tests for ProjectionAssertion class.
/// </summary>
public partial class ProjectionAssertionTests
{
    [EventName("OrderCreated")]
    private record OrderCreated(string OrderId, decimal Amount);

    [EventName("OrderShipped")]
    private record OrderShipped(string TrackingNumber);

    [JsonSerializable(typeof(OrderCreated))]
    [JsonSerializable(typeof(OrderShipped))]
    private partial class ProjAssertOrderEventsJsonContext : JsonSerializerContext { }

    private class Order : Aggregate, ITestableAggregate<Order>
    {
        public static string ObjectName => "order";
        public static Order Create(IEventStream stream) => new Order(stream);

        public Order(IEventStream stream) : base(stream)
        {
            stream.EventTypeRegistry.Add(
                typeof(OrderCreated),
                "OrderCreated",
                ProjAssertOrderEventsJsonContext.Default.OrderCreated);
            stream.EventTypeRegistry.Add(
                typeof(OrderShipped),
                "OrderShipped",
                ProjAssertOrderEventsJsonContext.Default.OrderShipped);
        }

        public override void Fold(IEvent @event) { }
    }

    private class OrderSummary : Projection, ITestableProjection<OrderSummary>
    {
        public static OrderSummary Create(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
            => new OrderSummary(documentFactory, eventStreamFactory);

        public OrderSummary(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
            : base(documentFactory, eventStreamFactory)
        {
            Checkpoint = new Checkpoint();
        }

        public int TotalOrders { get; private set; }
        public decimal TotalAmount { get; private set; }
        public int ShippedOrders { get; private set; }

        public override async Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? parentContext = null)
            where T : class
        {
            if (@event is JsonEvent jsonEvent)
            {
                switch (jsonEvent.EventType)
                {
                    case "OrderCreated":
                        var created = JsonSerializer.Deserialize(
                            jsonEvent.Payload!,
                            ProjAssertOrderEventsJsonContext.Default.OrderCreated);
                        if (created != null)
                        {
                            TotalOrders++;
                            TotalAmount += created.Amount;
                        }
                        break;
                    case "OrderShipped":
                        ShippedOrders++;
                        break;
                }
            }
            await Task.CompletedTask;
        }

        public override string ToJson() => JsonSerializer.Serialize(this);
        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;
        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories => new();
        public override Checkpoint Checkpoint { get; set; }
    }

    private static TestContext CreateTestContext()
    {
        var provider = new SimpleServiceProvider();
        return TestSetup.GetContext(provider, _ => typeof(DummyFactory));
    }

    private class SimpleServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private class DummyFactory : IAggregateCovarianceFactory<IBase>
    {
        public string GetObjectName() => "dummy";
        public IBase Create(IEventStream eventStream) => new Order(eventStream);
        public IBase Create(IObjectDocument document) => throw new NotImplementedException();
    }

    public class ShouldHaveStateMethod : ProjectionAssertionTests
    {
        [Fact]
        public async Task Should_pass_when_assertion_succeeds()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 100m))
                .UpdateToLatest();

            // Act & Assert - should not throw
            builder.Then()
                .ShouldHaveState(p => Assert.Equal(1, p.TotalOrders));
        }

        [Fact]
        public async Task Should_throw_when_assertion_fails()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 100m))
                .UpdateToLatest();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                builder.Then().ShouldHaveState(p => Assert.Equal(5, p.TotalOrders)));
            Assert.Contains("Projection state assertion failed", ex.Message);
        }

        [Fact]
        public void Should_throw_when_assertion_is_null()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = ProjectionTestBuilder.For<OrderSummary>(context);
            var assertion = builder.Then();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                assertion.ShouldHaveState((Action<OrderSummary>)null!));
        }
    }

    public class ShouldHavePropertyMethod : ProjectionAssertionTests
    {
        [Fact]
        public async Task Should_pass_when_property_matches()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 99.99m))
                .UpdateToLatest();

            // Act & Assert - should not throw
            builder.Then()
                .ShouldHaveProperty(p => p.TotalAmount, 99.99m);
        }

        [Fact]
        public async Task Should_throw_when_property_does_not_match()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 50m))
                .UpdateToLatest();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                builder.Then().ShouldHaveProperty(p => p.TotalAmount, 100m));
            Assert.Contains("Expected projection property to be '100'", ex.Message);
            Assert.Contains("found '50'", ex.Message);
        }

        [Fact]
        public void Should_throw_when_selector_is_null()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = ProjectionTestBuilder.For<OrderSummary>(context);
            var assertion = builder.Then();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                assertion.ShouldHaveProperty((Func<OrderSummary, int>)null!, 1));
        }
    }

    public class ShouldHaveCheckpointMethod : ProjectionAssertionTests
    {
        [Fact]
        public async Task Should_pass_when_checkpoint_matches()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 100m))
                .UpdateToLatest();

            // Act & Assert - should not throw
            builder.Then()
                .ShouldHaveCheckpoint("order", "order-1", 0);
        }

        [Fact]
        public async Task Should_throw_when_checkpoint_not_found()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 100m))
                .UpdateToLatest();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                builder.Then().ShouldHaveCheckpoint("order", "nonexistent", 0));
            Assert.Contains("does not contain entry", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_checkpoint_version_does_not_match()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 100m))
                .UpdateToLatest();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                builder.Then().ShouldHaveCheckpoint("order", "order-1", 999));
            Assert.Contains("Expected checkpoint version 999", ex.Message);
        }

        [Fact]
        public void Should_throw_when_object_name_is_null()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = ProjectionTestBuilder.For<OrderSummary>(context);
            var assertion = builder.Then();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                assertion.ShouldHaveCheckpoint(null!, "id", 0));
        }

        [Fact]
        public void Should_throw_when_object_id_is_null()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = ProjectionTestBuilder.For<OrderSummary>(context);
            var assertion = builder.Then();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                assertion.ShouldHaveCheckpoint("order", null!, 0));
        }
    }

    public class ShouldHaveCheckpointsMethod : ProjectionAssertionTests
    {
        [Fact]
        public async Task Should_pass_when_all_checkpoints_match()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 100m))
                .Given<Order>("order-2", new OrderCreated("order-2", 200m))
                .UpdateToLatest();

            // Act & Assert - should not throw
            builder.Then()
                .ShouldHaveCheckpoints(new Dictionary<(string, string), int>
                {
                    { ("order", "order-1"), 0 },
                    { ("order", "order-2"), 0 }
                });
        }

        [Fact]
        public void Should_throw_when_checkpoints_is_null()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = ProjectionTestBuilder.For<OrderSummary>(context);
            var assertion = builder.Then();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                assertion.ShouldHaveCheckpoints(null!));
        }
    }

    public class ShouldHaveCheckpointFingerprintMethod : ProjectionAssertionTests
    {
        [Fact]
        public void Should_throw_when_fingerprint_does_not_match()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = ProjectionTestBuilder.For<OrderSummary>(context);
            var assertion = builder.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveCheckpointFingerprint("expected-fingerprint"));
            Assert.Contains("Expected checkpoint fingerprint", ex.Message);
        }

        [Fact]
        public void Should_throw_when_fingerprint_is_null()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = ProjectionTestBuilder.For<OrderSummary>(context);
            var assertion = builder.Then();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                assertion.ShouldHaveCheckpointFingerprint(null!));
        }
    }

    public class ShouldHaveNonEmptyCheckpointMethod : ProjectionAssertionTests
    {
        [Fact]
        public async Task Should_pass_when_checkpoint_is_not_empty()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 100m))
                .UpdateToLatest();

            // Act & Assert - should not throw
            builder.Then()
                .ShouldHaveNonEmptyCheckpoint();
        }

        [Fact]
        public void Should_throw_when_checkpoint_is_empty()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = ProjectionTestBuilder.For<OrderSummary>(context);
            var assertion = builder.Then();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveNonEmptyCheckpoint());
            Assert.Contains("non-empty checkpoint", ex.Message);
        }
    }

    public class ShouldHaveCheckpointCountMethod : ProjectionAssertionTests
    {
        [Fact]
        public async Task Should_pass_when_count_matches()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 100m))
                .Given<Order>("order-2", new OrderCreated("order-2", 200m))
                .UpdateToLatest();

            // Act & Assert - should not throw
            builder.Then()
                .ShouldHaveCheckpointCount(2);
        }

        [Fact]
        public async Task Should_throw_when_count_does_not_match()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 100m))
                .UpdateToLatest();

            // Act & Assert
            var ex = Assert.Throws<TestAssertionException>(() =>
                builder.Then().ShouldHaveCheckpointCount(5));
            Assert.Contains("Expected 5 checkpoint entries", ex.Message);
            Assert.Contains("found 1", ex.Message);
        }
    }

    public class MethodChaining : ProjectionAssertionTests
    {
        [Fact]
        public async Task Should_allow_chaining_multiple_assertions()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1",
                    new OrderCreated("order-1", 100m),
                    new OrderShipped("TRACK-001"))
                .UpdateToLatest();

            // Act & Assert - All methods should return the assertion for chaining
            builder.Then()
                .ShouldHaveProperty(p => p.TotalOrders, 1)
                .ShouldHaveProperty(p => p.TotalAmount, 100m)
                .ShouldHaveProperty(p => p.ShippedOrders, 1)
                .ShouldHaveCheckpoint("order", "order-1", 1)
                .ShouldHaveNonEmptyCheckpoint()
                .ShouldHaveCheckpointCount(1)
                .ShouldHaveState(p => Assert.True(p.ShippedOrders <= p.TotalOrders));
        }
    }

    public class ProjectionProperty : ProjectionAssertionTests
    {
        [Fact]
        public async Task Should_expose_projection_instance()
        {
            // Arrange
            var context = CreateTestContext();
            var builder = await ProjectionTestBuilder.For<OrderSummary>(context)
                .Given<Order>("order-1", new OrderCreated("order-1", 100m))
                .UpdateToLatest();

            // Act
            var assertion = builder.Then();

            // Assert
            Assert.NotNull(assertion.Projection);
            Assert.Equal(1, assertion.Projection.TotalOrders);
        }
    }
}
