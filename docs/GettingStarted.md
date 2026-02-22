# Getting Started with ErikLieben.FA.ES

This guide walks you through setting up an event-sourced application using the ErikLieben.FA.ES framework.

## Prerequisites

- .NET 9.0 SDK or later
- Azure Storage Emulator (Azurite) or Azure Storage Account
- Visual Studio 2022, VS Code, or Rider

## Installation

### 1. Install the NuGet Packages

```bash
# Core library
dotnet add package ErikLieben.FA.ES

# Storage provider (choose one or more)
dotnet add package ErikLieben.FA.ES.AzureStorage    # Blob & Table Storage
dotnet add package ErikLieben.FA.ES.CosmosDb        # Cosmos DB

# Optional: ASP.NET Core integration
dotnet add package ErikLieben.FA.ES.AspNetCore.MinimalApis

# Optional: Azure Functions integration
dotnet add package ErikLieben.FA.ES.Azure.Functions.Worker.Extensions
```

### 2. Install the CLI Tool

The CLI tool generates boilerplate code for your aggregates and projections:

```bash
dotnet tool install --global ErikLieben.FA.ES.CLI
```

Verify installation:

```bash
dotnet faes --version
```

## Quick Start: Your First Aggregate

### Step 1: Create an Event

Events describe things that happened in your domain. They are immutable records:

```csharp
// Events/Order/OrderCreated.cs
using ErikLieben.FA.ES.Attributes;

namespace MyApp.Events.Order;

[EventName("Order.Created")]
public record OrderCreated(
    string CustomerId,
    DateTime CreatedAt);

[EventName("Order.ItemAdded")]
public record OrderItemAdded(
    string ProductId,
    int Quantity,
    decimal UnitPrice);

[EventName("Order.Submitted")]
public record OrderSubmitted(
    DateTime SubmittedAt);
```

### Step 2: Create an Aggregate

Aggregates are the core domain objects that handle commands and emit events:

```csharp
// Aggregates/Order.cs
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Processors;
using MyApp.Events.Order;

namespace MyApp.Aggregates;

[Aggregate]
public partial class Order : Aggregate
{
    public Order(IEventStream stream) : base(stream) { }

    // State properties (private setters - only modified by When methods)
    public string? CustomerId { get; private set; }
    public List<OrderItem> Items { get; } = new();
    public decimal Total { get; private set; }
    public bool IsSubmitted { get; private set; }

    // Command: Create a new order
    public async Task Create(string customerId)
    {
        await Stream.Session(context =>
            Fold(context.Append(new OrderCreated(customerId, DateTime.UtcNow))));
    }

    // Command: Add an item to the order
    public async Task AddItem(string productId, int quantity, decimal unitPrice)
    {
        if (IsSubmitted)
            throw new InvalidOperationException("Cannot modify a submitted order");

        await Stream.Session(context =>
            Fold(context.Append(new OrderItemAdded(productId, quantity, unitPrice))));
    }

    // Command: Submit the order
    public async Task Submit()
    {
        if (IsSubmitted)
            throw new InvalidOperationException("Order already submitted");

        if (!Items.Any())
            throw new InvalidOperationException("Cannot submit an empty order");

        await Stream.Session(context =>
            Fold(context.Append(new OrderSubmitted(DateTime.UtcNow))));
    }

    // Event handlers - apply events to state
    private void When(OrderCreated @event)
    {
        CustomerId = @event.CustomerId;
    }

    private void When(OrderItemAdded @event)
    {
        Items.Add(new OrderItem(@event.ProductId, @event.Quantity, @event.UnitPrice));
        Total += @event.Quantity * @event.UnitPrice;
    }

    private void When(OrderSubmitted @event)
    {
        IsSubmitted = true;
    }
}

public record OrderItem(string ProductId, int Quantity, decimal UnitPrice);
```

### Step 3: Generate Code

Run the CLI tool to generate the supporting code:

```bash
dotnet faes
```

This generates:
- `Order.Generated.cs` - Fold method, JSON serialization contexts
- `MyAppExtensions.Generated.cs` - DI registration

### Step 4: Configure Services

