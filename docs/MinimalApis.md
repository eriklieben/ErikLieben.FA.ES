# ASP.NET Core Minimal APIs Integration

The `ErikLieben.FA.ES.AspNetCore.MinimalApis` package provides seamless integration with ASP.NET Core Minimal APIs, offering parameter binding for aggregates and projections similar to `[FromServices]` or `[FromBody]`.

## Installation

```bash
dotnet add package ErikLieben.FA.ES.AspNetCore.MinimalApis
```

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure core event store services
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store", autoCreateContainer: true));
builder.Services.ConfigureMyDomainFactory(); // Your generated factory registration

// Add Minimal API bindings (optional - for future extensibility)
builder.Services.AddEventStoreMinimalApis();

var app = builder.Build();

// Use attribute-based binding in your endpoints
app.MapPost("/customers/{id}/register", async (
    [EventStream] Customer customer,
    [FromBody] RegisterCustomerCommand command) =>
{
    await customer.RegisterThroughWebsite(command.Name);
    return Results.Ok(customer.Id);
});

app.Run();
```

---

## EventStream Binding

The `[EventStream]` attribute binds an aggregate from the event store by loading the document and folding all events.

### Basic Usage

```csharp
// Uses "id" route parameter by default
app.MapGet("/orders/{id}", async ([EventStream] Order order) =>
{
    return Results.Ok(new { order.Id, order.Status, order.TotalAmount });
});

app.MapPost("/orders/{id}/items", async (
    [EventStream] Order order,
    [FromBody] AddItemCommand command) =>
{
    order.AddItem(command.ProductId, command.Quantity, command.Price);
    return Results.Ok();
});
```

### Custom Route Parameter

```csharp
// Use a different route parameter name
app.MapPost("/orders/{orderId}/ship", async (
    [EventStream("orderId")] Order order,
    [FromBody] ShipOrderCommand command) =>
{
    order.Ship(command.CarrierName, command.TrackingNumber);
    return Results.Ok();
});
```

### Creating New Aggregates

```csharp
// Create a new aggregate if it doesn't exist
app.MapPost("/orders", async (
    [EventStream(CreateIfNotExists = true)] Order order,
    [FromBody] CreateOrderCommand command) =>
{
    // order is a new, empty aggregate
    order.Create(command.CustomerId, command.Items);
    return Results.Created($"/orders/{order.Id}", new { order.Id });
});
```

### Full Configuration

```csharp
app.MapGet("/orders/{id}", async (
    [EventStream("id", ObjectType = "Order", Store = "blob", CreateIfNotExists = false)] Order order) =>
{
    return Results.Ok(order);
});
```

### Attribute Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RouteParameterName` | `string` | `"id"` | The route parameter containing the object ID |
| `ObjectType` | `string?` | `null` | Override the object type name (defaults to aggregate type name) |
| `CreateIfNotExists` | `bool` | `false` | Create a new aggregate if it doesn't exist |
| `Store` | `string?` | `null` | Specify a custom store name |

---

## Projection Binding

The `[Projection]` attribute binds a projection from storage.

### Basic Usage

```csharp
// Load a global projection (single instance)
app.MapGet("/dashboard", async ([Projection] DashboardProjection dashboard) =>
{
    return Results.Ok(new
    {
        dashboard.TotalOrders,
        dashboard.TotalRevenue,
        dashboard.ActiveCustomers
    });
});

app.MapGet("/customers", async ([Projection] CustomerListProjection customers) =>
{
    return Results.Ok(customers.CustomerNames);
});
```

### Routed Projections

For projections that are per-entity (e.g., order summary per order), use blob name patterns with route parameter substitution:

```csharp
// Routed projection with blob name from route parameter
app.MapGet("/orders/{id}/summary", async (
    [Projection("{id}")] OrderSummaryProjection summary) =>
{
    return Results.Ok(new
    {
        summary.OrderId,
        summary.CustomerName,
        summary.ItemCount,
        summary.TotalAmount,
        summary.Status
    });
});

// Multiple route parameters
app.MapGet("/tenants/{tenantId}/orders/{orderId}/summary", async (
    [Projection("{tenantId}/{orderId}")] OrderSummaryProjection summary) =>
{
    return Results.Ok(summary);
});
```

### Projection That Must Exist

```csharp
// Throws ProjectionNotFoundException if projection doesn't exist
app.MapGet("/reports/monthly", async (
    [Projection(CreateIfNotExists = false)] MonthlyReportProjection report) =>
{
    return Results.Ok(report);
});
```

### Attribute Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BlobNamePattern` | `string?` | `null` | Blob name pattern with route parameter substitution |
| `CreateIfNotExists` | `bool` | `true` | Create projection if it doesn't exist |

---

## Projection Output (Post-Execution Update)

Update projections after an endpoint executes successfully using endpoint filters.

### Basic Usage

```csharp
// Update a single projection after the endpoint completes
app.MapPost("/orders/{id}/items", async (
    [EventStream] Order order,
    [FromBody] AddItemCommand command) =>
{
    order.AddItem(command.ProductId, command.Quantity, command.Price);
    return Results.Ok();
})
.WithProjectionOutput<OrderSummaryProjection>();

// Alternative, more descriptive method name
app.MapPost("/orders/{id}/complete", async (
    [EventStream] Order order) =>
{
    order.Complete();
    return Results.Ok();
})
.AndUpdateProjectionToLatest<OrderSummaryProjection>();
```

### Multiple Projections

```csharp
// Update multiple projections after execution
app.MapPost("/orders/{id}/items", async (
    [EventStream] Order order,
    [FromBody] AddItemCommand command) =>
{
    order.AddItem(command.ProductId, command.Quantity, command.Price);
    return Results.Ok();
})
.WithProjectionOutput<OrderSummaryProjection>()
.WithProjectionOutput<InventoryProjection>()
.AndUpdateProjectionToLatest<DashboardProjection>();
```

### Routed Projection Output

```csharp
// Update a routed projection with blob name from route
app.MapPost("/orders/{id}/complete", async (
    [EventStream] Order order) =>
{
    order.Complete();
    return Results.Ok();
})
.WithProjectionOutput<OrderSummaryProjection>("{id}");
```

### Control Saving Behavior

```csharp
// Update but don't auto-save (useful for batch operations)
app.MapPost("/orders/{id}/items", async (
    [EventStream] Order order,
    [FromBody] AddItemCommand command) =>
{
    order.AddItem(command.ProductId, command.Quantity, command.Price);
    return Results.Ok();
})
.WithProjectionOutput<OrderSummaryProjection>(null, saveAfterUpdate: false);
```

---

## Manual Binding (Without Attributes)

For scenarios where attribute-based binding isn't sufficient, use the helper methods directly:

```csharp
app.MapPost("/orders/batch", async (HttpContext context, [FromBody] BatchOrderCommand command) =>
{
    var results = new List<string>();

    foreach (var orderId in command.OrderIds)
    {
        // Manually bind aggregate
        var order = await EventStoreEndpointExtensions.BindEventStreamAsync<Order>(
            context,
            routeParameterName: "id",  // Not used here since we have the ID directly
            createIfNotExists: false);

        // Or bind by specific ID
        var orderById = await EventStoreEndpointExtensions.BindEventStreamByIdAsync<Order>(
            context,
            objectId: orderId,
            createIfNotExists: false);

        orderById.Process();
        results.Add(orderById.Id);
    }

    return Results.Ok(results);
});

app.MapGet("/projections/{type}", async (HttpContext context, string type) =>
{
    // Manually bind projection with dynamic blob name
    var projection = await EventStoreEndpointExtensions.BindProjectionAsync<CustomerListProjection>(
        context,
        blobNamePattern: type,
        createIfNotExists: true);

    return Results.Ok(projection);
});
```

