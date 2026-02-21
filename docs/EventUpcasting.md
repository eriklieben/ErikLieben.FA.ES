# Event Upcasting

Event upcasting handles schema evolution by transforming old event versions to new ones at read time. This allows you to change event structures without migrating historical data.

## Overview

When event schemas change over time, you have two options:

1. **Migrate all historical events** - Expensive and risky
2. **Upcast events at read time** - Transform old events on-the-fly

ErikLieben.FA.ES provides upcasting as the recommended approach for non-breaking schema changes.

## When to Use Upcasting

| Scenario | Approach |
|----------|----------|
| Add optional field | No upcaster needed (use defaults) |
| Rename field | Upcaster transforms old to new |
| Split event into multiple | Upcaster yields multiple events |
| Merge fields | Upcaster combines values |
| Change data type | Upcaster converts types |
| Remove field | No upcaster needed (just stop using) |

## Basic Structure

### 1. Create an Upcaster

Implement `IUpcastEvent`:

```csharp
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Upcasting;

namespace MyApp.Upcasters;

public class OrderCreatedV1ToV2Upcaster : IUpcastEvent
{
    public bool CanUpcast(IEvent @event)
    {
        // Match the old event type
        return @event.EventType == "Order.Created" && @event.EventVersion == 1;
    }

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        // Deserialize old event format
        var oldEvent = JsonEvent.ToEvent(@event,
            OrderCreatedV1JsonSerializerContext.Default.OrderCreatedV1);
        var data = oldEvent.Data();

        // Create new event with transformed data
        yield return new Event<OrderCreatedV2>
        {
            EventType = "Order.Created",
            EventVersion = 2,
            Data = new OrderCreatedV2(
                data.CustomerId,
                data.CreatedAt,
                CustomerType.Unknown  // New required field with default
            ),
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}
```

### 2. Register the Upcaster

Apply `[UseUpcaster<T>]` to your aggregate:

```csharp
[Aggregate]
[UseUpcaster<OrderCreatedV1ToV2Upcaster>]
public partial class Order : Aggregate
{
    public Order(IEventStream stream) : base(stream) { }

    // Event handlers work with the latest version
    private void When(OrderCreatedV2 @event)
    {
        CustomerId = @event.CustomerId;
        CustomerType = @event.CustomerType;
    }
}
```

### 3. Version Your Events

Use `[EventVersion]` to track schema versions:

```csharp
// Original event (V1)
[EventName("Order.Created")]
[EventVersion(1)]
public record OrderCreatedV1(
    string CustomerId,
    DateTime CreatedAt);

// New event (V2) with additional field
[EventName("Order.Created")]
[EventVersion(2)]
public record OrderCreatedV2(
    string CustomerId,
    DateTime CreatedAt,
    CustomerType CustomerType);
```

## IUpcastEvent Interface

```csharp
public interface IUpcastEvent
{
    /// <summary>
    /// Determines whether this upcaster can handle the specified event.
    /// </summary>
    bool CanUpcast(IEvent @event);

    /// <summary>
    /// Upcasts an event to one or more newer event versions.
    /// </summary>
    IEnumerable<IEvent> UpCast(IEvent @event);
}
```

## Common Patterns

### Adding a Required Field

```csharp
public class OrderCreatedAddCustomerType : IUpcastEvent
{
    public bool CanUpcast(IEvent @event) =>
        @event.EventType == "Order.Created" && @event.EventVersion == 1;

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var old = JsonEvent.ToEvent(@event,
            OrderCreatedV1Context.Default.OrderCreatedV1).Data();

        yield return new Event<OrderCreatedV2>
        {
            EventType = "Order.Created",
            EventVersion = 2,
            Data = new OrderCreatedV2(
                old.CustomerId,
                old.CreatedAt,
                CustomerType.Unknown  // Default value
            ),
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}
```

### Renaming Fields

```csharp
public class OrderCreatedRenameField : IUpcastEvent
{
    public bool CanUpcast(IEvent @event) =>
        @event.EventType == "Order.Created" && @event.EventVersion == 1;

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var old = JsonEvent.ToEvent(@event,
            OrderCreatedV1Context.Default.OrderCreatedV1).Data();

        yield return new Event<OrderCreatedV2>
        {
            EventType = "Order.Created",
            EventVersion = 2,
            Data = new OrderCreatedV2(
                old.UserId,      // Old: UserId
                old.CreatedAt    // New: CustomerId (same value, different name)
            ),
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}
```

