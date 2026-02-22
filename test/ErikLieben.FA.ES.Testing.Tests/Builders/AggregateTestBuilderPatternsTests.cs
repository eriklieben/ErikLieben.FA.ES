using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.Builders;
using ErikLieben.FA.ES.Testing.InMemory;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Builders;

/// <summary>
/// Tests demonstrating both patterns for using AggregateTestBuilder:
/// 1. ITestableAggregate pattern - Simpler syntax when aggregate implements the interface
/// 2. Explicit factory pattern - More flexible, works with any aggregate
/// </summary>
public partial class AggregateTestBuilderPatternsTests
{
    #region Events

    [EventName("OrderCreated")]
    private record OrderCreated(string CustomerId, decimal Total);

    [EventName("OrderShipped")]
    private record OrderShipped(string TrackingNumber, DateTimeOffset ShippedAt);

    [EventName("OrderCancelled")]
    private record OrderCancelled(string Reason);

    [JsonSerializable(typeof(OrderCreated))]
    [JsonSerializable(typeof(OrderShipped))]
    [JsonSerializable(typeof(OrderCancelled))]
    private partial class OrderEventsJsonContext : JsonSerializerContext { }

    #endregion

    #region Pattern 1: ITestableAggregate - Aggregate implementing the interface

    /// <summary>
    /// This aggregate implements ITestableAggregate{TSelf} which provides:
    /// - static string ObjectName => The logical name used for event streams
    /// - static TSelf Create(IEventStream stream) => Factory method for creating instances
    ///
    /// This enables the simpler test syntax:
    ///   AggregateTestBuilder.For{Order}(orderId, context)
    /// </summary>
    private class Order : Aggregate, ITestableAggregate<Order>
    {
        // ITestableAggregate implementation
        public static string ObjectName => "order";
        public static Order Create(IEventStream stream) => new Order(stream);

        public Order(IEventStream stream) : base(stream)
        {
            // Register event types for this aggregate
            stream.EventTypeRegistry.Add(
                typeof(OrderCreated),
                "OrderCreated",
                OrderEventsJsonContext.Default.OrderCreated);
            stream.EventTypeRegistry.Add(
                typeof(OrderShipped),
                "OrderShipped",
                OrderEventsJsonContext.Default.OrderShipped);
            stream.EventTypeRegistry.Add(
                typeof(OrderCancelled),
                "OrderCancelled",
                OrderEventsJsonContext.Default.OrderCancelled);
        }

        public string? CustomerId { get; private set; }
        public decimal Total { get; private set; }
        public bool IsShipped { get; private set; }
        public bool IsCancelled { get; private set; }
        public string? TrackingNumber { get; private set; }

        public async Task CreateOrder(string customerId, decimal total)
        {
            await Stream.Session(context =>
                Fold(context.Append(new OrderCreated(customerId, total))));
        }

        public async Task Ship(string trackingNumber)
        {
            if (IsCancelled)
                throw new InvalidOperationException("Cannot ship a cancelled order");

            await Stream.Session(context =>
                Fold(context.Append(new OrderShipped(trackingNumber, DateTimeOffset.UtcNow))));
        }

        public async Task Cancel(string reason)
        {
            if (IsShipped)
                throw new InvalidOperationException("Cannot cancel a shipped order");

            await Stream.Session(context =>
                Fold(context.Append(new OrderCancelled(reason))));
        }

        private void When(OrderCreated @event)
        {
            CustomerId = @event.CustomerId;
            Total = @event.Total;
        }

        private void When(OrderShipped @event)
        {
            IsShipped = true;
            TrackingNumber = @event.TrackingNumber;
        }

        private void When(OrderCancelled @event)
        {
            IsCancelled = true;
        }

