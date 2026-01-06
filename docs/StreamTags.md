# Stream Tags

Tags provide a way to categorize and query documents and event streams without modifying the events themselves.

## Overview

The tag system supports two types:

- **Document Tags** - Associate tags with object documents
- **Stream Tags** - Associate tags with event streams

Tags enable:

- Categorization of aggregates
- Efficient querying by category
- Filtering without full scans
- Cross-cutting concerns tracking

## Document Tag Store

### Interface

```csharp
public interface IDocumentTagStore
{
    /// <summary>
    /// Associates the specified tag with the given document.
    /// </summary>
    Task SetAsync(IObjectDocument document, string tag);

    /// <summary>
    /// Gets the identifiers of documents that have the specified tag.
    /// </summary>
    Task<IEnumerable<string>> GetAsync(string objectName, string tag);

    /// <summary>
    /// Removes the specified tag from the given document.
    /// </summary>
    Task RemoveAsync(IObjectDocument document, string tag);
}
```

### Usage

```csharp
public class OrderService
{
    private readonly IDocumentTagStore _tagStore;
    private readonly IAggregateFactory<Order> _orderFactory;

    public async Task MarkOrderPriority(string orderId)
    {
        var order = await _orderFactory.GetAsync(orderId);

        // Add tag to the document
        await _tagStore.SetAsync(order.Stream.Document, "priority");
    }

    public async Task<IEnumerable<string>> GetPriorityOrders()
    {
        // Query all orders with the "priority" tag
        return await _tagStore.GetAsync("order", "priority");
    }

    public async Task ClearPriority(string orderId)
    {
        var order = await _orderFactory.GetAsync(orderId);

        // Remove tag from document
        await _tagStore.RemoveAsync(order.Stream.Document, "priority");
    }
}
```

## Tag Store Implementations

### Blob Storage

```csharp
// Automatic registration with ConfigureBlobEventStore
services.ConfigureBlobEventStore(new EventStreamBlobSettings(
    defaultDataStore: "Store",
    defaultDocumentTagStore: "Store"  // Can use separate connection
));
```

### Table Storage

```csharp
services.ConfigureTableEventStore(new EventStreamTableSettings(
    defaultDataStore: "Store",
    defaultDocumentTagStore: "Store",
    defaultDocumentTagTableName: "documenttags",
    defaultStreamTagTableName: "streamtags"
));
```

### Cosmos DB

```csharp
services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings
{
    TagsContainerName = "tags"
});
```

### In-Memory (Testing)

```csharp
var context = TestSetup.GetContext();
var tagStore = context.GetService<IDocumentTagStore>();
```

## Common Patterns

### Status-Based Tagging

```csharp
[Aggregate]
public partial class Order : Aggregate
{
    private readonly IDocumentTagStore _tagStore;

    public Order(IEventStream stream, IDocumentTagStore tagStore) : base(stream)
    {
        _tagStore = tagStore;
    }

    public async Task Ship(string trackingNumber)
    {
        await Stream.Session(context =>
            Fold(context.Append(new OrderShipped(trackingNumber, DateTime.UtcNow))));

        // Update tags to reflect status
        await _tagStore.RemoveAsync(Stream.Document, "pending");
        await _tagStore.SetAsync(Stream.Document, "shipped");
    }

    public async Task Complete()
    {
        await Stream.Session(context =>
            Fold(context.Append(new OrderCompleted(DateTime.UtcNow))));

        await _tagStore.RemoveAsync(Stream.Document, "shipped");
        await _tagStore.SetAsync(Stream.Document, "completed");
    }
}
```

### Category Tagging

```csharp
public async Task CategorizeOrder(string orderId, string category)
{
    var order = await orderFactory.GetAsync(orderId);
    await _tagStore.SetAsync(order.Stream.Document, $"category:{category}");
}

public async Task<IEnumerable<string>> GetOrdersByCategory(string category)
{
    return await _tagStore.GetAsync("order", $"category:{category}");
}
```

### Multi-Tenant Tagging

```csharp
public async Task TagForTenant(string orderId, string tenantId)
{
    var order = await orderFactory.GetAsync(orderId);
    await _tagStore.SetAsync(order.Stream.Document, $"tenant:{tenantId}");
}

public async Task<IEnumerable<string>> GetTenantOrders(string tenantId)
{
    return await _tagStore.GetAsync("order", $"tenant:{tenantId}");
}
```