### Splitting One Event into Multiple

```csharp
public class ProjectCompletedSplit : IUpcastEvent
{
    public bool CanUpcast(IEvent @event) =>
        @event.EventType == "Project.Completed";

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var old = JsonEvent.ToEvent(@event,
            ProjectCompletedJsonSerializerContext.Default.ProjectCompleted).Data();

        // Determine specific outcome from generic completion
        var outcome = old.Outcome?.ToLowerInvariant() ?? "";

        IEvent newEvent = outcome switch
        {
            _ when outcome.Contains("success") =>
                CreateEvent("Project.CompletedSuccessfully",
                    new ProjectCompletedSuccessfully(old.Outcome ?? "", old.CompletedBy, old.CompletedAt),
                    @event),

            _ when outcome.Contains("cancel") =>
                CreateEvent("Project.Cancelled",
                    new ProjectCancelled(old.Outcome ?? "", old.CompletedBy, old.CompletedAt),
                    @event),

            _ when outcome.Contains("fail") =>
                CreateEvent("Project.Failed",
                    new ProjectFailed(old.Outcome ?? "", old.CompletedBy, old.CompletedAt),
                    @event),

            _ => CreateEvent("Project.CompletedSuccessfully",
                    new ProjectCompletedSuccessfully(old.Outcome ?? "", old.CompletedBy, old.CompletedAt),
                    @event)
        };

        yield return newEvent;
    }

    private static IEvent CreateEvent<T>(string eventType, T data, IEvent original) where T : class
    {
        return new Event<T>
        {
            EventType = eventType,
            EventVersion = original.EventVersion,
            Data = data,
            ActionMetadata = original.ActionMetadata ?? new ActionMetadata(),
            Metadata = original.Metadata
        };
    }
}
```

### Chained Upcasters (Multi-Version)

For events that have evolved through multiple versions:

```csharp
[Aggregate]
[UseUpcaster<OrderCreatedV1ToV2Upcaster>]  // v1 -> v2
[UseUpcaster<OrderCreatedV2ToV3Upcaster>]  // v2 -> v3
public partial class Order : Aggregate
{
    // Handler only needs to handle the latest version
    private void When(OrderCreatedV3 @event) { }
}
```

Each upcaster handles one version jump:

```csharp
public class OrderCreatedV1ToV2Upcaster : IUpcastEvent
{
    public bool CanUpcast(IEvent @event) =>
        @event.EventType == "Order.Created" && @event.EventVersion == 1;

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var old = JsonEvent.ToEvent(@event,
            OrderCreatedV1Context.Default.OrderCreatedV1).Data();

        yield return new Event<OrderCreatedV2>
        {
            EventType = "Order.Created",
            EventVersion = 2,  // Upcast to v2
            Data = new OrderCreatedV2(old.CustomerId, old.CreatedAt, CustomerType.Unknown),
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}

public class OrderCreatedV2ToV3Upcaster : IUpcastEvent
{
    public bool CanUpcast(IEvent @event) =>
        @event.EventType == "Order.Created" && @event.EventVersion == 2;

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var old = JsonEvent.ToEvent(@event,
            OrderCreatedV2Context.Default.OrderCreatedV2).Data();

        yield return new Event<OrderCreatedV3>
        {
            EventType = "Order.Created",
            EventVersion = 3,  // Upcast to v3
            Data = new OrderCreatedV3(old.CustomerId, old.CreatedAt, old.CustomerType, ""),
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}
```

### Changing Event Type Name

```csharp
public class RenameEventType : IUpcastEvent
{
    public bool CanUpcast(IEvent @event) =>
        @event.EventType == "Order.ItemAdded";  // Old name

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var old = JsonEvent.ToEvent(@event,
            OrderItemAddedContext.Default.OrderItemAdded).Data();

        yield return new Event<LineItemAdded>  // New type
        {
            EventType = "Order.LineItemAdded",  // New name
            EventVersion = 1,
            Data = new LineItemAdded(old.ProductId, old.Quantity, old.Price),
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}
```

## Event Version Attribute

Track schema versions with `[EventVersion]`:

