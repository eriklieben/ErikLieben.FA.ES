# Version Tokens

Version tokens are compact identifiers that uniquely reference a specific version within an event stream. They enable optimistic concurrency, checkpoint tracking, and cross-system event correlation.

## Overview

A version token encodes:

- **Object Name** - The aggregate type (e.g., "order")
- **Object ID** - The unique identifier (e.g., "order-123")
- **Stream Identifier** - The event stream identifier
- **Version** - The zero-based event version number

Format: `{ObjectName}__{ObjectId}__{StreamIdentifier}__{VersionString}`

Example: `order__order-123__order__00000000000000000042`

## Creating Version Tokens

### From Event and Document

```csharp
// Created automatically when appending events
var versionToken = new VersionToken(@event, document);
```

### From Parts

```csharp
// From explicit values
var token = new VersionToken(
    objectName: "order",
    objectId: "order-123",
    streamIdentifier: "order",
    version: 42);

// From string
var token = new VersionToken("order__order-123__order__00000000000000000042");

// From identifier parts
var objectId = new ObjectIdentifier("order", "order-123");
var versionId = new VersionIdentifier("order", 42);
var token = new VersionToken(objectId, versionId);
```

## Token Properties

| Property | Type | Description |
|----------|------|-------------|
| `Value` | string | Full token string |
| `ObjectName` | string | Aggregate type name |
| `ObjectId` | string | Unique object identifier |
| `StreamIdentifier` | string | Event stream identifier |
| `Version` | int | Zero-based version number |
| `VersionString` | string | Zero-padded version (20 chars) |
| `ObjectIdentifier` | ObjectIdentifier | Strongly typed object ID |
| `VersionIdentifier` | VersionIdentifier | Strongly typed version ID |

## Use Cases

### 1. Optimistic Concurrency

Use version tokens to detect concurrent modifications:

```csharp
public async Task UpdateOrder(string orderId, VersionToken expectedToken, UpdateRequest request)
{
    var order = await orderFactory.GetAsync(orderId);

    // Check version hasn't changed
    var currentToken = order.Stream.GetCurrentVersionToken();
    if (currentToken.Version != expectedToken.Version)
    {
        throw new ConcurrencyException(
            $"Order modified. Expected version {expectedToken.Version}, current is {currentToken.Version}");
    }

    await order.Update(request.Field, request.Value);
}
```

### 2. Projection Checkpoints

Track which events have been processed:

```csharp
// Store checkpoint in projection
projection.Checkpoint[versionToken.ObjectIdentifier] = versionToken.VersionIdentifier;

// Check if event already processed
var key = versionToken.ObjectIdentifier;
if (projection.Checkpoint.TryGetValue(key, out var lastProcessed))
{
    if (lastProcessed.Version >= versionToken.Version)
    {
        return; // Already processed
    }
}
```

### 3. Event Correlation

Track which user/system triggered events:

```csharp
public async Task ShipOrder(string orderId, VersionToken userToken)
{
    var order = await orderFactory.GetAsync(orderId);

    await order.Stream.Session(context =>
        order.Fold(context.Append(
            new OrderShipped(DateTime.UtcNow),
            new ActionMetadata
            {
                EventOccuredAt = DateTime.UtcNow,
                OriginatedFromUser = userToken  // Track who triggered this
            })));
}
```

### 4. API Responses

Return version tokens for clients to use in subsequent requests:

```csharp
[HttpPost("orders/{id}/items")]
public async Task<IActionResult> AddItem(
    string id,
    [FromBody] AddItemRequest request,
    [FromHeader(Name = "If-Match")] string? etag)
{
    var order = await orderFactory.GetAsync(id);

    // Validate ETag if provided
    if (!string.IsNullOrEmpty(etag))
    {
        var expectedToken = new VersionToken(etag);
        if (order.Metadata.Version != expectedToken.Version)
        {
            return StatusCode(412, "Precondition Failed");
        }
    }

    await order.AddItem(request.ProductId, request.Quantity);

    // Return new version in ETag header
    var newToken = order.Stream.GetCurrentVersionToken();
    Response.Headers.ETag = newToken.Value;

    return Ok(new { versionToken = newToken.Value });
}
```

## Updating to Latest Version

Mark a token to indicate processing should continue to the latest version:

```csharp
var token = existingToken.ToLatestVersion();

// Now token.TryUpdateToLatestVersion == true
await projection.Fold(@event, token);
```

## Version String Format

Versions are stored as 20-character zero-padded strings:

```csharp
// Conversion
var versionString = VersionToken.ToVersionTokenString(42);
// Result: "00000000000000000042"

// This ensures proper lexicographic sorting in storage
```

## Object and Version Identifiers

### ObjectIdentifier

Identifies an object across the system:

```csharp
var objectId = new ObjectIdentifier("order", "order-123");
// Value: "order__order-123"
```

### VersionIdentifier

Identifies a specific version:

```csharp
var versionId = new VersionIdentifier("order", 42);
// Value: "order__00000000000000000042"
```

## JSON Serialization

Version tokens serialize to their string value:

```csharp
// Serialized as string
{
    "versionToken": "order__order-123__order__00000000000000000042"
}

// Deserialized back to VersionToken
var token = JsonSerializer.Deserialize<VersionToken>(json);
```

## Metadata Usage

Pass version tokens in action metadata:

```csharp
await Stream.Session(context =>
    Fold(context.Append(
        new OrderCreated(customerId),
        new ActionMetadata
        {
            EventOccuredAt = DateTime.UtcNow,
            OriginatedFromUser = userVersionToken,    // User context
            CorrelationId = correlationToken.Value    // Request correlation
        })));
```

## Concurrency Patterns

### Last-Write-Wins

```csharp
// Don't check version - last write wins
await order.Update(field, value);
```

### Optimistic Concurrency

```csharp
// Client sends expected version
public async Task<IActionResult> Update(
    [FromHeader(Name = "If-Match")] string expectedVersion,
    UpdateRequest request)
{
    var token = new VersionToken(expectedVersion);
    // Validate and update...
}
```

### Pessimistic Locking

```csharp
// Use constraints for existence checks
await Stream.Session(context => ..., Constraint.Existing);  // Must exist
await Stream.Session(context => ..., Constraint.New);       // Must not exist
```

## Best Practices

### Do

- Return version tokens in API responses
- Use version tokens for optimistic concurrency
- Store checkpoint tokens in projections
- Include user tokens in action metadata
- Validate tokens before critical operations

### Don't

- Don't rely on version tokens alone for security
- Don't expose internal stream identifiers unnecessarily
- Don't assume versions are sequential (gaps may exist after migrations)
- Don't compare tokens from different streams

## See Also

- [Aggregates](Aggregates.md) - Aggregate patterns
- [Projections](Projections.md) - Checkpoint tracking
- [Concurrency](Concurrency.md) - Concurrency handling
- [Testing](Testing.md) - Testing with version tokens
