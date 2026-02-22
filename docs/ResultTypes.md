# Result Types

The library provides `Result` and `Result<T>` types for explicit error handling without exceptions. These are value types (structs) optimized for performance and AOT compatibility.

## Overview

Result types represent operations that can either succeed or fail:
- `Result` - For operations that don't return a value
- `Result<T>` - For operations that return a value on success

## Basic Usage

### Creating Results

```csharp
// Success
var success = Result.Success();
var successWithValue = Result<Customer>.Success(customer);

// Failure
var failure = Result.Failure(new Error("NOT_FOUND", "Customer not found"));
var failureWithCode = Result<Customer>.Failure("VALIDATION_ERROR", "Invalid email");

// Implicit conversions
Result<Customer> result = customer;  // Implicit success
Result<Customer> error = new Error("ERROR", "Something went wrong");  // Implicit failure
```

### Checking Results

```csharp
var result = await GetCustomerAsync(id);

if (result.IsSuccess)
{
    var customer = result.Value;
    // Use customer...
}
else
{
    var error = result.Error;
    logger.LogError("Failed to get customer: {Code} - {Message}", error.Code, error.Message);
}
```

### Functional Operations

The `Result<T>` type supports functional programming patterns:

```csharp
// Map - Transform the value if successful
Result<CustomerDto> dtoResult = customerResult.Map(c => new CustomerDto(c.Name, c.Email));

// Bind - Chain operations that return Results
Result<Order> orderResult = customerResult.Bind(c => GetLatestOrderAsync(c.Id));

// OnSuccess - Execute side effect on success
customerResult.OnSuccess(c => logger.LogInformation("Found customer: {Name}", c.Name));

// OnFailure - Execute side effect on failure
customerResult.OnFailure(e => logger.LogWarning("Error: {Code}", e.Code));
```

## Error Type

The `Error` record represents an error with a code and message:

```csharp
public sealed record Error(string Code, string Message);
```

### Common Error Factories

The `EventSourcingErrors` class provides factory methods for common errors:

```csharp
// Stream errors
EventSourcingErrors.StreamNotFound("order-123")
// -> Error("STREAM_NOT_FOUND", "Event stream 'order-123' was not found")

EventSourcingErrors.ConcurrencyConflict("order-123", expectedVersion: 5, actualVersion: 7)
// -> Error("CONCURRENCY_CONFLICT", "Concurrency conflict on stream 'order-123'. Expected version 5, but actual version is 7")

// Aggregate errors
EventSourcingErrors.AggregateNotFound("Order", "order-123")
EventSourcingErrors.AggregateAlreadyExists("Order", "order-123")

// Projection errors
EventSourcingErrors.ProjectionNotFound("Dashboard", "main")
EventSourcingErrors.ProjectionSaveFailed("Dashboard", "Connection timeout")

// Storage errors
EventSourcingErrors.StorageOperationFailed("Append", "Container not found")
EventSourcingErrors.SnapshotNotFound("order-123", version: 10)

// General errors
EventSourcingErrors.ValidationFailed("Email is required")
EventSourcingErrors.Timeout(TimeSpan.FromSeconds(30))
EventSourcingErrors.OperationCancelled

// From exceptions
Error.FromException(ex)
// -> Error("EXCEPTION.InvalidOperationException", "The operation is invalid")
```

## Example: Command Handler with Result

```csharp
public async Task<Result> CreateOrderAsync(CreateOrderCommand command)
{
    // Validate
    if (string.IsNullOrEmpty(command.CustomerId))
        return EventSourcingErrors.ValidationFailed("CustomerId is required");

    // Check customer exists
    var customerResult = await _customerFactory.GetAsync(command.CustomerId);
    if (customerResult.IsFailure)
        return customerResult.Error!;

    // Create order
    try
    {
        var order = await _orderFactory.CreateAsync(Guid.NewGuid().ToString());
        await order.Create(command.CustomerId);
        return Result.Success();
    }
    catch (Exception ex)
    {
        return Error.FromException(ex);
    }
}
```

## Example: API Endpoint with Result

```csharp
app.MapGet("/customers/{id}", async (string id, ICustomerService service) =>
{
    var result = await service.GetCustomerAsync(id);

    return result.IsSuccess
        ? Results.Ok(result.Value)
        : result.Error!.Code switch
        {
            "NOT_FOUND" => Results.NotFound(result.Error),
            "VALIDATION_FAILED" => Results.BadRequest(result.Error),
            _ => Results.Problem(result.Error!.Message)
        };
});
```

## Built-in Error Codes

| Code | Description |
|------|-------------|
| `UNKNOWN` | An unknown error occurred |
| `NULL_VALUE` | A null value was provided |
| `STREAM_NOT_FOUND` | Event stream does not exist |
| `CONCURRENCY_CONFLICT` | Optimistic concurrency conflict |
| `AGGREGATE_NOT_FOUND` | Aggregate does not exist |
| `AGGREGATE_ALREADY_EXISTS` | Aggregate already exists |
| `EVENT_DESERIALIZATION_FAILED` | Failed to deserialize event |
| `PROJECTION_NOT_FOUND` | Projection does not exist |
| `PROJECTION_SAVE_FAILED` | Failed to save projection |
| `SNAPSHOT_NOT_FOUND` | Snapshot does not exist |
| `STORAGE_OPERATION_FAILED` | Storage operation failed |
| `OPERATION_CANCELLED` | Operation was cancelled |
| `TIMEOUT` | Operation timed out |
| `VALIDATION_FAILED` | Validation failed |
| `EXCEPTION.*` | Error from exception (e.g., `EXCEPTION.ArgumentNullException`) |

## Best Practices

1. **Use Result for operations that can fail predictably** - Network calls, validation, lookups
2. **Use exceptions for truly exceptional cases** - Programming errors, configuration issues
3. **Use descriptive error codes** - Enables programmatic error handling
4. **Log failures at the boundary** - Don't log inside Result-returning methods
5. **Chain operations with Map/Bind** - Avoid nested if statements

## See Also

- [Testing](Testing.md) - Testing with Result types
- [Concurrency](Concurrency.md) - Handling concurrency conflicts
