# ErikLieben.FA.ES - AI Coding Assistant Instructions

This file provides patterns and guidance for AI assistants working with the ErikLieben.FA.ES event sourcing library.

## Library Overview

ErikLieben.FA.ES is an event sourcing framework for .NET featuring:
- **Code generation** via `dotnet faes` CLI
- **Multiple storage providers** (Azure Blob, Table, CosmosDB, S3-compatible)
- **AOT-compatible** (Native AOT, no reflection)
- **Roslyn analyzers** for compile-time validation

## Required Workflow

After ANY change to aggregates, projections, or events:
```bash
dotnet faes
```

This generates:
- `*.Generated.cs` files with Fold methods and JSON contexts
- `*Extensions.Generated.cs` with DI registration
- Factories and repositories

## Aggregate Pattern

### Correct Implementation

```csharp
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Processors;

namespace MyDomain.Aggregates;

[Aggregate]
public partial class Order : Aggregate
{
    // Constructor takes IEventStream
    public Order(IEventStream stream) : base(stream) { }

    // State as public properties with private setters
    public string? CustomerId { get; private set; }
    public string? Status { get; private set; }
    public decimal Total { get; private set; }
    public List<OrderLine> Lines { get; } = new();

    // Command: Returns Task, uses Stream.Session, wraps Append in Fold
    public async Task Create(string customerId)
    {
        await Stream.Session(context =>
            Fold(context.Append(new OrderCreated(customerId, DateTime.UtcNow))));
    }

    public async Task AddLine(string productId, int quantity, decimal price)
    {
        await Stream.Session(context =>
            Fold(context.Append(new OrderLineAdded(productId, quantity, price))));
    }

    public async Task Ship(string trackingNumber)
    {
        // Validate before appending
        if (Status == "Shipped")
            throw new InvalidOperationException("Already shipped");

        await Stream.Session(context =>
            Fold(context.Append(new OrderShipped(trackingNumber, DateTime.UtcNow))));
    }

    // When methods: Apply events to state (called by generated Fold method)
    private void When(OrderCreated @event)
    {
        CustomerId = @event.CustomerId;
        Status = "Created";
    }

    private void When(OrderLineAdded @event)
    {
        Lines.Add(new OrderLine(@event.ProductId, @event.Quantity, @event.Price));
        Total += @event.Quantity * @event.Price;
    }

    private void When(OrderShipped @event)
    {
        Status = "Shipped";
    }
}
```

### Common Mistakes

```csharp
// WRONG: Append without Fold - event won't be applied to state
await Stream.Session(context =>
    context.Append(new OrderCreated(customerId, DateTime.UtcNow))); // Missing Fold!

// CORRECT: Always wrap in Fold
await Stream.Session(context =>
    Fold(context.Append(new OrderCreated(customerId, DateTime.UtcNow))));

// WRONG: Modifying state directly in command
public async Task Ship(string trackingNumber)
{
    Status = "Shipped"; // NO! State should only change in When methods
    await Stream.Session(context => ...);
}

// WRONG: Non-partial class
public class Order : Aggregate // Missing 'partial' keyword!

// WRONG: Missing [Aggregate] attribute
public partial class Order : Aggregate // Missing [Aggregate]!
```

## Event Pattern

### Correct Implementation

```csharp
using ErikLieben.FA.ES.Attributes;

namespace MyDomain.Events.Order;

// Events are immutable records with [EventName] attribute
[EventName("Order.Created")]
public record OrderCreated(string CustomerId, DateTime CreatedAt);

[EventName("Order.LineAdded")]
public record OrderLineAdded(string ProductId, int Quantity, decimal Price);

[EventName("Order.Shipped")]
public record OrderShipped(string TrackingNumber, DateTime ShippedAt);
```

### Event Naming Convention

- Format: `{AggregateName}.{PastTenseVerb}` (e.g., "Order.Created", "Order.Shipped")
- Always past tense (events describe what happened)

## Projection Pattern

### Correct Implementation

```csharp
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Projections;

namespace MyDomain.Projections;

[BlobJsonProjection("projections")]
public partial class OrderDashboard : Projection
{
    // State properties
    public int TotalOrders { get; private set; }
    public decimal TotalRevenue { get; private set; }
    public Dictionary<string, int> OrdersByStatus { get; } = new();

    // When methods handle events from ANY aggregate type
    private void When(OrderCreated @event)
    {
        TotalOrders++;
        OrdersByStatus["Created"] = OrdersByStatus.GetValueOrDefault("Created") + 1;
    }

    private void When(OrderCompleted @event)
    {
        TotalRevenue += @event.Amount;
        OrdersByStatus["Completed"] = OrdersByStatus.GetValueOrDefault("Completed") + 1;
    }
}
```

