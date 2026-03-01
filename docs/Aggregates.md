# Aggregates

Aggregates are the core building blocks of event-sourced systems. They encapsulate domain logic, handle commands, emit events, and maintain consistent state.

## Overview

An aggregate in ErikLieben.FA.ES:

1. **Handles commands** - Methods that validate and execute business logic
2. **Emits events** - Records of what happened
3. **Applies events to state** - `When` methods that update internal state
4. **Maintains invariants** - Ensures business rules are always satisfied

## Basic Structure

```csharp
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Processors;

namespace MyApp.Aggregates;

[Aggregate]
public partial class Order : Aggregate
{
    // Constructor - receives the event stream
    public Order(IEventStream stream) : base(stream) { }

    // State properties - private setters
    public string? CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal Total { get; private set; }

    // Command methods
    public async Task Create(string customerId) { /* ... */ }
    public async Task AddItem(string productId, int qty, decimal price) { /* ... */ }

    // Event handlers (When methods)
    private void When(OrderCreated @event) { /* ... */ }
    private void When(OrderItemAdded @event) { /* ... */ }
}
```

## Required Elements

### 1. Attributes and Inheritance

```csharp
[Aggregate]                          // Required attribute
public partial class Order : Aggregate  // Must be partial, must inherit Aggregate
```

### 2. Constructor

```csharp
public Order(IEventStream stream) : base(stream) { }
```

The constructor receives an `IEventStream` and passes it to the base class. You can add additional setup here:

```csharp
public Order(IEventStream stream) : base(stream)
{
    // Register stream actions
    stream.RegisterAction(new AuditLogAction());
}
```

### 3. State Properties

```csharp
// Simple properties
public string? Name { get; private set; }
public int Count { get; private set; }

// Collections (initialize in declaration)
public List<OrderItem> Items { get; } = new();
public Dictionary<string, decimal> Prices { get; } = new();
```

**Key rules:**
- Use `private set` - state changes only through events
- Initialize collections in the declaration
- Properties appear in the generated interface

### 4. Command Methods

Commands are async methods that validate and emit events:

```csharp
public async Task Create(string customerId)
{
    // Validation
    if (string.IsNullOrWhiteSpace(customerId))
        throw new ArgumentException("Customer ID required");

    // Emit event using Session + Fold pattern
    await Stream.Session(context =>
        Fold(context.Append(new OrderCreated(customerId, DateTime.UtcNow))));
}
```

### 5. When Methods

When methods apply events to state:

```csharp
private void When(OrderCreated @event)
{
    CustomerId = @event.CustomerId;
    Status = OrderStatus.Created;
}

private void When(OrderItemAdded @event)
{
    Items.Add(new OrderItem(@event.ProductId, @event.Quantity, @event.Price));
    Total += @event.Quantity * @event.Price;
}
```

**Key rules:**
- Always `private void When(EventType @event)`
- No async operations - state changes only
- No validation - events have already happened
- No side effects - deterministic state rebuilding

## Command Patterns

### Basic Command

```csharp
public async Task Ship(string trackingNumber)
{
    await Stream.Session(context =>
        Fold(context.Append(new OrderShipped(trackingNumber, DateTime.UtcNow))));
}
```

### Command with Validation

```csharp
public async Task Ship(string trackingNumber)
{
    // Validate current state
    if (Status != OrderStatus.Confirmed)
        throw new InvalidOperationException("Order must be confirmed before shipping");

    if (string.IsNullOrWhiteSpace(trackingNumber))
        throw new ArgumentException("Tracking number required");

    await Stream.Session(context =>
        Fold(context.Append(new OrderShipped(trackingNumber, DateTime.UtcNow))));
}
```

### Command with Result Pattern

Using the Result pattern for validation (recommended for complex validation):

```csharp
public async Task<Result> Ship(string trackingNumber)
{
    var validation = Result.Success()
        .Validate(() => Status == OrderStatus.Confirmed, "Order must be confirmed")
        .Validate(() => !string.IsNullOrWhiteSpace(trackingNumber), "Tracking number required");

    if (validation.IsFailure)
        return validation;

    await Stream.Session(context =>
        Fold(context.Append(new OrderShipped(trackingNumber, DateTime.UtcNow))));

    return Result.Success();
}
```