```csharp
// Program.cs
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.AzureStorage;
using ErikLieben.FA.ES.Builder;
using MyApp;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure Storage
builder.Services.AddAzureClients(clients =>
{
    clients.AddBlobServiceClient(builder.Configuration.GetConnectionString("Storage")!)
        .WithName("Store");
});

// Configure Event Store using fluent builder (recommended)
builder.Services.AddFaes(faes => faes
    .UseDefaultStorage("blob")
    .UseBlobStorage(new EventStreamBlobSettings("Store", autoCreateContainer: true))
);

// Register your domain (generated extension method)
builder.Services.ConfigureMyAppFactory();

var app = builder.Build();
```

> **Note**: The classic configuration approach using `ConfigureBlobEventStore()` and `ConfigureEventStore()` is still supported. See [Configuration](Configuration.md) for details.

### Step 5: Use the Aggregate

```csharp
// In an API endpoint or service
app.MapPost("/orders", async (
    IAggregateFactory<Order> orderFactory,
    CreateOrderRequest request) =>
{
    // Create a new order
    var order = await orderFactory.CreateAsync(Guid.NewGuid().ToString());
    await order.Create(request.CustomerId);

    return Results.Created($"/orders/{order.Id}", new { order.Id });
});

app.MapPost("/orders/{id}/items", async (
    string id,
    IAggregateFactory<Order> orderFactory,
    AddItemRequest request) =>
{
    // Load existing order and add item
    var order = await orderFactory.GetAsync(id);
    await order.AddItem(request.ProductId, request.Quantity, request.UnitPrice);

    return Results.Ok();
});
```

## Quick Start: Your First Projection

Projections are read models that aggregate data from events:

```csharp
// Projections/OrderDashboard.cs
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Projections;
using MyApp.Events.Order;

namespace MyApp.Projections;

[BlobJsonProjection("projections")]
public partial class OrderDashboard : Projection
{
    public int TotalOrders { get; private set; }
    public int SubmittedOrders { get; private set; }
    public decimal TotalRevenue { get; private set; }

    private void When(OrderCreated @event)
    {
        TotalOrders++;
    }

    private void When(OrderSubmitted @event)
    {
        SubmittedOrders++;
    }

    private void When(OrderItemAdded @event)
    {
        TotalRevenue += @event.Quantity * @event.UnitPrice;
    }
}
```

Run `dotnet faes` again to generate the projection code.

## Development Workflow

1. **Create/modify events** - Define what happened in your domain
2. **Create/modify aggregates** - Handle commands and emit events
3. **Create/modify projections** - Build read models from events
4. **Run `dotnet faes`** - Generate supporting code
5. **Build and test** - Verify everything works

For continuous development, use watch mode:

```bash
dotnet faes watch
```

This automatically regenerates code when you save changes.

## Project Structure

Recommended folder structure:

```
MyApp/
├── Aggregates/
│   ├── Order.cs
│   └── Order.Generated.cs          # Generated
├── Events/
│   └── Order/
│       ├── OrderCreated.cs
│       ├── OrderItemAdded.cs
│       └── OrderSubmitted.cs
├── Projections/
│   ├── OrderDashboard.cs
│   └── OrderDashboard.Generated.cs # Generated
├── MyAppExtensions.Generated.cs    # Generated
└── Program.cs
```

## Next Steps

- [Aggregates Deep Dive](Aggregates.md) - Complete aggregate patterns
- [Projections Deep Dive](Projections.md) - Projection types and routing
- [CLI Tool Reference](CLI.md) - All CLI commands and options
- [Testing Guide](Testing.md) - Unit testing event-sourced code
- [Storage Providers](StorageProviders.md) - Configure Blob, Table, or Cosmos DB

## Common Issues

### "Generated file not found" error

Run `dotnet faes` to generate the missing files.

### "Aggregate class must be partial" warning

Add the `partial` keyword to your class declaration:

```csharp
public partial class Order : Aggregate  // Add 'partial'
```

### Events not being applied to state

Make sure you wrap `Append` with `Fold`:

```csharp
// Wrong
await Stream.Session(context =>
    context.Append(new OrderCreated(...)));

// Correct
await Stream.Session(context =>
    Fold(context.Append(new OrderCreated(...))));
```