### Projection with Object ID (per-entity projections)

```csharp
[BlobJsonProjection("projections")]
public partial class OrderSummary : Projection
{
    public string? OrderId { get; private set; }
    public string? CustomerName { get; private set; }

    // Use WhenParameterValueFactory to get the object ID
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(OrderCreated @event, string orderId)
    {
        OrderId = orderId;
    }
}
```

## Storage Provider Configuration

### Fluent Builder API (Recommended)

```csharp
// In Program.cs - Use fluent builder for clean configuration
builder.Services.AddFaes(faes => faes
    .UseDefaultStorage("blob")
    .UseBlobStorage(new EventStreamBlobSettings("Store", autoCreateContainer: true))
    .UseTableStorage(new EventStreamTableSettings("Tables"))
    .UseCosmosDb(new EventStreamCosmosDbSettings { DatabaseName = "eventstore" })
    .UseS3Storage(new EventStreamS3Settings("s3",
        serviceUrl: "http://localhost:9000",
        accessKey: "minioadmin",
        secretKey: "minioadmin",
        autoCreateBucket: true))
);
```

### Classic Configuration (Also Supported)

#### Azure Blob Storage (Default)

```csharp
// In Program.cs
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store", autoCreateContainer: true));
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));
```

#### Azure Table Storage

```csharp
// On the aggregate class
[EventStreamType("table", "table")]
public partial class MyAggregate : Aggregate { }

// In Program.cs
builder.Services.ConfigureTableEventStore(new EventStreamTableSettings("Store"));
```

#### Azure CosmosDB

```csharp
// On the aggregate class
[EventStreamType("cosmosdb", "cosmosdb")]
public partial class MyAggregate : Aggregate { }

// In Program.cs
builder.Services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings("CosmosConnection"));
```

#### S3-Compatible Storage (AWS S3, MinIO, Scaleway, etc.)

```csharp
// On the aggregate class
[EventStreamType("s3", "s3")]
public partial class MyAggregate : Aggregate { }

// In Program.cs
builder.Services.ConfigureS3EventStore(new EventStreamS3Settings("s3",
    serviceUrl: "http://localhost:9000",
    accessKey: "minioadmin",
    secretKey: "minioadmin",
    autoCreateBucket: true));
```

## ASP.NET Core Minimal APIs

### Aggregate Binding

```csharp
// The [EventStream] attribute binds aggregates from route parameters
app.MapPost("/orders/{id}/ship", async (
    [EventStream] Order order,
    [FromBody] ShipOrderRequest request) =>
{
    await order.Ship(request.TrackingNumber);
    return Results.Ok();
});

// Create new aggregate
app.MapPost("/orders", async (
    [EventStream(CreateIfNotExists = true)] Order order,
    [FromBody] CreateOrderRequest request) =>
{
    await order.Create(request.CustomerId);
    return Results.Created($"/orders/{order.Id}", new { order.Id });
});
```

### Projection Binding

```csharp
// Load global projection
app.MapGet("/dashboard", async ([Projection] OrderDashboard dashboard) =>
{
    return Results.Ok(dashboard);
});

// Load routed projection (per-entity)
app.MapGet("/orders/{id}/summary", async ([Projection("{id}")] OrderSummary summary) =>
{
    return Results.Ok(summary);
});
```

## Testing Pattern

```csharp
using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.Builders;

[Fact]
public async Task Order_ShouldBeShipped_WhenShipCommandExecuted()
{
    var context = TestSetup.GetContext();

    await AggregateTestBuilder.For<Order>("order-123", context)
        .Given(new OrderCreated("customer-1", DateTime.UtcNow))
        .When(async order => await order.Ship("TRACK-001"))
        .Then(assertion =>
        {
            assertion.ShouldHaveAppended<OrderShipped>();
            assertion.ShouldHaveProperty(o => o.Status, "Shipped");
        });
}
```

## Event Upcasting (Schema Evolution)

