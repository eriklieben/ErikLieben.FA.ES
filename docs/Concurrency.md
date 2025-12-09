# Concurrency Control

The event sourcing framework provides built-in concurrency control through constraints and optimistic concurrency checking to ensure data consistency in concurrent environments.

## Overview

When opening an event stream session, you can specify a constraint that determines how the system handles stream existence and concurrent modifications:

| Constraint | Description | Use Case |
|------------|-------------|----------|
| `Constraint.Loose` | No version checking | Upsert-style operations |
| `Constraint.New` | Stream must not exist | Creating new aggregates |
| `Constraint.Existing` | Stream must exist | Updating existing aggregates |

## Constraint Types

### Loose Constraint

Use when you don't care whether the stream exists or not. The operation will succeed regardless of stream state.

```csharp
// Loose constraint - no version checking
// Use when you don't care if the stream is new or existing

var session = await eventStream.OpenSessionAsync(Constraint.Loose);

// This will work whether the stream exists or not
session.Append(new OrderCreated { OrderId = orderId });

await session.CommitAsync();
```

### New Constraint

Use when creating new aggregates to prevent duplicates. The operation fails if the stream already exists.

```csharp
// New constraint - only works if stream doesn't exist
// Use for creating new aggregates to prevent duplicates

var session = await eventStream.OpenSessionAsync(Constraint.New);

// This will throw if the stream already exists
session.Append(new CustomerCreated
{
    CustomerId = customerId,
    Name = name,
    Email = email
});

await session.CommitAsync();

// Throws ConcurrencyException if stream already exists
```

### Existing Constraint

Use when updating aggregates that must already exist. The operation fails if the stream doesn't exist.

```csharp
// Existing constraint - only works if stream exists
// Use when updating aggregates that must already exist

var session = await eventStream.OpenSessionAsync(Constraint.Existing);

// This will throw if the stream doesn't exist
session.Append(new OrderShipped
{
    OrderId = orderId,
    ShippedAt = DateTimeOffset.UtcNow
});

await session.CommitAsync();

// Throws ConcurrencyException if stream doesn't exist
```

## Optimistic Concurrency

The framework uses optimistic concurrency control by tracking the expected version of the stream. When you read state and then append events, the system ensures no other process has modified the stream in between.

```csharp
// Optimistic concurrency with version tracking
// The system tracks the expected version automatically

var session = await eventStream.OpenSessionAsync(Constraint.Existing);

// Read current state (tracks version internally)
var order = await session.GetAsync<Order>();

// Make changes based on current state
if (order.Status == OrderStatus.Pending)
{
    session.Append(new OrderConfirmed { OrderId = order.Id });
}

// Commit will fail if another process modified the stream
await session.CommitAsync();
```

## Handling Concurrency Conflicts

When a concurrency conflict occurs, a `ConcurrencyException` is thrown. Implement retry logic to handle these scenarios:

```csharp
// Handling concurrency conflicts with retry logic
public async Task AppendWithRetryAsync<TEvent>(
    IEventStream eventStream,
    TEvent @event,
    int maxRetries = 3)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            var session = await eventStream.OpenSessionAsync(Constraint.Existing);
            session.Append(@event);
            await session.CommitAsync();
            return; // Success
        }
        catch (ConcurrencyException)
        {
            if (attempt == maxRetries - 1)
                throw; // Rethrow on final attempt

            // Exponential backoff
            await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)));
        }
    }
}
```

## Complete Example: Command Handler

```csharp
public class OrderCommandHandler
{
    private readonly IEventStreamFactory _eventStreamFactory;

    public async Task Handle(CreateOrderCommand command)
    {
        var stream = _eventStreamFactory.GetStream(command.OrderId);

        // Use New constraint to ensure order doesn't already exist
        var session = await stream.OpenSessionAsync(Constraint.New);

        session.Append(new OrderCreated
        {
            OrderId = command.OrderId,
            CustomerId = command.CustomerId,
            Items = command.Items
        });

        await session.CommitAsync();
    }

    public async Task Handle(ShipOrderCommand command)
    {
        var stream = _eventStreamFactory.GetStream(command.OrderId);

        // Use Existing constraint to ensure order exists
        var session = await stream.OpenSessionAsync(Constraint.Existing);

        var order = await session.GetAsync<Order>();

        if (order.Status != OrderStatus.Confirmed)
            throw new InvalidOperationException("Order must be confirmed before shipping");

        session.Append(new OrderShipped
        {
            OrderId = command.OrderId,
            ShippedAt = DateTimeOffset.UtcNow
        });

        await session.CommitAsync();
    }
}
```

## Best Practices

1. **Use `Constraint.New` for creation** - Prevents duplicate aggregate creation
2. **Use `Constraint.Existing` for updates** - Ensures aggregate exists before modification
3. **Implement retry logic** - Handle transient concurrency conflicts gracefully
4. **Use exponential backoff** - Prevents retry storms in high-contention scenarios
5. **Keep transactions short** - Reduces the window for conflicts
6. **Consider domain-specific conflict resolution** - Some conflicts can be resolved by merging changes