### Priority Queue Pattern

```csharp
public async Task SetPriority(string orderId, int priority)
{
    var order = await orderFactory.GetAsync(orderId);

    // Remove existing priority tags
    for (int i = 1; i <= 5; i++)
    {
        await _tagStore.RemoveAsync(order.Stream.Document, $"priority:{i}");
    }

    // Set new priority
    await _tagStore.SetAsync(order.Stream.Document, $"priority:{priority}");
}

public async Task<IEnumerable<string>> GetHighPriorityOrders()
{
    return await _tagStore.GetAsync("order", "priority:1");
}
```

## Stream Information Tags

Stream metadata includes tag store configuration:

```csharp
var streamInfo = order.Stream.Document.Active;

// Tag store settings
var documentTagType = streamInfo.DocumentTagType;      // e.g., "blob"
var streamTagType = streamInfo.EventStreamTagType;    // e.g., "table"
var documentTagStore = streamInfo.DocumentTagStore;    // Connection name
var streamTagStore = streamInfo.StreamTagStore;        // Connection name
```

## Tag Naming Conventions

| Pattern | Example | Use Case |
|---------|---------|----------|
| Simple | `"priority"` | Boolean flags |
| Prefixed | `"status:active"` | Enumerated values |
| Namespaced | `"tenant:abc"` | Multi-tenant |
| Hierarchical | `"region:us:west"` | Geographic |
| Compound | `"2024:Q1:sales"` | Time-based |

## Querying with Tags

### Single Tag Query

```csharp
var activeOrders = await _tagStore.GetAsync("order", "status:active");
```

### Multiple Tags (AND)

```csharp
var priorityActive = (await _tagStore.GetAsync("order", "priority:1"))
    .Intersect(await _tagStore.GetAsync("order", "status:active"));
```

### Multiple Tags (OR)

```csharp
var priority1Or2 = (await _tagStore.GetAsync("order", "priority:1"))
    .Union(await _tagStore.GetAsync("order", "priority:2"));
```

## API Integration

```csharp
app.MapGet("/orders/by-tag/{tag}", async (
    string tag,
    IDocumentTagStore tagStore,
    IAggregateFactory<Order> orderFactory) =>
{
    var orderIds = await tagStore.GetAsync("order", tag);
    var orders = new List<OrderSummary>();

    foreach (var id in orderIds)
    {
        var order = await orderFactory.GetAsync(id);
        orders.Add(new OrderSummary(order.Id, order.Status));
    }

    return Results.Ok(orders);
});

app.MapPost("/orders/{id}/tags/{tag}", async (
    string id,
    string tag,
    IDocumentTagStore tagStore,
    IAggregateFactory<Order> orderFactory) =>
{
    var order = await orderFactory.GetAsync(id);
    await tagStore.SetAsync(order.Stream.Document, tag);
    return Results.NoContent();
});

app.MapDelete("/orders/{id}/tags/{tag}", async (
    string id,
    string tag,
    IDocumentTagStore tagStore,
    IAggregateFactory<Order> orderFactory) =>
{
    var order = await orderFactory.GetAsync(id);
    await tagStore.RemoveAsync(order.Stream.Document, tag);
    return Results.NoContent();
});
```

## Best Practices

### Do

- Use consistent naming conventions for tags
- Use prefixes to namespace related tags
- Remove old tags when state changes
- Keep tag names short but descriptive
- Use tags for queryable categories

### Don't

- Don't use tags for unique identifiers (use object IDs)
- Don't store sensitive data in tags
- Don't create too many unique tag values (impacts query performance)
- Don't rely on tags for data integrity
- Don't use tags instead of proper projections for complex queries

## Performance Considerations

| Storage | Set | Get | Remove |
|---------|-----|-----|--------|
| Blob | Moderate | Scan-based | Moderate |
| Table | Fast | Partition query | Fast |
| Cosmos DB | Fast | Point read | Fast |

For high-volume tag queries, consider:

- Using Table or Cosmos DB for tag storage
- Caching frequently accessed tag queries
- Using projections for complex filtering

## See Also

- [Aggregates](Aggregates.md) - Aggregate patterns
- [Configuration](Configuration.md) - Storage configuration
- [Storage Providers](StorageProviders.md) - Provider details
- [Testing](Testing.md) - Testing with tags
