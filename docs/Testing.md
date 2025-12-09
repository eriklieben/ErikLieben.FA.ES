# Testing Event-Sourced Aggregates

This document describes how to test aggregates using the `AggregateTestBuilder` fluent API. The testing library provides a Given-When-Then pattern for testing aggregate behavior.

## Table of Contents

- [Getting Started](#getting-started)
- [Two Patterns for Testing](#two-patterns-for-testing)
  - [Pattern 1: ITestableAggregate (Recommended)](#pattern-1-itestableaggregate-recommended)
  - [Pattern 2: Explicit Factory](#pattern-2-explicit-factory)
- [Given-When-Then Examples](#given-when-then-examples)
- [Assertion Methods](#assertion-methods)
- [Testing with Time](#testing-with-time)
- [Best Practices](#best-practices)

## Getting Started

First, create a test context using `TestSetup.GetContext()`:

```csharp
using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.Builders;

public class OrderTests
{
    private static TestContext CreateTestContext()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return TestSetup.GetContext(serviceProvider);
    }
}
```

## Two Patterns for Testing

The `AggregateTestBuilder` supports two patterns for creating test builders, each suited for different scenarios.

### Pattern 1: ITestableAggregate (Recommended)

**When to use:** When your aggregate implements `ITestableAggregate<TSelf>`

This is the recommended approach for new aggregates. It provides the cleanest test syntax and is fully AOT-compatible.

#### Step 1: Implement ITestableAggregate

```csharp
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Processors;

public class Order : Aggregate, ITestableAggregate<Order>
{
    // Required by ITestableAggregate - the logical name for event streams
    public static string ObjectName => "order";

    // Required by ITestableAggregate - factory method for creating instances
    public static Order Create(IEventStream stream) => new Order(stream);

    public Order(IEventStream stream) : base(stream)
    {
        // Register event types in constructor
        stream.EventTypeRegistry.Add(typeof(OrderCreated), "OrderCreated", ...);
    }

    // ... aggregate implementation
}
```

#### Step 2: Write Tests with Simple Syntax

```csharp
[Fact]
public async Task Order_ShouldBeShipped_WhenShipCommandIsExecuted()
{
    var context = CreateTestContext();

    // Note: Only objectId and context needed!
    await AggregateTestBuilder.For<Order>("order-123", context)
        .Given(new OrderCreated("customer-1", 99.99m))
        .When(async order => await order.Ship("TRACK-001"))
        .Then(assertion =>
        {
            assertion.ShouldHaveAppended<OrderShipped>();
            assertion.ShouldHaveProperty(o => o.IsShipped, true);
        });
}
```

#### With Strongly-Typed Identifiers

For aggregates with strongly-typed IDs, implement `ITestableAggregate<TSelf, TId>`:

```csharp
public class Order : Aggregate, ITestableAggregate<Order, OrderId>
{
    public static string ObjectName => "order";
    public static Order Create(IEventStream stream) => new Order(stream);

    // ...
}

// Usage in tests:
await AggregateTestBuilder.For<Order, OrderId>(new OrderId("order-123"), context)
    .Given(...)
    .When(...)
    .Then(...);
```

### Pattern 2: Explicit Factory

**When to use:**
- Your aggregate doesn't implement `ITestableAggregate`
- You need custom initialization logic
- You want to use a different object name for testing
- Working with legacy aggregates

#### Syntax

```csharp
[Fact]
public async Task Invoice_ShouldBeCreated()
{
    var context = CreateTestContext();

    // All parameters must be explicitly provided
    await AggregateTestBuilder<Invoice>.For(
        "invoice",                    // objectName
        "inv-001",                    // objectId
        context,                      // test context
        stream => new Invoice(stream) // factory function
    )
    .Given(new InvoiceCreated("customer-x", 500.00m))
    .Then(assertion =>
    {
        assertion.ShouldHaveProperty(i => i.CustomerId, "customer-x");
    });
}
```

#### Custom Factory with Dependencies

```csharp
await AggregateTestBuilder<Order>.For(
    "order",
    "order-123",
    context,
    stream => new Order(stream, new MockDiscountService())  // Inject dependencies
)
.When(async order => await order.ApplyDiscount())
.Then(assertion => assertion.ShouldHaveAppended<DiscountApplied>());
```

## Given-When-Then Examples

### Setting Up Initial State with Events

```csharp
// Single event
await AggregateTestBuilder.For<Order>("order-1", context)
    .Given(new OrderCreated("customer-1", 100m))
    .Then(...);

// Multiple events
await AggregateTestBuilder.For<Order>("order-2", context)
    .Given(
        new OrderCreated("customer-1", 100m),
        new OrderItemAdded("SKU-001", 2),
        new OrderItemAdded("SKU-002", 1)
    )
    .Then(...);

// Explicitly declare no prior events
await AggregateTestBuilder.For<Order>("new-order", context)
    .GivenNoPriorEvents()
    .When(async order => await order.Create(...))
    .Then(...);
```

### Executing Commands

```csharp
// Async command
await AggregateTestBuilder.For<Order>("order-1", context)
    .Given(new OrderCreated(...))
    .When(async order => await order.Ship("tracking-123"))
    .Then(...);

// Sync command
await AggregateTestBuilder.For<Order>("order-1", context)
    .Given(new OrderCreated(...))
    .When(order => order.MarkAsPriority())
    .Then(...);
```

### Testing Exceptions

```csharp
[Fact]
public async Task Order_ShouldThrow_WhenCancellingShippedOrder()
{
    var context = CreateTestContext();

    var builderAfterWhen = await AggregateTestBuilder.For<Order>("order-1", context)
        .Given(
            new OrderCreated("customer", 100m),
            new OrderShipped("TRACK-001", DateTimeOffset.UtcNow)
        )
        .When(async order => await order.Cancel("Changed my mind"));

    var assertion = await builderAfterWhen.Then();
    assertion.ShouldThrow<InvalidOperationException>();
}
```

## Assertion Methods

### Event Assertions

```csharp
// Check that a specific event type was appended
assertion.ShouldHaveAppended<OrderShipped>();

// Check event with specific payload
assertion.ShouldHaveAppended(new OrderCreated("customer-1", 99.99m));

// Check event count
assertion.ShouldHaveAppendedCount(1);
assertion.ShouldHaveAppendedAtLeast(1);

// Check no events appended
assertion.ShouldNotHaveAppendedAnyEvents();

// Check specific event type was NOT appended
assertion.ShouldNotHaveAppended<OrderCancelled>();

// Check event with predicate
assertion.ShouldContainEvent<OrderShipped>(e => e.TrackingNumber.StartsWith("TRACK"));
```

### State Assertions

```csharp
// Check property value
assertion.ShouldHaveProperty(order => order.Status, OrderStatus.Shipped);
assertion.ShouldHaveProperty(order => order.Total, 99.99m);

// Check state with predicate
assertion.ShouldHaveState(order => order.IsShipped && order.TrackingNumber != null);

// Check state with custom assertion
assertion.ShouldHaveState(order =>
{
    Assert.Equal("customer-1", order.CustomerId);
    Assert.True(order.Total > 0);
});
```

### Exception Assertions

```csharp
// Check specific exception type was thrown
assertion.ShouldThrow<InvalidOperationException>();

// Check no exception was thrown
assertion.ShouldNotThrow();
```

### Direct Aggregate Access

```csharp
// Get the aggregate for custom assertions
var aggregate = await builder.ThenAggregate();
Assert.Equal("customer-1", aggregate.CustomerId);
```

## Testing with Time

Use `TestClock` for deterministic time-based tests:

```csharp
[Fact]
public async Task Order_ShouldRecordCorrectShipmentTime()
{
    // Arrange - create context with controlled time
    var testClock = new TestClock(new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.Zero));
    var context = TestSetup.GetContext(serviceProvider, testClock);

    // Act
    await AggregateTestBuilder.For<Order>("order-1", context)
        .Given(new OrderCreated("customer", 100m))
        .When(async order => await order.Ship("TRACK-001"))
        .Then(assertion =>
        {
            assertion.ShouldContainEvent<OrderShipped>(e =>
                e.ShippedAt == new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.Zero));
        });
}

[Fact]
public async Task Order_ShouldExpire_AfterTimeout()
{
    var testClock = new TestClock(DateTimeOffset.UtcNow);
    var context = TestSetup.GetContext(serviceProvider, testClock);

    await AggregateTestBuilder.For<Order>("order-1", context)
        .Given(new OrderCreated("customer", 100m))
        .When(order =>
        {
            // Advance time by 30 days
            testClock.AdvanceBy(TimeSpan.FromDays(30));
            order.CheckExpiration();
        })
        .Then(assertion => assertion.ShouldHaveAppended<OrderExpired>());
}
```

### TestClock API

```csharp
var clock = new TestClock();                    // Starts at current time
var clock = new TestClock(specificTime);        // Starts at specific time
var clock = new TestClock(localTimeZone: tz);   // With specific timezone

clock.SetTime(newTime);                         // Set absolute time
clock.AdvanceBy(TimeSpan.FromHours(2));         // Advance by duration
clock.Advance(TimeSpan.FromMinutes(30));        // Alias for AdvanceBy
clock.Freeze();                                 // Stop time from advancing
clock.Unfreeze();                               // Resume time advancement

var now = clock.UtcNow;                         // Get current UTC time
var localNow = clock.Now;                       // Get current local time
```

## Best Practices

### 1. Use ITestableAggregate for New Aggregates

```csharp
// Good - simple, clean, AOT-friendly
public class Order : Aggregate, ITestableAggregate<Order>
{
    public static string ObjectName => "order";
    public static Order Create(IEventStream stream) => new(stream);
    // ...
}
```

### 2. One Behavior Per Test

```csharp
// Good - tests one specific behavior
[Fact]
public async Task Ship_ShouldAppendShippedEvent()
{
    await AggregateTestBuilder.For<Order>("order", context)
        .Given(new OrderCreated(...))
        .When(async o => await o.Ship("TRACK"))
        .Then(a => a.ShouldHaveAppended<OrderShipped>());
}

// Avoid - testing multiple behaviors
[Fact]
public async Task Order_ShouldWork()  // Too vague
{
    // Tests creation, shipping, and state together
}
```

### 3. Use Fluent Then for Inline Assertions

```csharp
// Concise syntax for simple assertions
await AggregateTestBuilder.For<Order>("order", context)
    .Given(new OrderCreated(...))
    .When(async o => await o.Ship("TRACK"))
    .Then(a =>
    {
        a.ShouldHaveAppended<OrderShipped>();
        a.ShouldHaveProperty(o => o.IsShipped, true);
    });
```

### 4. Use Explicit Then for Complex Scenarios

```csharp
// When you need the builder reference or async assertions
var builderAfterWhen = await AggregateTestBuilder.For<Order>("order", context)
    .Given(new OrderCreated(...))
    .When(async o => await o.FailingCommand());

var assertion = await builderAfterWhen.Then();
assertion.ShouldThrow<InvalidOperationException>();
```

### 5. Register All Event Types in Aggregate Constructor

```csharp
public Order(IEventStream stream) : base(stream)
{
    stream.EventTypeRegistry.Add(typeof(OrderCreated), "OrderCreated", jsonTypeInfo);
    stream.EventTypeRegistry.Add(typeof(OrderShipped), "OrderShipped", jsonTypeInfo);
    stream.EventTypeRegistry.Add(typeof(OrderCancelled), "OrderCancelled", jsonTypeInfo);
}
```

## Pattern Comparison

| Aspect | ITestableAggregate | Explicit Factory |
|--------|-------------------|------------------|
| Syntax | `AggregateTestBuilder.For<T>(id, ctx)` | `AggregateTestBuilder<T>.For(name, id, ctx, factory)` |
| ObjectName | Provided by aggregate | Must be specified |
| Factory | Provided by aggregate | Must be specified |
| AOT Support | Full | Full |
| Custom Init | Via custom factory overload | Via lambda |
| Best For | New aggregates | Legacy code, testing variations |

## Migration Guide

To migrate from explicit factory to ITestableAggregate:

1. Add interface implementation:
   ```csharp
   public class Order : Aggregate, ITestableAggregate<Order>
   ```

2. Add static members:
   ```csharp
   public static string ObjectName => "order";
   public static Order Create(IEventStream stream) => new(stream);
   ```

3. Update tests:
   ```csharp
   // Before
   AggregateTestBuilder<Order>.For("order", id, ctx, s => new Order(s))

   // After
   AggregateTestBuilder.For<Order>(id, ctx)
   ```

## Testing Projections

The `ProjectionTestBuilder` supports two patterns for testing projections, similar to `AggregateTestBuilder`.

### Pattern 1: ITestableProjection (Recommended)

**When to use:** When your projection implements `ITestableProjection<TSelf>`

The CLI generates the `ITestableProjection` implementation with the `Create` static factory method.

```csharp
[Fact]
public async Task Should_aggregate_project_data()
{
    var context = TestSetup.GetContext();

    await ProjectionTestBuilder.For<ProjectDashboard>(context)
        .Given<Project>("project-1",
            new ProjectInitiated("Project A", "First project", "owner-1", DateTime.UtcNow),
            new MemberJoinedProject("member-1", "Developer", permissions, "owner-1", DateTime.UtcNow))
        .Given<Project>("project-2",
            new ProjectInitiated("Project B", "Second project", "owner-1", DateTime.UtcNow))
        .UpdateToLatest()
        .Then()
            .ShouldHaveState(p => p.TotalProjects == 2)
            .ShouldHaveState(p => p.TotalTeamMembers == 1);
}
```

### Pattern 2: Explicit Factory

**When to use:**
- Your projection doesn't implement `ITestableProjection`
- You need custom initialization logic
- You want to inject mock dependencies

```csharp
[Fact]
public async Task Should_track_completed_projects()
{
    var context = TestSetup.GetContext();

    await ProjectionTestBuilder<LegacyDashboard>.Create(
        context,
        (docFactory, streamFactory) => new LegacyDashboard(docFactory, streamFactory)
    )
    .GivenEvents("Project", "project-1",
        new ProjectInitiated("Project A", "Description", "owner-1", DateTime.UtcNow),
        new ProjectCompletedSuccessfully("Done", "owner-1", DateTime.UtcNow))
    .GivenEvents("Project", "project-2",
        new ProjectInitiated("Project B", "Description", "owner-1", DateTime.UtcNow))
    .UpdateToLatest()
    .Then()
        .ShouldHaveState(p => p.TotalProjects == 2)
        .ShouldHaveState(p => p.CompletedProjects == 1);
}
```

### Projection Pattern Comparison

| Aspect | ITestableProjection | Explicit Factory |
|--------|---------------------|------------------|
| Syntax | `ProjectionTestBuilder.For<T>(ctx)` | `ProjectionTestBuilder<T>.Create(ctx, factory)` |
| Factory | Provided by projection | Must be specified |
| AOT Support | Full | Full |
| Best For | New projections | Legacy code, mock injection |
