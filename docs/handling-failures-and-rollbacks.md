# Handling Failures and Rollbacks in Event Sourcing

## Table of Contents
- [Introduction](#introduction)
- [The Event Sourcing Philosophy](#the-event-sourcing-philosophy)
- [Strategies for Handling Failures](#strategies-for-handling-failures)
  - [1. Compensating Events](#1-compensating-events)
  - [2. Constraints and Optimistic Concurrency](#2-constraints-and-optimistic-concurrency)
  - [3. Action Pipeline for Validation](#3-action-pipeline-for-validation)
  - [4. Stream Termination](#4-stream-termination)
  - [5. Sagas and Process Managers](#5-sagas-and-process-managers)
- [Practical Examples](#practical-examples)
- [Best Practices](#best-practices)
- [Common Patterns](#common-patterns)
- [Troubleshooting](#troubleshooting)

---

## Introduction

In traditional CRUD systems, rolling back a failed operation is straightforward: you simply undo the database changes using a transaction rollback. However, in event sourcing, **events are immutable facts that have occurred**. You cannot delete or modify historical events—instead, you must model the reversal of business logic as new events.

This document explains how to handle failures, compensate for errors, and maintain consistency in the ErikLieben.FA.ES event sourcing framework.

---

## The Event Sourcing Philosophy

### Core Principles

1. **Events are immutable**: Once an event is committed to the stream, it represents a fact that occurred and cannot be changed.
2. **Append-only**: The event stream is append-only; we never delete or modify historical events.
3. **Compensating actions**: To "undo" an event, we append a new compensating event that reverses its effect.
4. **Temporal modeling**: The event stream preserves the full history, including mistakes and their corrections.

### Why We Don't Delete Events

```
❌ BAD: Deleting an event
[CustomerRegistered] → [Deleted]
(We lose history - what happened? when?)

✅ GOOD: Compensating event
[CustomerRegistered] → [CustomerRegistrationCancelled]
(Full audit trail preserved)
```

---

## Strategies for Handling Failures

### 1. Compensating Events

Compensating events are the primary mechanism for "undoing" the effect of previous events.

#### Example: Order Cancellation

```csharp
// Original events
[EventName("Order.Placed")]
public record OrderPlaced(
    string OrderId,
    string CustomerId,
    decimal TotalAmount,
    List<OrderLine> Items
);

[EventName("Payment.Captured")]
public record PaymentCaptured(
    string PaymentId,
    decimal Amount
);

// Compensating events
[EventName("Order.Cancelled")]
public record OrderCancelled(
    string OrderId,
    string Reason,
    DateTimeOffset CancelledAt
);

[EventName("Payment.Refunded")]
public record PaymentRefunded(
    string PaymentId,
    decimal Amount,
    string Reason
);
```

#### Aggregate Implementation

```csharp
public partial class Order : Aggregate
{
    private OrderStatus _status = OrderStatus.New;
    private decimal _totalAmount;
    private string? _paymentId;

    // Handle the original event
    private void When(OrderPlaced @event)
    {
        _status = OrderStatus.Placed;
        _totalAmount = @event.TotalAmount;
    }

    private void When(PaymentCaptured @event)
    {
        _status = OrderStatus.Paid;
        _paymentId = @event.PaymentId;
    }

    // Handle the compensating events
    private void When(OrderCancelled @event)
    {
        _status = OrderStatus.Cancelled;
    }

    private void When(PaymentRefunded @event)
    {
        _status = OrderStatus.Refunded;
        _paymentId = null;
    }

    // Business logic to cancel order
    public async Task CancelOrder(string reason)
    {
        if (_status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Order is already cancelled");

        if (_status == OrderStatus.Shipped)
            throw new InvalidOperationException("Cannot cancel shipped order");

        await Stream.Session(context =>
        {
            // Append compensating events
            context.Append(new OrderCancelled(
                Stream.Document.ObjectId,
                reason,
                DateTimeOffset.UtcNow
            ));

            // If payment was captured, refund it
            if (_paymentId != null)
            {
                context.Append(new PaymentRefunded(
                    _paymentId,
                    _totalAmount,
                    $"Order cancelled: {reason}"
                ));
            }

            return Fold(context);
        });
    }
}
```

#### Key Points

- The `OrderCancelled` event compensates for `OrderPlaced`
- The `PaymentRefunded` event compensates for `PaymentCaptured`
- The aggregate's state is updated by folding these compensating events
- The full history is preserved: we can see when the order was placed and when it was cancelled

---

### 2. Constraints and Optimistic Concurrency

Use constraints to prevent concurrent modification conflicts and ensure data integrity.

#### Constraint Types

```csharp
public enum Constraint
{
    Loose = 0,      // No constraint - works with new or existing streams
    New = 1,        // Can only append to NEW streams (prevents duplicates)
    Existing = 2    // Can only append to EXISTING streams (prevents phantom writes)
}
```

#### Example: Preventing Duplicate Registration

```csharp
public partial class Customer : Aggregate
{
    public async Task RegisterCustomer(string customerName, string email)
    {
        await Stream.Session(context =>
        {
            context.Append(new CustomerRegistered(customerName, email));
            return Fold(context);
        }, constraint: Constraint.New);  // ← Ensures this is a NEW customer
    }
}
```

**What happens:**
- If stream already exists → `ConstraintException` is thrown
- Application can catch this and handle accordingly (e.g., "Customer already registered")
- No events are committed on failure

#### Optimistic Concurrency with Hash

```csharp
public async Task UpdateCustomerDetails(string newAddress)
{
    await Stream.Session(async context =>
    {
        // Read current events to get latest hash
        var events = await context.ReadAsync();

        // Stream tracks hash for optimistic concurrency
        context.Append(new CustomerAddressUpdated(newAddress));
        return await Fold(context);
    }, constraint: Constraint.Existing);
}
```

The `ObjectDocument.Hash` and `ObjectDocument.PrevHash` are used internally to detect concurrent modifications. If another process commits between your read and write, the commit will fail.

---

### 3. Action Pipeline for Validation

Use pre-append actions to validate state before committing events.

#### Registering a Pre-Append Action

```csharp
public class ValidateOrderLimitsAction : IPreAppendAction
{
    public Func<T> PreAppend<T>(
        T data,
        JsonEvent @event,
        IObjectDocument objectDocument) where T : class
    {
        return () =>
        {
            // Example: Validate order limits
            if (@event.EventType == "Order.Placed" && data is OrderPlaced order)
            {
                if (order.TotalAmount > 100000m)
                {
                    throw new InvalidOperationException(
                        $"Order amount {order.TotalAmount} exceeds maximum limit of 100000");
                }
            }

            return data;
        };
    }
}

// Register the action
eventStream.RegisterAction(new ValidateOrderLimitsAction());
```

**Flow:**
1. `context.Append(new OrderPlaced(...))` is called
2. Pre-append action validates the order
3. If validation fails, exception is thrown **before** event is written
4. No event is committed; the stream remains unchanged

#### Post-Append Actions for Side Effects

```csharp
public class SendOrderConfirmationAction : IAsyncPostCommitAction
{
    private readonly IEmailService _emailService;

    public SendOrderConfirmationAction(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task PostCommitAsync(
        IEnumerable<JsonEvent> events,
        IObjectDocument document)
    {
        foreach (var evt in events)
        {
            if (evt.EventType == "Order.Placed")
            {
                var orderPlaced = evt.GetPayload<OrderPlaced>();

                try
                {
                    await _emailService.SendConfirmation(orderPlaced.CustomerId);
                }
                catch (Exception ex)
                {
                    // Option 1: Log and continue
                    // Option 2: Append a compensating event
                    // We cannot rollback the event - it's already committed!

                    // Best practice: Append a failure event for later retry
                    await AppendFailureEvent(document, evt, ex);
                }
            }
        }
    }

    private async Task AppendFailureEvent(
        IObjectDocument document,
        JsonEvent originalEvent,
        Exception error)
    {
        // Use a separate stream or the same stream to record the failure
        // This creates an audit trail and enables retry mechanisms

        var eventStream = GetEventStream(document);
        await eventStream.Session(context =>
        {
            context.Append(new EmailDeliveryFailed(
                originalEvent.EventVersion,
                originalEvent.EventType,
                error.Message
            ));
            return Task.CompletedTask;
        });
    }
}
```

**Key Insight:** Post-commit actions run AFTER events are successfully written. If a post-commit action fails (e.g., email service is down), you cannot rollback the event. Instead:

1. **Log the failure** for monitoring
2. **Append a failure event** to create an audit trail
3. **Use a retry mechanism** or process manager to retry later

---

### 4. Stream Termination

When a stream becomes corrupted or needs to be superseded, use stream termination.

#### Terminating a Stream

```csharp
public record TerminatedStream
{
    public string? StreamIdentifier { get; set; }
    public string? StreamType { get; set; }
    public string? StreamConnectionName { get; set; }
    public string? Reason { get; set; }
    public string? ContinuationStreamId { get; set; }  // ← New stream to use
    public DateTimeOffset TerminationDate { get; set; }
    public int? StreamVersion { get; set; }
    public bool Deleted { get; set; }
    public DateTimeOffset DeletionDate { get; set; }
}
```

#### Use Cases

1. **Stream Rollover**: Long-running streams (millions of events) can be closed and continued in a new stream
2. **Data Corruption**: If a stream is corrupted, terminate it and create a new one
3. **Schema Migration**: Terminate old stream and replay events with upcasters into new stream
4. **Compliance**: Mark streams for deletion after retention period

#### Example

```csharp
// In ObjectDocument or custom management code
var terminatedStream = new TerminatedStream
{
    StreamIdentifier = oldStreamId,
    StreamType = "Customer",
    Reason = "Stream rollover - exceeded 10M events",
    ContinuationStreamId = newStreamId,
    TerminationDate = DateTimeOffset.UtcNow,
    StreamVersion = currentVersion
};

document.TerminatedStreams.Add(terminatedStream);
document.Active = new StreamInformation
{
    StreamIdentifier = newStreamId,
    // ... other properties
};
```

**Important:** Version tokens still reference the original stream identifier, ensuring audit trail integrity.

---

### 5. Sagas and Process Managers

For distributed transactions across multiple aggregates, use sagas or process managers.

#### Problem: Multi-Aggregate Transaction

```
Order aggregate: [OrderPlaced]
   ↓
Inventory aggregate: [InventoryReserved]
   ↓
Payment aggregate: [PaymentCaptured]
   ↓
   ❌ Payment fails - how do we rollback inventory reservation?
```

#### Solution: Process Manager Pattern

```csharp
[EventName("OrderProcess.Started")]
public record OrderProcessStarted(string OrderId, string CustomerId);

[EventName("OrderProcess.InventoryReserved")]
public record OrderProcessInventoryReserved(string OrderId);

[EventName("OrderProcess.PaymentCaptured")]
public record OrderProcessPaymentCaptured(string OrderId);

[EventName("OrderProcess.Failed")]
public record OrderProcessFailed(string OrderId, string Reason, string FailedStep);

[EventName("OrderProcess.Compensating")]
public record OrderProcessCompensating(string OrderId, string Step);

[EventName("OrderProcess.Completed")]
public record OrderProcessCompleted(string OrderId);

public partial class OrderProcess : Aggregate
{
    private OrderProcessState _state = OrderProcessState.New;
    private string? _orderId;
    private bool _inventoryReserved;
    private bool _paymentCaptured;

    private void When(OrderProcessStarted @event)
    {
        _orderId = @event.OrderId;
        _state = OrderProcessState.Started;
    }

    private void When(OrderProcessInventoryReserved @event)
    {
        _inventoryReserved = true;
    }

    private void When(OrderProcessPaymentCaptured @event)
    {
        _paymentCaptured = true;
    }

    private void When(OrderProcessFailed @event)
    {
        _state = OrderProcessState.Failed;
    }

    private void When(OrderProcessCompensating @event)
    {
        _state = OrderProcessState.Compensating;
    }

    private void When(OrderProcessCompleted @event)
    {
        _state = OrderProcessState.Completed;
    }

    public async Task ExecuteOrderProcess(
        string orderId,
        IInventoryService inventoryService,
        IPaymentService paymentService)
    {
        try
        {
            await Stream.Session(context =>
            {
                context.Append(new OrderProcessStarted(orderId, "customer123"));
                return Fold(context);
            });

            // Step 1: Reserve inventory
            var inventoryReserved = await inventoryService.ReserveInventory(orderId);
            if (!inventoryReserved)
            {
                await RecordFailure("Inventory reservation failed", "InventoryReservation");
                return;
            }

            await Stream.Session(context =>
            {
                context.Append(new OrderProcessInventoryReserved(orderId));
                return Fold(context);
            });

            // Step 2: Capture payment
            var paymentCaptured = await paymentService.CapturePayment(orderId);
            if (!paymentCaptured)
            {
                await RecordFailure("Payment capture failed", "PaymentCapture");
                await CompensateInventory(inventoryService, orderId);
                return;
            }

            await Stream.Session(context =>
            {
                context.Append(new OrderProcessPaymentCaptured(orderId));
                context.Append(new OrderProcessCompleted(orderId));
                return Fold(context);
            });
        }
        catch (Exception ex)
        {
            await RecordFailure(ex.Message, "Unknown");
            await CompensateAll(inventoryService, paymentService, orderId);
        }
    }

    private async Task RecordFailure(string reason, string failedStep)
    {
        await Stream.Session(context =>
        {
            context.Append(new OrderProcessFailed(_orderId!, reason, failedStep));
            return Fold(context);
        });
    }

    private async Task CompensateInventory(IInventoryService inventoryService, string orderId)
    {
        await Stream.Session(context =>
        {
            context.Append(new OrderProcessCompensating(orderId, "InventoryReservation"));
            return Fold(context);
        });

        await inventoryService.ReleaseInventory(orderId);
    }

    private async Task CompensateAll(
        IInventoryService inventoryService,
        IPaymentService paymentService,
        string orderId)
    {
        if (_paymentCaptured)
        {
            await Stream.Session(context =>
            {
                context.Append(new OrderProcessCompensating(orderId, "PaymentCapture"));
                return Fold(context);
            });
            await paymentService.RefundPayment(orderId);
        }

        if (_inventoryReserved)
        {
            await Stream.Session(context =>
            {
                context.Append(new OrderProcessCompensating(orderId, "InventoryReservation"));
                return Fold(context);
            });
            await inventoryService.ReleaseInventory(orderId);
        }
    }
}
```

#### Key Benefits

- Each step is recorded as an event
- Failures are explicitly modeled
- Compensation logic is clear and auditable
- Can resume from any point by folding the process events
- Full audit trail of the distributed transaction

---

## Practical Examples

### Example 1: Bank Transfer with Compensation

```csharp
// Events
[EventName("Account.MoneyWithdrawn")]
public record MoneyWithdrawn(string AccountId, decimal Amount, string TransferId);

[EventName("Account.MoneyDeposited")]
public record MoneyDeposited(string AccountId, decimal Amount, string TransferId);

[EventName("Transfer.Failed")]
public record TransferFailed(string TransferId, string Reason);

[EventName("Account.WithdrawalReversed")]
public record WithdrawalReversed(string AccountId, decimal Amount, string TransferId);

// Process Manager
public async Task ExecuteTransfer(
    string fromAccountId,
    string toAccountId,
    decimal amount)
{
    var transferId = Guid.NewGuid().ToString();

    try
    {
        // Step 1: Withdraw from source account
        await WithdrawFromAccount(fromAccountId, amount, transferId);

        // Step 2: Deposit to destination account
        try
        {
            await DepositToAccount(toAccountId, amount, transferId);
        }
        catch (Exception ex)
        {
            // Compensation: Reverse the withdrawal
            await ReverseWithdrawal(fromAccountId, amount, transferId);
            await RecordTransferFailure(transferId, ex.Message);
            throw;
        }
    }
    catch (Exception ex)
    {
        await RecordTransferFailure(transferId, ex.Message);
        throw;
    }
}

private async Task ReverseWithdrawal(string accountId, decimal amount, string transferId)
{
    var accountStream = GetAccountStream(accountId);
    await accountStream.Session(context =>
    {
        context.Append(new WithdrawalReversed(accountId, amount, transferId));
        return Task.CompletedTask;
    });
}
```

---

### Example 2: Idempotent Command Handling

```csharp
[EventName("Order.PlacementAttempted")]
public record OrderPlacementAttempted(
    string OrderId,
    string IdempotencyKey,
    DateTimeOffset AttemptedAt
);

[EventName("Order.Placed")]
public record OrderPlaced(
    string OrderId,
    string IdempotencyKey,
    /* ... other fields ... */
);

public partial class Order : Aggregate
{
    private HashSet<string> _processedIdempotencyKeys = new();
    private OrderStatus _status;

    private void When(OrderPlacementAttempted @event)
    {
        _processedIdempotencyKeys.Add(@event.IdempotencyKey);
    }

    private void When(OrderPlaced @event)
    {
        _status = OrderStatus.Placed;
        _processedIdempotencyKeys.Add(@event.IdempotencyKey);
    }

    public async Task PlaceOrder(string idempotencyKey, /* ... params ... */)
    {
        // Check if we've already processed this command
        if (_processedIdempotencyKeys.Contains(idempotencyKey))
        {
            // Already processed - return success without appending events
            return;
        }

        await Stream.Session(context =>
        {
            context.Append(new OrderPlacementAttempted(
                Stream.Document.ObjectId,
                idempotencyKey,
                DateTimeOffset.UtcNow
            ));

            context.Append(new OrderPlaced(
                Stream.Document.ObjectId,
                idempotencyKey
                /* ... other fields ... */
            ));

            return Fold(context);
        });
    }
}
```

**Benefits:**
- Duplicate commands are safely ignored
- Full audit trail of all attempts
- No need for external deduplication store

---

### Example 3: Soft Delete with Audit Trail

```csharp
[EventName("Customer.Registered")]
public record CustomerRegistered(string Name, string Email);

[EventName("Customer.Deleted")]
public record CustomerDeleted(string Reason, DateTimeOffset DeletedAt);

[EventName("Customer.Restored")]
public record CustomerRestored(string Reason, DateTimeOffset RestoredAt);

public partial class Customer : Aggregate
{
    private bool _isDeleted;
    private string? _name;

    private void When(CustomerRegistered @event)
    {
        _name = @event.Name;
        _isDeleted = false;
    }

    private void When(CustomerDeleted @event)
    {
        _isDeleted = true;
    }

    private void When(CustomerRestored @event)
    {
        _isDeleted = false;
    }

    public async Task DeleteCustomer(string reason)
    {
        if (_isDeleted)
            throw new InvalidOperationException("Customer is already deleted");

        await Stream.Session(context =>
        {
            context.Append(new CustomerDeleted(reason, DateTimeOffset.UtcNow));
            return Fold(context);
        });
    }

    public async Task RestoreCustomer(string reason)
    {
        if (!_isDeleted)
            throw new InvalidOperationException("Customer is not deleted");

        await Stream.Session(context =>
        {
            context.Append(new CustomerRestored(reason, DateTimeOffset.UtcNow));
            return Fold(context);
        });
    }
}
```

---

## Best Practices

### 1. Design Compensating Events Upfront

When designing events, think about how they might be reversed:

```
✅ GOOD
OrderPlaced        ↔ OrderCancelled
PaymentCaptured    ↔ PaymentRefunded
InventoryReserved  ↔ InventoryReleased
CustomerRegistered ↔ CustomerDeregistered

❌ BAD
GenericUpdate (too vague - hard to compensate)
StateChanged  (no context - impossible to reverse)
```

### 2. Use Correlation IDs for Traceability

```csharp
var metadata = new ActionMetadata(
    CorrelationId: requestCorrelationId,
    CausationId: commandId,
    EventOccuredAt: DateTimeOffset.UtcNow
);

context.Append(new OrderPlaced(...), actionMetadata: metadata);
```

This allows you to trace all events related to a business transaction.

### 3. Handle External System Failures Gracefully

```csharp
public class SendEmailPostCommitAction : IAsyncPostCommitAction
{
    public async Task PostCommitAsync(
        IEnumerable<JsonEvent> events,
        IObjectDocument document)
    {
        foreach (var evt in events)
        {
            try
            {
                await SendEmail(evt);
            }
            catch (Exception ex)
            {
                // DON'T throw - the event is already committed!
                // Instead, record the failure for retry
                await RecordEmailFailure(evt, ex);
            }
        }
    }
}
```

### 4. Use Version Tokens for Checkpointing

```csharp
// In projections, store the version token
var versionToken = new VersionToken(@event, document);
await SaveCheckpoint(versionToken);

// On restart, resume from checkpoint
var checkpoint = await LoadCheckpoint();
var events = await Stream.ReadAsync(checkpoint.Version + 1);
```

### 5. Implement Retry with Exponential Backoff

```csharp
public async Task<bool> TryExecuteWithRetry(
    Func<Task> operation,
    int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            await operation();
            return true;
        }
        catch (Exception ex)
        {
            if (i == maxRetries - 1)
                throw;

            var delay = TimeSpan.FromSeconds(Math.Pow(2, i));
            await Task.Delay(delay);
        }
    }
    return false;
}
```

### 6. Model Time-Based Compensation

```csharp
[EventName("Subscription.Activated")]
public record SubscriptionActivated(
    string SubscriptionId,
    DateTimeOffset ActivatedAt,
    DateTimeOffset ExpiresAt
);

[EventName("Subscription.Expired")]
public record SubscriptionExpired(
    string SubscriptionId,
    DateTimeOffset ExpiredAt
);

// A background process can check expiration and append SubscriptionExpired events
```

---

## Common Patterns

### Pattern 1: Two-Phase Commit Simulation

```csharp
// Phase 1: Intent
[EventName("Order.PlacementInitiated")]
public record OrderPlacementInitiated(...);

// Phase 2: Confirmation
[EventName("Order.PlacementConfirmed")]
public record OrderPlacementConfirmed(...);

// Or: Cancellation
[EventName("Order.PlacementCancelled")]
public record OrderPlacementCancelled(...);
```

### Pattern 2: Reservation with Timeout

```csharp
[EventName("Inventory.Reserved")]
public record InventoryReserved(
    string ProductId,
    int Quantity,
    string ReservationId,
    DateTimeOffset ExpiresAt  // ← Timeout
);

[EventName("Inventory.ReservationConfirmed")]
public record InventoryReservationConfirmed(string ReservationId);

[EventName("Inventory.ReservationExpired")]
public record InventoryReservationExpired(string ReservationId);

// Background job checks for expired reservations and appends expiry events
```

### Pattern 3: Approval Workflow

```csharp
[EventName("Document.Submitted")]
public record DocumentSubmitted(string DocumentId, string SubmittedBy);

[EventName("Document.Approved")]
public record DocumentApproved(string DocumentId, string ApprovedBy);

[EventName("Document.Rejected")]
public record DocumentRejected(string DocumentId, string RejectedBy, string Reason);
```

---

## Troubleshooting

### Issue 1: "I appended an event but need to undo it immediately"

**Solution:** If the session hasn't been committed yet, don't commit it. Throw an exception to abort:

```csharp
await Stream.Session(context =>
{
    context.Append(new OrderPlaced(...));

    // Validation fails
    if (invalidCondition)
    {
        throw new InvalidOperationException("Order validation failed");
        // Session is aborted, no events committed
    }

    return Fold(context);
});
```

If already committed, append a compensating event.

---

### Issue 2: "External system failed after I committed events"

**Solution:** Use post-commit actions and record failures:

```csharp
public class ExternalSystemIntegration : IAsyncPostCommitAction
{
    public async Task PostCommitAsync(
        IEnumerable<JsonEvent> events,
        IObjectDocument document)
    {
        foreach (var evt in events)
        {
            try
            {
                await CallExternalSystem(evt);
            }
            catch (Exception ex)
            {
                // Append a failure event to trigger retry later
                await AppendIntegrationFailureEvent(evt, ex);
            }
        }
    }
}
```

---

### Issue 3: "How do I handle duplicate commands?"

**Solution:** Use idempotency keys (see Example 2 above).

---

### Issue 4: "Concurrent modifications are causing conflicts"

**Solution:** Use constraints and optimistic concurrency:

```csharp
await Stream.Session(context =>
{
    // Read current state
    await context.ReadAsync();

    // Append new events
    context.Append(new CustomerUpdated(...));

    return Fold(context);
}, constraint: Constraint.Existing);
```

The hash check will detect concurrent modifications and throw `ConstraintException`.

---

### Issue 5: "I need to fix corrupted data"

**Solution:** Use event upcasters:

```csharp
public class FixCorruptedCustomerDataUpcaster : IEventUpcaster
{
    public bool CanUpcast(IEvent @event)
    {
        return @event.EventType == "Customer.Registered"
            && @event.EventVersion < 100;  // Only fix events before version 100
    }

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var payload = @event.GetPayload<CustomerRegistered>();

        // Fix the corrupted data
        var fixed = payload with { Email = payload.Email?.ToLower() };

        yield return @event with { Payload = JsonSerializer.Serialize(fixed) };
    }
}

// Register the upcaster
eventStream.RegisterUpcaster(new FixCorruptedCustomerDataUpcaster());
```

---

## Summary

In event sourcing, **you cannot rollback events**. Instead:

1. **Use compensating events** to reverse the business effect
2. **Use constraints** to prevent invalid states
3. **Use pre-append actions** to validate before committing
4. **Use post-commit actions** carefully - they cannot rollback committed events
5. **Use process managers** for multi-aggregate transactions
6. **Model time, approvals, and retries** explicitly as events
7. **Preserve the full audit trail** - don't delete history

By embracing these patterns, you build systems that are resilient, auditable, and maintainable.

---

## Additional Resources

- [Event Sourcing Pattern](https://martinfowler.com/eaaDev/EventSourcing.html) by Martin Fowler
- [Compensating Transactions](https://docs.microsoft.com/en-us/azure/architecture/patterns/compensating-transaction)
- [Saga Pattern](https://microservices.io/patterns/data/saga.html)
- ErikLieben.FA.ES Framework Documentation: `/docs/`

---

**Document Version:** 1.0
**Last Updated:** 2025-11-04
**Author:** ErikLieben.FA.ES Documentation Team