        public override void Fold(IEvent @event)
        {
            if (@event is JsonEvent jsonEvent)
            {
                switch (jsonEvent.EventType)
                {
                    case "OrderCreated":
                        var created = JsonSerializer.Deserialize(
                            jsonEvent.Payload!,
                            OrderEventsJsonContext.Default.OrderCreated);
                        if (created != null) When(created);
                        break;
                    case "OrderShipped":
                        var shipped = JsonSerializer.Deserialize(
                            jsonEvent.Payload!,
                            OrderEventsJsonContext.Default.OrderShipped);
                        if (shipped != null) When(shipped);
                        break;
                    case "OrderCancelled":
                        var cancelled = JsonSerializer.Deserialize(
                            jsonEvent.Payload!,
                            OrderEventsJsonContext.Default.OrderCancelled);
                        if (cancelled != null) When(cancelled);
                        break;
                }
            }
        }
    }

    #endregion

    #region Pattern 2: Explicit Factory - Legacy aggregate without interface

    /// <summary>
    /// This aggregate does NOT implement ITestableAggregate.
    /// It requires the explicit factory pattern:
    ///   AggregateTestBuilder{Invoice}.For("invoice", invoiceId, context, s => new Invoice(s))
    /// </summary>
    private class Invoice : Aggregate
    {
        public Invoice(IEventStream stream) : base(stream)
        {
            // Register event types
            stream.EventTypeRegistry.Add(
                typeof(OrderCreated),
                "OrderCreated",
                OrderEventsJsonContext.Default.OrderCreated);
        }

        public string? CustomerId { get; private set; }
        public decimal Amount { get; private set; }

        public async Task CreateInvoice(string customerId, decimal amount)
        {
            await Stream.Session(context =>
                Fold(context.Append(new OrderCreated(customerId, amount))));
        }

        private void When(OrderCreated @event)
        {
            CustomerId = @event.CustomerId;
            Amount = @event.Total;
        }

        public override void Fold(IEvent @event)
        {
            if (@event is JsonEvent jsonEvent && jsonEvent.EventType == "OrderCreated")
            {
                var created = JsonSerializer.Deserialize(
                    jsonEvent.Payload!,
                    OrderEventsJsonContext.Default.OrderCreated);
                if (created != null) When(created);
            }
        }
    }

    #endregion

    #region Test Helpers

    private static TestContext CreateTestContext()
    {
        var provider = new SimpleServiceProvider();
        return TestSetup.GetContext(provider, _ => typeof(DummyFactory));
    }

    private class SimpleServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private class DummyFactory : ErikLieben.FA.ES.Aggregates.IAggregateCovarianceFactory<IBase>
    {
        public string GetObjectName() => "dummy";
        public IBase Create(IEventStream eventStream) => new Order(eventStream);
        public IBase Create(ErikLieben.FA.ES.Documents.IObjectDocument document) => throw new NotImplementedException();
    }

    #endregion

    #region Pattern 1 Tests: ITestableAggregate Pattern

    /// <summary>
    /// Demonstrates the simplest syntax using ITestableAggregate.
    /// The ObjectName and factory are provided by the aggregate's static members.
    /// </summary>
    [Fact]
    public async Task Pattern1_ITestableAggregate_SimplestSyntax()
    {
        // Arrange
        var context = CreateTestContext();

        // Act - Note: Only objectId and context needed!
        var assertion = await AggregateTestBuilder.For<Order>("order-123", context)
            .Given(new OrderCreated("customer-1", 99.99m))
            .Then();

        // Assert
        assertion.ShouldHaveProperty(o => o.CustomerId, "customer-1");
        assertion.ShouldHaveProperty(o => o.Total, 99.99m);
    }