---

## Complete Example: Order Management API

```csharp
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.AspNetCore.MinimalApis;
using ErikLieben.FA.ES.AspNetCore.MinimalApis.Extensions;
using ErikLieben.FA.ES.AzureStorage;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure Storage
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(
        builder.Configuration.GetConnectionString("Store")!).WithName("Store");
});

// Configure Event Store
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store", autoCreateContainer: true));
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));
builder.Services.ConfigureOrderDomainFactory(); // Generated

// Add Minimal API bindings
builder.Services.AddEventStoreMinimalApis();

var app = builder.Build();

// ============================================
// Order Endpoints
// ============================================

// Create a new order
app.MapPost("/orders", async (
    [EventStream(CreateIfNotExists = true)] Order order,
    [FromBody] CreateOrderRequest request) =>
{
    order.Create(request.CustomerId, request.ShippingAddress);
    return Results.Created($"/orders/{order.Id}", new { order.Id });
})
.WithProjectionOutput<ActiveOrdersProjection>()
.WithName("CreateOrder");

// Get order details
app.MapGet("/orders/{id}", async ([EventStream] Order order) =>
{
    return Results.Ok(new OrderResponse
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        Status = order.Status,
        Items = order.Items,
        TotalAmount = order.TotalAmount
    });
})
.WithName("GetOrder");

// Add item to order
app.MapPost("/orders/{id}/items", async (
    [EventStream] Order order,
    [FromBody] AddItemRequest request) =>
{
    if (order.Status != OrderStatus.Draft)
        return Results.BadRequest("Cannot add items to a non-draft order");

    order.AddItem(request.ProductId, request.ProductName, request.Quantity, request.Price);
    return Results.Ok();
})
.WithProjectionOutput<OrderSummaryProjection>("{id}")
.WithName("AddOrderItem");

// Submit order
app.MapPost("/orders/{id}/submit", async ([EventStream] Order order) =>
{
    if (order.Status != OrderStatus.Draft)
        return Results.BadRequest("Order is not in draft status");

    if (!order.Items.Any())
        return Results.BadRequest("Cannot submit an empty order");

    order.Submit();
    return Results.Ok();
})
.WithProjectionOutput<OrderSummaryProjection>("{id}")
.AndUpdateProjectionToLatest<ActiveOrdersProjection>()
.WithName("SubmitOrder");

// Ship order
app.MapPost("/orders/{id}/ship", async (
    [EventStream] Order order,
    [FromBody] ShipOrderRequest request) =>
{
    if (order.Status != OrderStatus.Submitted)
        return Results.BadRequest("Order must be submitted before shipping");

    order.Ship(request.CarrierName, request.TrackingNumber);
    return Results.Ok();
})
.WithProjectionOutput<OrderSummaryProjection>("{id}")
.AndUpdateProjectionToLatest<ActiveOrdersProjection>()
.WithName("ShipOrder");

// ============================================
// Projection Endpoints (Read Models)
// ============================================

// Get order summary (routed projection)
app.MapGet("/orders/{id}/summary", async (
    [Projection("{id}")] OrderSummaryProjection summary) =>
{
    return Results.Ok(summary);
})
.WithName("GetOrderSummary");

// Get all active orders
app.MapGet("/orders/active", async ([Projection] ActiveOrdersProjection activeOrders) =>
{
    return Results.Ok(activeOrders.Orders);
})
.WithName("GetActiveOrders");

// Get dashboard stats
app.MapGet("/dashboard", async ([Projection] DashboardProjection dashboard) =>
{
    return Results.Ok(new
    {
        dashboard.TotalOrders,
        dashboard.TotalRevenue,
        dashboard.OrdersByStatus,
        dashboard.RecentOrders
    });
})
.WithName("GetDashboard");

// ============================================
// Customer Endpoints
// ============================================

app.MapPost("/customers", async (
    [EventStream(CreateIfNotExists = true)] Customer customer,
    [FromBody] RegisterCustomerRequest request) =>
{
    customer.Register(request.Name, request.Email);
    return Results.Created($"/customers/{customer.Id}", new { customer.Id });
})
.AndUpdateProjectionToLatest<CustomerListProjection>()
.WithName("RegisterCustomer");

app.MapGet("/customers", async ([Projection] CustomerListProjection customers) =>
{
    return Results.Ok(customers.Customers);
})
.WithName("GetCustomers");

app.Run();

// ============================================
// Request/Response DTOs
// ============================================

public record CreateOrderRequest(string CustomerId, string ShippingAddress);
public record AddItemRequest(string ProductId, string ProductName, int Quantity, decimal Price);
public record ShipOrderRequest(string CarrierName, string TrackingNumber);
public record RegisterCustomerRequest(string Name, string Email);

public class OrderResponse
{
    public required string Id { get; init; }
    public required string CustomerId { get; init; }
    public required string Status { get; init; }
    public required List<OrderItem> Items { get; init; }
    public required decimal TotalAmount { get; init; }
}
```