```csharp
// Default version is 1 (no attribute needed)
[EventName("Order.Created")]
public record OrderCreated(string CustomerId);

// Explicit version
[EventName("Order.Created")]
[EventVersion(2)]
public record OrderCreatedV2(string CustomerId, CustomerType Type);

// Newer version
[EventName("Order.Created")]
[EventVersion(3)]
public record OrderCreatedV3(string CustomerId, CustomerType Type, string Notes);
```

## Event Upcaster Registry

For performance-critical scenarios, use the registry directly:

```csharp
var registry = new EventUpcasterRegistry();

// Register upcasters with typed delegates
registry.Add<OrderCreatedV1, OrderCreatedV2>(
    "Order.Created",
    fromVersion: 1,
    toVersion: 2,
    upcast: v1 => new OrderCreatedV2(v1.CustomerId, v1.CreatedAt, CustomerType.Unknown));

registry.Add<OrderCreatedV2, OrderCreatedV3>(
    "Order.Created",
    fromVersion: 2,
    toVersion: 3,
    upcast: v2 => new OrderCreatedV3(v2.CustomerId, v2.CreatedAt, v2.CustomerType, ""));

// Freeze for optimized lookups
registry.Freeze();

// Upcast event data through multiple versions
var (data, finalVersion) = registry.UpcastToVersion(
    "Order.Created",
    currentVersion: 1,
    targetVersion: 3,
    eventData: oldEventData);
```

## Best Practices

### Do

- Keep old event types for deserialization
- Test upcasters with historical event data
- Document version changes in comments
- Use explicit version numbers
- Preserve all metadata when upcasting
- Run `dotnet faes` after adding upcasters

### Don't

- Don't delete old event type definitions (needed for deserialization)
- Don't change the semantic meaning of events
- Don't throw exceptions in upcasters (return empty or skip)
- Don't modify original event data
- Don't create circular upcaster chains

## Testing Upcasters

```csharp
[Fact]
public void Upcaster_ShouldTransformV1ToV2()
{
    // Arrange
    var upcaster = new OrderCreatedV1ToV2Upcaster();
    var v1Event = new JsonEvent
    {
        EventType = "Order.Created",
        EventVersion = 1,
        Payload = JsonSerializer.Serialize(new OrderCreatedV1("customer-1", DateTime.UtcNow))
    };

    // Act
    var result = upcaster.UpCast(v1Event).ToList();

    // Assert
    Assert.Single(result);
    var upcastedEvent = result[0];
    Assert.Equal("Order.Created", upcastedEvent.EventType);
    Assert.Equal(2, upcastedEvent.EventVersion);

    var data = (OrderCreatedV2)((Event<OrderCreatedV2>)upcastedEvent).Data;
    Assert.Equal("customer-1", data.CustomerId);
    Assert.Equal(CustomerType.Unknown, data.CustomerType);
}

[Fact]
public async Task Aggregate_ShouldLoadWithUpcastedEvents()
{
    var context = TestSetup.GetContext();

    // Given v1 events in the stream
    await AggregateTestBuilder.For<Order>("order-1", context)
        .Given(new JsonEvent
        {
            EventType = "Order.Created",
            EventVersion = 1,
            Payload = JsonSerializer.Serialize(new OrderCreatedV1("customer-1", DateTime.UtcNow))
        })
        .Then(order =>
        {
            // State should reflect upcasted event
            Assert.Equal("customer-1", order.CustomerId);
            Assert.Equal(CustomerType.Unknown, order.CustomerType);
        });
}
```

## Migration vs Upcasting

| Aspect | Upcasting | Migration |
|--------|-----------|-----------|
| Data modification | None | Rewrites events |
| Performance | Read-time overhead | One-time cost |
| Rollback | Easy (remove upcaster) | Complex |
| Disk space | Same | May increase/decrease |
| Complexity | Per-version logic | Batch operation |

**Use upcasting when:**
- Schema changes are additive
- Historical data volume is large
- You need quick iteration
- Rollback capability is important

**Use migration when:**
- Event names change fundamentally
- Data needs consolidation
- Storage optimization needed
- You're changing storage providers

## See Also

- [Aggregates](Aggregates.md) - Aggregate patterns
- [Events](GettingStarted.md#step-1-create-an-event) - Event definitions
- [Live Migration](LiveMigration.md) - Stream migration
- [Testing](Testing.md) - Testing patterns