### Command with Multiple Events

```csharp
public async Task CompleteWithDiscount(decimal discountPercent)
{
    await Stream.Session(context =>
    {
        // Apply discount first
        Fold(context.Append(new DiscountApplied(discountPercent, Total * discountPercent / 100)));

        // Then complete
        Fold(context.Append(new OrderCompleted(DateTime.UtcNow)));
    });
}
```

### Command with Metadata

```csharp
public async Task Ship(string trackingNumber, VersionToken? user = null)
{
    await Stream.Session(context =>
        Fold(context.Append(
            new OrderShipped(trackingNumber, DateTime.UtcNow),
            new ActionMetadata
            {
                EventOccuredAt = DateTime.UtcNow,
                OriginatedFromUser = user
            })));
}
```

## Event Definitions

### Basic Event

```csharp
[EventName("Order.Created")]
public record OrderCreated(string CustomerId, DateTime CreatedAt);
```

### Event with Complex Types

```csharp
[EventName("Order.ItemAdded")]
public record OrderItemAdded(
    string ProductId,
    int Quantity,
    decimal UnitPrice,
    List<string> Tags);
```

### Event Naming Convention

- Format: `{AggregateName}.{PastTenseVerb}`
- Examples: `Order.Created`, `Order.Shipped`, `Payment.Received`
- Always past tense (events describe what happened)

## Storage Configuration

### Default (Blob Storage)

No additional attributes needed - uses default blob storage.

### Table Storage

```csharp
[Aggregate]
[EventStreamType("table", "table")]
public partial class Order : Aggregate { }
```

### Cosmos DB

```csharp
[Aggregate]
[EventStreamType("cosmosdb", "cosmosdb")]
public partial class Order : Aggregate { }
```

### Custom Blob Storage Connection

```csharp
[Aggregate]
[EventStreamBlobSettings("CustomConnection")]
public partial class Order : Aggregate { }
```

## Aggregate Factory and Repository

The CLI generates a **factory** and a **repository** for each aggregate, with distinct responsibilities:

- **Factory** — creates and instantiates single aggregate instances
- **Repository** — retrieves and queries aggregates (single or multiple results)

### Factory (Creation)

Use the factory when you need to **create** a new aggregate:

```csharp
public class OrderService
{
    private readonly IOrderFactory _orderFactory;

    public OrderService(IOrderFactory orderFactory)
    {
        _orderFactory = orderFactory;
    }

    public async Task<Order> CreateOrder(string customerId)
    {
        var order = await _orderFactory.CreateAsync(Guid.NewGuid().ToString());
        await order.Create(customerId);
        return order;
    }
}
```

#### Factory Methods

| Method | Description |
|--------|-------------|
| `CreateAsync(id)` | Create a new aggregate with the given ID |
| `Create(IEventStream)` | Instantiate an aggregate from an event stream |
| `Create(IObjectDocument)` | Instantiate an aggregate from a document |

### Repository (Retrieval and Queries)

Use the repository when you need to **retrieve** or **query** aggregates:

```csharp
public class OrderService
{
    private readonly IOrderRepository _orderRepository;

    public OrderService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Order?> GetOrder(string orderId)
    {
        return await _orderRepository.GetByIdAsync(orderId);
    }

    public async Task<Order?> FindByEmail(string email)
    {
        return await _orderRepository.GetFirstByDocumentTagAsync(email);
    }
}
```

#### Repository Methods

| Method | Description |
|--------|-------------|
| `GetByIdAsync(id, upToVersion?)` | Get a single aggregate by ID (returns `null` if not found) |
| `GetByIdWithDocumentAsync(id)` | Get a single aggregate with its document |
| `GetFirstByDocumentTagAsync(tag)` | Get the first aggregate matching a document tag |
| `GetAllByDocumentTagAsync(tag)` | Get all aggregates matching a document tag |
| `GetObjectIdsAsync(token?, pageSize)` | Get a paginated list of all aggregate IDs |
| `ExistsAsync(id)` | Check if an aggregate exists |
| `CountAsync()` | Get total count of aggregates |

### Using Both Together

For endpoints that create or retrieve, inject both:

```csharp
app.MapPost("/orders", async (
    [FromBody] CreateOrderRequest request,
    IOrderFactory factory) =>
{
    var order = await factory.CreateAsync(Guid.NewGuid().ToString());
    await order.Create(request.CustomerId);
    return Results.Created($"/orders/{order.Id}", new { order.Id });
});

app.MapGet("/orders/{id}", async (
    string id,
    IOrderRepository repository) =>
{
    var order = await repository.GetByIdAsync(id);
    return order is not null ? Results.Ok(order) : Results.NotFound();
});
```

## Stream Actions

Register actions to execute code before/after events:

```csharp
public Order(IEventStream stream) : base(stream)
{
    stream.RegisterAction(new AuditLogAction());
    stream.RegisterAction(new ValidationAction());
}
```

See [Stream Actions](StreamActions.md) for details.

## Event Upcasting

Handle schema evolution with upcasters:

```csharp
[Aggregate]
[UseUpcaster<OrderCreatedUpcast>]
public partial class Order : Aggregate { }
```

See [Event Upcasting](EventUpcasting.md) for details.

## Concurrency

Control concurrent access with constraints:

```csharp
// In the aggregate
await Stream.Session(context => ..., Constraint.Existing);  // Must exist
await Stream.Session(context => ..., Constraint.New);       // Must not exist
await Stream.Session(context => ..., Constraint.Loose);     // Don't check
```

See [Concurrency](Concurrency.md) for details.

## Testing

Use the `AggregateTestBuilder` for Given-When-Then style tests:

```csharp
[Fact]
public async Task Order_ShouldBeShipped()
{
    var context = TestSetup.GetContext();

    await AggregateTestBuilder.For<Order>("order-1", context)
        .Given(new OrderCreated("customer-1", DateTime.UtcNow))
        .When(async order => await order.Ship("TRACK-001"))
        .Then(assertion =>
        {
            assertion.ShouldHaveAppended<OrderShipped>();
            assertion.ShouldHaveProperty(o => o.Status, OrderStatus.Shipped);
        });
}
```

See [Testing](Testing.md) for complete testing guide.

## Best Practices

### Do

- Keep aggregates focused on a single responsibility
- Validate invariants before emitting events
- Use meaningful event names in past tense
- Keep When methods simple and deterministic
- Use private setters for state properties
- Run `dotnet faes` after any changes

### Don't

- Don't modify state directly in commands
- Don't call external services in When methods
- Don't use `context.Append()` without `Fold()`
- Don't make aggregates non-partial
- Don't throw exceptions in When methods
- Don't put async code in When methods

## Common Patterns

### Soft Delete

```csharp
public bool IsDeleted { get; private set; }

public async Task Delete()
{
    if (IsDeleted) return;  // Idempotent
    await Stream.Session(context =>
        Fold(context.Append(new OrderDeleted(DateTime.UtcNow))));
}

private void When(OrderDeleted @event)
{
    IsDeleted = true;
}
```

### State Machine

```csharp
public OrderStatus Status { get; private set; }

public async Task Confirm()
{
    if (Status != OrderStatus.Created)
        throw new InvalidOperationException($"Cannot confirm from {Status}");

    await Stream.Session(context =>
        Fold(context.Append(new OrderConfirmed(DateTime.UtcNow))));
}

public async Task Ship(string tracking)
{
    if (Status != OrderStatus.Confirmed)
        throw new InvalidOperationException($"Cannot ship from {Status}");

    await Stream.Session(context =>
        Fold(context.Append(new OrderShipped(tracking, DateTime.UtcNow))));
}
```

### Tracking Changes

```csharp
public List<ChangeRecord> ChangeHistory { get; } = new();

private void When(OrderRenamed @event)
{
    ChangeHistory.Add(new ChangeRecord("Name", Name, @event.NewName, @event.ChangedAt));
    Name = @event.NewName;
}
```

## See Also

- [Getting Started](GettingStarted.md) - Quick setup guide
- [Events](EventUpcasting.md) - Event patterns and upcasting
- [Projections](Projections.md) - Read models from events
- [Testing](Testing.md) - Unit testing aggregates
- [Concurrency](Concurrency.md) - Handling concurrent access