    [Fact]
    public async Task Pattern1_ITestableAggregate_GivenWhenThen()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        await AggregateTestBuilder.For<Order>("order-456", context)
            .Given(new OrderCreated("customer-2", 150.00m))
            .When(async order => await order.Ship("TRACK-001"))
            .Then(assertion =>
            {
                assertion.ShouldHaveAppended<OrderShipped>();
                assertion.ShouldHaveProperty(o => o.IsShipped, true);
                assertion.ShouldHaveProperty(o => o.TrackingNumber, "TRACK-001");
            });
    }

    [Fact]
    public async Task Pattern1_ITestableAggregate_TestingExceptions()
    {
        // Arrange
        var context = CreateTestContext();

        // Act - Try to cancel a shipped order
        var builderAfterWhen = await AggregateTestBuilder.For<Order>("order-789", context)
            .Given(
                new OrderCreated("customer-3", 200.00m),
                new OrderShipped("TRACK-002", DateTimeOffset.UtcNow))
            .When(async order => await order.Cancel("Changed my mind"));

        var assertion = await builderAfterWhen.Then();

        // Assert - Should have thrown an exception
        assertion.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task Pattern1_ITestableAggregate_GivenNoPriorEvents()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        await AggregateTestBuilder.For<Order>("new-order", context)
            .GivenNoPriorEvents()
            .When(async order => await order.CreateOrder("new-customer", 50.00m))
            .Then(assertion =>
            {
                assertion.ShouldHaveAppended(new OrderCreated("new-customer", 50.00m));
                assertion.ShouldHaveAppendedCount(1);
            });
    }

    #endregion

    #region Pattern 2 Tests: Explicit Factory Pattern

    /// <summary>
    /// Demonstrates the explicit factory pattern for aggregates that don't implement ITestableAggregate.
    /// You must provide: objectName, objectId, context, and a factory function.
    /// </summary>
    [Fact]
    public async Task Pattern2_ExplicitFactory_FullSyntax()
    {
        // Arrange
        var context = CreateTestContext();

        // Act - Note: All parameters must be explicitly provided
        var assertion = await AggregateTestBuilder<Invoice>.For(
            "invoice",           // objectName
            "inv-001",           // objectId
            context,             // test context
            s => new Invoice(s)) // factory function
            .Given(new OrderCreated("customer-x", 500.00m))
            .Then();

        // Assert
        assertion.ShouldHaveProperty(i => i.CustomerId, "customer-x");
        assertion.ShouldHaveProperty(i => i.Amount, 500.00m);
    }

    [Fact]
    public async Task Pattern2_ExplicitFactory_GivenWhenThen()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        await AggregateTestBuilder<Invoice>.For(
            "invoice",
            "inv-002",
            context,
            s => new Invoice(s))
            .GivenNoPriorEvents()
            .When(async invoice => await invoice.CreateInvoice("customer-y", 750.00m))
            .Then(assertion =>
            {
                assertion.ShouldHaveAppended<OrderCreated>();
                assertion.ShouldHaveProperty(i => i.Amount, 750.00m);
            });
    }

    /// <summary>
    /// The explicit factory pattern also works with ITestableAggregate aggregates
    /// when you need custom initialization or want to override the ObjectName.
    /// </summary>
    [Fact]
    public async Task Pattern2_ExplicitFactory_WithITestableAggregate_CustomObjectName()
    {
        // Arrange
        var context = CreateTestContext();

        // Act - Using explicit factory even though Order implements ITestableAggregate
        // This allows using a different objectName for testing
        var assertion = await AggregateTestBuilder<Order>.For(
            "custom-order-type",  // Custom objectName (overrides Order.ObjectName)
            "order-custom-1",
            context,
            s => new Order(s))
            .Given(new OrderCreated("customer-special", 1000.00m))
            .Then();

        // Assert
        assertion.ShouldHaveProperty(o => o.CustomerId, "customer-special");
    }

    #endregion

    #region Comparison Tests

    /// <summary>
    /// Shows both patterns side by side for the same aggregate.
    /// When the aggregate implements ITestableAggregate, prefer Pattern 1 for cleaner syntax.
    /// </summary>
    [Fact]
    public async Task Comparison_BothPatternsProduceSameResult()
    {
        var context = CreateTestContext();
        var orderId = "comparison-order";

        // Pattern 1: ITestableAggregate (simpler)
        var order1 = await AggregateTestBuilder.For<Order>(orderId + "-1", context)
            .Given(new OrderCreated("same-customer", 100.00m))
            .ThenAggregate();

        // Pattern 2: Explicit Factory (more verbose)
        var order2 = await AggregateTestBuilder<Order>.For(
            "order",
            orderId + "-2",
            context,
            s => new Order(s))
            .Given(new OrderCreated("same-customer", 100.00m))
            .ThenAggregate();

        // Both should produce equivalent results
        Assert.Equal(order1.CustomerId, order2.CustomerId);
        Assert.Equal(order1.Total, order2.Total);
    }

    #endregion
}
