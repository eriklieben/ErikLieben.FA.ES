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
- [Advanced Pattern: Multi-Aggregate Coordination with Minimal Compensation](#advanced-pattern-multi-aggregate-coordination-with-minimal-compensation)
  - [Strategy 1: Provisional Events Pattern (Recommended)](#strategy-1-provisional-events-pattern-recommended)
  - [Strategy 2: Single Transaction with Validation](#strategy-2-single-transaction-with-validation)
  - [Strategy 3: Batch with Checkpointing](#strategy-3-batch-with-checkpointing)
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

## Advanced Pattern: Multi-Aggregate Coordination with Minimal Compensation

### The Challenge

When a single action needs to update multiple aggregates, you face a distributed transaction problem:

```
Scenario: Account details change affects 5 projects
  Project 1: [Updated] ✅
  Project 2: [Updated] ✅
  Project 3: [Updated] ✅
  Project 4: [Failed]  ❌ System crashes
  Project 5: [Not started]

Problem: How do we handle this without:
- Writing 3 compensating events immediately
- Losing the ability to retry
- Creating duplicate events on retry
```

**The naive approach** would be to immediately compensate the 3 successful projects, but this creates unnecessary events and loses valuable information about the intent.

**A better approach** uses the **Provisional Events Pattern** combined with a **Process Manager**.

---

### Strategy 1: Provisional Events Pattern (Recommended)

This pattern minimizes compensation events by using provisional/pending state that can be confirmed or cancelled.

#### Step 1: Define Provisional Events

```csharp
// Provisional event - indicates intent, not finalized
[EventName("Project.AccountUpdatePending")]
public record ProjectAccountUpdatePending(
    string ProjectId,
    string ProcessId,              // ← Links to process manager
    string NewAccountName,
    string NewAccountDetails,
    DateTimeOffset PendingAt
);

// Confirmation event - minimal, just confirms the pending change
[EventName("Project.AccountUpdateConfirmed")]
public record ProjectAccountUpdateConfirmed(
    string ProjectId,
    string ProcessId,
    DateTimeOffset ConfirmedAt
);

// Cancellation event - cancels the pending change
[EventName("Project.AccountUpdateCancelled")]
public record ProjectAccountUpdateCancelled(
    string ProjectId,
    string ProcessId,
    string Reason,
    DateTimeOffset CancelledAt
);
```

#### Step 2: Aggregate Handles Provisional State

```csharp
public partial class Project : Aggregate
{
    private string? _accountName;
    private string? _accountDetails;
    private ProjectAccountUpdateStatus _updateStatus = ProjectAccountUpdateStatus.None;
    private string? _pendingAccountName;
    private string? _pendingAccountDetails;
    private string? _pendingProcessId;

    private void When(ProjectAccountUpdatePending @event)
    {
        // Store pending state - don't apply yet
        _pendingAccountName = @event.NewAccountName;
        _pendingAccountDetails = @event.NewAccountDetails;
        _pendingProcessId = @event.ProcessId;
        _updateStatus = ProjectAccountUpdateStatus.Pending;
    }

    private void When(ProjectAccountUpdateConfirmed @event)
    {
        // Now apply the pending changes
        _accountName = _pendingAccountName;
        _accountDetails = _pendingAccountDetails;
        _updateStatus = ProjectAccountUpdateStatus.Confirmed;

        // Clear pending state
        _pendingAccountName = null;
        _pendingAccountDetails = null;
        _pendingProcessId = null;
    }

    private void When(ProjectAccountUpdateCancelled @event)
    {
        // Discard pending changes
        _pendingAccountName = null;
        _pendingAccountDetails = null;
        _pendingProcessId = null;
        _updateStatus = ProjectAccountUpdateStatus.None;
    }

    public async Task ApplyAccountUpdateProvisionally(
        string processId,
        string newAccountName,
        string newAccountDetails)
    {
        // Check if already processed for this process
        if (_pendingProcessId == processId ||
            (_updateStatus == ProjectAccountUpdateStatus.Confirmed && _pendingProcessId == processId))
        {
            return; // Idempotent - already processed
        }

        await Stream.Session(context =>
        {
            context.Append(new ProjectAccountUpdatePending(
                Stream.Document.ObjectId,
                processId,
                newAccountName,
                newAccountDetails,
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });
    }

    public async Task ConfirmAccountUpdate(string processId)
    {
        if (_pendingProcessId != processId)
            throw new InvalidOperationException(
                $"No pending update for process {processId}");

        if (_updateStatus == ProjectAccountUpdateStatus.Confirmed)
            return; // Already confirmed - idempotent

        await Stream.Session(context =>
        {
            context.Append(new ProjectAccountUpdateConfirmed(
                Stream.Document.ObjectId,
                processId,
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });
    }

    public async Task CancelAccountUpdate(string processId, string reason)
    {
        if (_pendingProcessId != processId)
            return; // Not for this process - idempotent

        if (_updateStatus == ProjectAccountUpdateStatus.None)
            return; // Already cancelled - idempotent

        await Stream.Session(context =>
        {
            context.Append(new ProjectAccountUpdateCancelled(
                Stream.Document.ObjectId,
                processId,
                reason,
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });
    }
}

public enum ProjectAccountUpdateStatus
{
    None,
    Pending,
    Confirmed
}
```

#### Step 3: Process Manager Coordinates Updates

```csharp
[EventName("AccountUpdateProcess.Started")]
public record AccountUpdateProcessStarted(
    string ProcessId,
    string AccountId,
    List<string> ProjectIds,
    string NewAccountName,
    string NewAccountDetails
);

[EventName("AccountUpdateProcess.ProjectProvisioned")]
public record AccountUpdateProcessProjectProvisioned(
    string ProcessId,
    string ProjectId,
    int CompletedCount,
    int TotalCount
);

[EventName("AccountUpdateProcess.Failed")]
public record AccountUpdateProcessFailed(
    string ProcessId,
    string FailedProjectId,
    string Reason,
    int ProvisionedCount
);

[EventName("AccountUpdateProcess.AllProvisioned")]
public record AccountUpdateProcessAllProvisioned(
    string ProcessId,
    int TotalCount
);

[EventName("AccountUpdateProcess.Confirmed")]
public record AccountUpdateProcessConfirmed(
    string ProcessId,
    DateTimeOffset ConfirmedAt
);

[EventName("AccountUpdateProcess.Cancelled")]
public record AccountUpdateProcessCancelled(
    string ProcessId,
    string Reason,
    DateTimeOffset CancelledAt
);

public partial class AccountUpdateProcess : Aggregate
{
    private string? _processId;
    private List<string> _projectIds = new();
    private HashSet<string> _provisionedProjects = new();
    private AccountUpdateProcessStatus _status = AccountUpdateProcessStatus.New;
    private string? _newAccountName;
    private string? _newAccountDetails;
    private string? _failureReason;

    private void When(AccountUpdateProcessStarted @event)
    {
        _processId = @event.ProcessId;
        _projectIds = @event.ProjectIds;
        _newAccountName = @event.NewAccountName;
        _newAccountDetails = @event.NewAccountDetails;
        _status = AccountUpdateProcessStatus.Started;
    }

    private void When(AccountUpdateProcessProjectProvisioned @event)
    {
        _provisionedProjects.Add(@event.ProjectId);
    }

    private void When(AccountUpdateProcessAllProvisioned @event)
    {
        _status = AccountUpdateProcessStatus.AllProvisioned;
    }

    private void When(AccountUpdateProcessFailed @event)
    {
        _status = AccountUpdateProcessStatus.Failed;
        _failureReason = @event.Reason;
    }

    private void When(AccountUpdateProcessConfirmed @event)
    {
        _status = AccountUpdateProcessStatus.Confirmed;
    }

    private void When(AccountUpdateProcessCancelled @event)
    {
        _status = AccountUpdateProcessStatus.Cancelled;
    }

    public async Task ExecuteAccountUpdate(
        string accountId,
        List<string> projectIds,
        string newAccountName,
        string newAccountDetails,
        Func<string, Task<Project>> getProject)
    {
        var processId = Guid.NewGuid().ToString();

        // Step 1: Record start
        await Stream.Session(context =>
        {
            context.Append(new AccountUpdateProcessStarted(
                processId,
                accountId,
                projectIds,
                newAccountName,
                newAccountDetails
            ));
            return Fold(context);
        });

        // Step 2: Provision all projects (two-phase commit phase 1)
        try
        {
            for (int i = 0; i < projectIds.Count; i++)
            {
                var projectId = projectIds[i];

                try
                {
                    var project = await getProject(projectId);

                    // Apply provisional update
                    await project.ApplyAccountUpdateProvisionally(
                        processId,
                        newAccountName,
                        newAccountDetails
                    );

                    // Record progress
                    await Stream.Session(context =>
                    {
                        context.Append(new AccountUpdateProcessProjectProvisioned(
                            processId,
                            projectId,
                            i + 1,
                            projectIds.Count
                        ));
                        return Fold(context);
                    });
                }
                catch (Exception ex)
                {
                    // Record failure
                    await Stream.Session(context =>
                    {
                        context.Append(new AccountUpdateProcessFailed(
                            processId,
                            projectId,
                            ex.Message,
                            i
                        ));
                        return Fold(context);
                    });

                    // Cancel all provisioned projects
                    await CancelProvisionedProjects(getProject);
                    throw;
                }
            }

            // Step 3: All provisioned successfully
            await Stream.Session(context =>
            {
                context.Append(new AccountUpdateProcessAllProvisioned(
                    processId,
                    projectIds.Count
                ));
                return Fold(context);
            });

            // Step 4: Confirm all projects (two-phase commit phase 2)
            await ConfirmAllProjects(getProject);

            // Step 5: Mark process as confirmed
            await Stream.Session(context =>
            {
                context.Append(new AccountUpdateProcessConfirmed(
                    processId,
                    DateTimeOffset.UtcNow
                ));
                return Fold(context);
            });
        }
        catch (Exception ex)
        {
            // Process failed - provisioned projects are already cancelled
            throw;
        }
    }

    private async Task CancelProvisionedProjects(
        Func<string, Task<Project>> getProject)
    {
        foreach (var projectId in _provisionedProjects)
        {
            try
            {
                var project = await getProject(projectId);
                await project.CancelAccountUpdate(
                    _processId!,
                    _failureReason ?? "Process failed"
                );
            }
            catch (Exception ex)
            {
                // Log but continue cancelling others
                // Consider recording this failure in the process stream
            }
        }

        await Stream.Session(context =>
        {
            context.Append(new AccountUpdateProcessCancelled(
                _processId!,
                _failureReason ?? "Process failed",
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });
    }

    private async Task ConfirmAllProjects(
        Func<string, Task<Project>> getProject)
    {
        foreach (var projectId in _projectIds)
        {
            var project = await getProject(projectId);
            await project.ConfirmAccountUpdate(_processId!);
        }
    }

    // Resume from failure - can be called to retry the process
    public async Task Resume(Func<string, Task<Project>> getProject)
    {
        if (_status == AccountUpdateProcessStatus.Confirmed)
            return; // Already completed

        if (_status == AccountUpdateProcessStatus.Cancelled)
        {
            // Cannot resume cancelled process
            throw new InvalidOperationException("Process was cancelled");
        }

        // If we failed during provisioning, cancel what we have and restart
        if (_status == AccountUpdateProcessStatus.Failed)
        {
            await CancelProvisionedProjects(getProject);

            // Client should create a new process to retry
            return;
        }

        // If all provisioned but not confirmed, complete the confirmation
        if (_status == AccountUpdateProcessStatus.AllProvisioned)
        {
            await ConfirmAllProjects(getProject);

            await Stream.Session(context =>
            {
                context.Append(new AccountUpdateProcessConfirmed(
                    _processId!,
                    DateTimeOffset.UtcNow
                ));
                return Fold(context);
            });
        }
    }
}

public enum AccountUpdateProcessStatus
{
    New,
    Started,
    Failed,
    AllProvisioned,
    Confirmed,
    Cancelled
}
```

#### Key Benefits of This Approach

1. **Minimal Events on Failure**:
   - 3 projects get 1 pending event each (3 events)
   - 1 cancellation event per project (3 events)
   - Total: 6 events (vs 6 full update + 3 full compensation = potentially 12+ events)

2. **Idempotent Retry**:
   - Process can be safely retried
   - Projects check if they've already processed this processId
   - No duplicate events on retry

3. **Clear Intent**:
   - Pending events show what *would* have happened
   - Full audit trail of the distributed transaction
   - Can analyze failures easily

4. **Atomic Confirmation**:
   - Once all projects are provisioned, confirmation is quick
   - If confirmation fails, can resume from that point

---

### Strategy 2: Single Transaction with Validation

For smaller sets of aggregates where you control the infrastructure, you can use a single transaction.

#### Using ErikLieben.FA.ES Features

```csharp
public async Task UpdateAccountAcrossProjects(
    string accountId,
    List<string> projectIds,
    string newAccountName)
{
    var processId = Guid.NewGuid().ToString();
    var metadata = new ActionMetadata(
        CorrelationId: processId,
        EventOccuredAt: DateTimeOffset.UtcNow
    );

    // Validate all projects can be updated BEFORE committing any events
    var projects = new List<Project>();
    foreach (var projectId in projectIds)
    {
        var project = await GetProject(projectId);
        await project.Fold(); // Load current state

        // Validate business rules
        if (!project.CanUpdateAccount())
        {
            throw new InvalidOperationException(
                $"Project {projectId} cannot update account");
        }

        projects.Add(project);
    }

    // All validations passed - now commit all at once
    var updateTasks = projects.Select(async project =>
    {
        await project.Stream.Session(context =>
        {
            context.Append(
                new ProjectAccountUpdated(
                    project.Stream.Document.ObjectId,
                    newAccountName
                ),
                actionMetadata: metadata
            );
            return project.Fold(context);
        });
    });

    // Execute all updates
    // Note: This isn't a true ACID transaction across aggregates,
    // but we've validated first to minimize failure risk
    await Task.WhenAll(updateTasks);
}
```

**Trade-offs:**
- ✅ Simpler code
- ✅ Fewer events
- ❌ Not truly atomic (no cross-aggregate transactions)
- ❌ Validation doesn't prevent concurrent modifications
- ❌ If one fails mid-way, need compensation

---

### Strategy 3: Batch with Checkpointing

For very large numbers of aggregates (100s or 1000s), use batch processing with checkpointing.

```csharp
[EventName("AccountUpdateBatch.Started")]
public record AccountUpdateBatchStarted(
    string BatchId,
    string AccountId,
    int TotalProjects
);

[EventName("AccountUpdateBatch.CheckpointReached")]
public record AccountUpdateBatchCheckpointReached(
    string BatchId,
    int ProcessedCount,
    int TotalCount,
    string LastProcessedProjectId
);

[EventName("AccountUpdateBatch.Completed")]
public record AccountUpdateBatchCompleted(
    string BatchId,
    int SuccessCount,
    int FailureCount
);

[EventName("AccountUpdateBatch.ProjectFailed")]
public record AccountUpdateBatchProjectFailed(
    string BatchId,
    string ProjectId,
    string Reason
);

public partial class AccountUpdateBatch : Aggregate
{
    private string? _batchId;
    private int _processedCount;
    private int _totalCount;
    private string? _lastProcessedProjectId;
    private List<string> _failedProjects = new();
    private BatchStatus _status = BatchStatus.New;

    public async Task ExecuteBatchUpdate(
        List<string> projectIds,
        string newAccountName,
        Func<string, Task<Project>> getProject,
        int checkpointInterval = 10)
    {
        var batchId = Guid.NewGuid().ToString();

        await Stream.Session(context =>
        {
            context.Append(new AccountUpdateBatchStarted(
                batchId,
                newAccountName,
                projectIds.Count
            ));
            return Fold(context);
        });

        for (int i = 0; i < projectIds.Count; i++)
        {
            var projectId = projectIds[i];

            try
            {
                var project = await getProject(projectId);
                await project.UpdateAccount(newAccountName);

                _processedCount++;
                _lastProcessedProjectId = projectId;

                // Checkpoint periodically
                if ((i + 1) % checkpointInterval == 0)
                {
                    await Stream.Session(context =>
                    {
                        context.Append(new AccountUpdateBatchCheckpointReached(
                            batchId,
                            _processedCount,
                            projectIds.Count,
                            projectId
                        ));
                        return Fold(context);
                    });
                }
            }
            catch (Exception ex)
            {
                // Record failure but continue processing others
                await Stream.Session(context =>
                {
                    context.Append(new AccountUpdateBatchProjectFailed(
                        batchId,
                        projectId,
                        ex.Message
                    ));
                    return Fold(context);
                });

                _failedProjects.Add(projectId);
            }
        }

        // Complete
        await Stream.Session(context =>
        {
            context.Append(new AccountUpdateBatchCompleted(
                batchId,
                _processedCount - _failedProjects.Count,
                _failedProjects.Count
            ));
            return Fold(context);
        });
    }

    // Resume from last checkpoint
    public async Task Resume(
        List<string> allProjectIds,
        string newAccountName,
        Func<string, Task<Project>> getProject)
    {
        // Find where we left off
        var startIndex = allProjectIds.IndexOf(_lastProcessedProjectId!) + 1;
        var remainingProjects = allProjectIds.Skip(startIndex).ToList();

        // Continue processing
        await ExecuteBatchUpdate(
            remainingProjects,
            newAccountName,
            getProject
        );
    }
}

public enum BatchStatus
{
    New,
    InProgress,
    Completed,
    Failed
}
```

**Trade-offs:**
- ✅ Handles large numbers of aggregates
- ✅ Can resume from checkpoint on failure
- ✅ Doesn't require all-or-nothing semantics
- ❌ Not atomic - some projects may be updated while others aren't
- ❌ Need to handle partial success scenarios
- ⚠️ Consider if your business rules allow partial updates

---

### Comparison: Which Strategy to Use?

| Strategy | Use When | Events on Failure | Retry Complexity | Atomicity |
|----------|----------|-------------------|------------------|-----------|
| **Provisional Events** | 2-100 aggregates, need atomicity | Minimal (1 pending + 1 cancel per aggregate) | Low (idempotent) | Semantic atomic |
| **Single Transaction** | 2-10 aggregates, low failure risk | Medium (need full compensation) | Medium | No guarantee |
| **Batch Checkpointing** | 100+ aggregates, partial success OK | High (many events) | Low (resume from checkpoint) | No atomicity |

---

### Example: Your 5 Projects Scenario

Using **Provisional Events Pattern** (recommended):

```csharp
// Usage
var process = await GetOrCreateProcess(processId);
var projectIds = new List<string> { "p1", "p2", "p3", "p4", "p5" };

await process.ExecuteAccountUpdate(
    accountId: "acc-123",
    projectIds: projectIds,
    newAccountName: "New Account Name",
    newAccountDetails: "New details",
    getProject: async (id) => await projectRepository.Get(id)
);
```

**Event Flow on Success:**
```
Process Stream:
  1. AccountUpdateProcess.Started (5 projects)
  2. AccountUpdateProcess.ProjectProvisioned (p1, 1/5)
  3. AccountUpdateProcess.ProjectProvisioned (p2, 2/5)
  4. AccountUpdateProcess.ProjectProvisioned (p3, 3/5)
  5. AccountUpdateProcess.ProjectProvisioned (p4, 4/5)
  6. AccountUpdateProcess.ProjectProvisioned (p5, 5/5)
  7. AccountUpdateProcess.AllProvisioned
  8. AccountUpdateProcess.Confirmed

Project Streams (p1-p5):
  Each gets: ProjectAccountUpdatePending, ProjectAccountUpdateConfirmed

Total: 8 process events + 10 project events = 18 events
```

**Event Flow on Failure (after p3):**
```
Process Stream:
  1. AccountUpdateProcess.Started (5 projects)
  2. AccountUpdateProcess.ProjectProvisioned (p1, 1/5)
  3. AccountUpdateProcess.ProjectProvisioned (p2, 2/5)
  4. AccountUpdateProcess.ProjectProvisioned (p3, 3/5)
  5. AccountUpdateProcess.Failed (p4, "Connection timeout", 3 provisioned)
  6. AccountUpdateProcess.Cancelled (reason: "Connection timeout")

Project Streams:
  p1: ProjectAccountUpdatePending, ProjectAccountUpdateCancelled
  p2: ProjectAccountUpdatePending, ProjectAccountUpdateCancelled
  p3: ProjectAccountUpdatePending, ProjectAccountUpdateCancelled
  p4: (no events)
  p5: (no events)

Total: 6 process events + 6 project events = 12 events

On retry (new process):
  - New processId generated
  - All 5 projects get provisioned with new processId
  - No duplicate events because processId is different
  - All 5 confirmed on success
```

**Key advantage**: The pending events contain all the data, so cancellation events are lightweight. On retry with a new process, there's no conflict because each process has its own ID.

---

### Best Practices for Multi-Aggregate Coordination

1. **Use Process IDs**: Always include a process/correlation ID to link events together
2. **Make Operations Idempotent**: Check if the operation was already performed
3. **Record Intent Early**: Create the process manager event before modifying aggregates
4. **Fail Fast**: Validate as much as possible before committing events
5. **Optimize for Success**: Design for the happy path; handle failures gracefully
6. **Consider Timeouts**: Provisional events should have expiration (use background job to auto-cancel)
7. **Monitor Progress**: Emit progress events for observability
8. **Test Failure Scenarios**: Explicitly test failures at each step

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

**Document Version:** 1.1
**Last Updated:** 2025-11-04
**Author:** ErikLieben.FA.ES Documentation Team
**Changelog:**
- v1.1: Added "Advanced Pattern: Multi-Aggregate Coordination with Minimal Compensation" section with three strategies for handling distributed transactions
- v1.0: Initial release