```csharp
using ErikLieben.FA.ES.Upcasting;

// Define upcaster for event version migration
public class OrderCreatedV1ToV2Upcaster : IUpcastEvent
{
    public bool CanUpcast(IEvent @event)
        => @event.EventType == "order.created" && @event.SchemaVersion == 1;

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var v1 = JsonEvent.To(@event, OrderCreatedV1Context.Default.OrderCreatedV1);
        yield return new JsonEvent
        {
            EventType = "order.created",
            SchemaVersion = 2,
            Payload = JsonSerializer.Serialize(new OrderCreatedV2(v1.OrderId, ""))
        };
    }
}

// Register on aggregate
[Aggregate]
[UseUpcaster<OrderCreatedV1ToV2Upcaster>]
public partial class Order : Aggregate { }
```

## File Structure Convention

```
MyDomain/
  Aggregates/
    Order.cs                    # Your code
    Order.Generated.cs          # Generated by CLI
  Events/
    Order/
      OrderCreated.cs
      OrderShipped.cs
  Projections/
    OrderDashboard.cs           # Your code
    OrderDashboard.Generated.cs # Generated by CLI
  MyDomainExtensions.Generated.cs  # Generated DI registration
```

## DI Registration

```csharp
// In Program.cs
builder.Services.ConfigureMyDomainFactory(); // Generated extension method
```

## Projection Catch-Up

When deploying new projections or rebuilding existing ones, use `ICatchUpDiscoveryService` to discover all objects that need processing:

```csharp
// Inject ICatchUpDiscoveryService (registered by ConfigureEventStore)
public class CatchUpProcessor
{
    private readonly ICatchUpDiscoveryService _discoveryService;

    public CatchUpProcessor(ICatchUpDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    public async Task ProcessAllAsync()
    {
        // Stream all work items for specified object types
        await foreach (var item in _discoveryService.StreamWorkItemsAsync(["project", "workitem"]))
        {
            // item.ObjectName = "project" or "workitem"
            // item.ObjectId = the unique object identifier
            // Process: load projection, update to latest, save
        }
    }

    public async Task ProcessWithPaginationAsync()
    {
        string? continuationToken = null;
        do
        {
            var result = await _discoveryService.DiscoverWorkItemsAsync(
                ["project"], pageSize: 50, continuationToken);

            foreach (var item in result.WorkItems)
            {
                // Process item
            }

            continuationToken = result.ContinuationToken;
        } while (continuationToken != null);
    }
}
```

For orchestration patterns (Durable Functions, queues), see `demo/src/TaskFlow.Functions/Functions/CatchUpFunctions.cs`.

## Result Types for Error Handling

The library provides `Result` and `Result<T>` types for explicit error handling:

```csharp
using ErikLieben.FA.ES.Results;

// Return Result from operations that can fail
public async Task<Result<Order>> GetOrderAsync(string id)
{
    var order = await _orderFactory.GetAsync(id);
    if (order == null)
        return EventSourcingErrors.AggregateNotFound("Order", id);

    return order;  // Implicit conversion to Result<Order>
}

// Handle results
var result = await GetOrderAsync(orderId);
if (result.IsSuccess)
{
    var order = result.Value;
    // Use order...
}
else
{
    logger.LogError("Error: {Code} - {Message}", result.Error!.Code, result.Error.Message);
}

// Functional operations
var dtoResult = result
    .Map(o => new OrderDto(o.Id, o.Status))
    .OnFailure(e => logger.LogWarning("Failed: {Code}", e.Code));
```

### Common Error Factories

```csharp
EventSourcingErrors.StreamNotFound("order-123")
EventSourcingErrors.ConcurrencyConflict("order-123", expected: 5, actual: 7)
EventSourcingErrors.AggregateNotFound("Order", "order-123")
EventSourcingErrors.ValidationFailed("Email is required")
Error.FromException(ex)
```

## Key Points for AI Assistants

1. **Always suggest running `dotnet faes`** after any aggregate/projection/event changes
2. **Always use `Fold(context.Append(...))`** - the analyzer warns but doesn't prevent compilation
3. **Classes must be partial** - generated code needs to extend them
4. **Events are records** - immutable, with [EventName] attribute
5. **State changes only in When methods** - never in commands
6. **Prefer fluent `AddFaes()` builder** for new projects
7. **Use Result types** for operations that can fail predictably
8. **Check for existing patterns** in `demo/src/TaskFlow.Domain/` before suggesting new approaches