---

## Comparison: With Bindings vs Without

### Without Minimal API Bindings (Manual)

```csharp
app.MapPost("/orders/{id}/items", async (
    string id,
    [FromBody] AddItemCommand command,
    IAggregateFactory aggregateFactory,
    IObjectDocumentFactory documentFactory,
    IEventStreamFactory streamFactory) =>
{
    // Manual: Get factory
    var factory = aggregateFactory.GetFactory(typeof(Order));
    if (factory == null)
        return Results.Problem("Order factory not registered");

    // Manual: Load document
    var document = await documentFactory.GetAsync("Order", id);

    // Manual: Create event stream
    var eventStream = streamFactory.Create(document);

    // Manual: Create aggregate and fold
    var order = (Order)factory.Create(eventStream);
    await order.Fold();

    // Business logic
    order.AddItem(command.ProductId, command.Quantity, command.Price);

    return Results.Ok();
});
```

### With Minimal API Bindings

```csharp
app.MapPost("/orders/{id}/items", async (
    [EventStream] Order order,
    [FromBody] AddItemCommand command) =>
{
    order.AddItem(command.ProductId, command.Quantity, command.Price);
    return Results.Ok();
});
```

The binding handles all the infrastructure concerns, letting you focus on business logic.

---

## Error Handling

The package throws specific exceptions that you can handle:

```csharp
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        var (statusCode, message) = exception switch
        {
            AggregateNotFoundException ex => (404, $"Aggregate not found: {ex.ObjectId}"),
            ProjectionNotFoundException ex => (404, $"Projection not found: {ex.BlobName}"),
            BindingException ex => (400, $"Binding failed: {ex.Message}"),
            _ => (500, "An unexpected error occurred")
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = message });
    });
});
```

### Exception Types

| Exception | When Thrown |
|-----------|-------------|
| `AggregateNotFoundException` | Aggregate doesn't exist and `CreateIfNotExists = false` |
| `ProjectionNotFoundException` | Projection doesn't exist and `CreateIfNotExists = false` |
| `BindingException` | Route parameter missing or binding configuration error |

---

## Integration with Azure Functions

If you're also using Azure Functions in the same solution, note that the Minimal APIs package uses similar patterns but different implementation:

| Feature | Azure Functions | Minimal APIs |
|---------|-----------------|--------------|
| EventStream binding | `[EventStreamInput]` | `[EventStream]` |
| Projection binding | `[ProjectionInput]` | `[Projection]` |
| Projection output | `[ProjectionOutput<T>]` attribute | `.WithProjectionOutput<T>()` fluent API |
| Configuration | `.ConfigureEventStoreBindings()` | `.AddEventStoreMinimalApis()` |

Both packages work with the same underlying `IAggregateFactory`, `IProjectionFactory<T>`, and storage providers.
