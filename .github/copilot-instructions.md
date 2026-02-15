# GitHub Copilot Instructions for ErikLieben.FA.ES

## Library Overview

ErikLieben.FA.ES is an event sourcing framework for .NET with code generation.

## Essential Commands

```bash
dotnet faes        # Generate code after any aggregate/projection/event changes
dotnet faes watch  # Watch mode for continuous generation
```

## Aggregate Pattern

```csharp
[Aggregate]
public partial class Order : Aggregate
{
    public Order(IEventStream stream) : base(stream) { }

    public string? Status { get; private set; }

    public async Task Create(string customerId)
    {
        await Stream.Session(context =>
            Fold(context.Append(new OrderCreated(customerId, DateTime.UtcNow))));
    }

    private void When(OrderCreated @event) { Status = "Created"; }
}
```

## Event Pattern

```csharp
[EventName("Order.Created")]
public record OrderCreated(string CustomerId, DateTime CreatedAt);
```

## Projection Pattern

```csharp
[BlobJsonProjection("projections")]
public partial class Dashboard : Projection
{
    public int Count { get; private set; }
    private void When(OrderCreated @event) { Count++; }
}
```

## Critical Rules

1. ALWAYS use `Fold(context.Append(...))` - never just `context.Append(...)`
2. ALWAYS make aggregate/projection classes partial
3. ALWAYS run `dotnet faes` after changes
4. Events must be immutable records with [EventName]
5. State changes only in When methods

## Reference

See `demo/src/TaskFlow.Domain/` for complete examples.
