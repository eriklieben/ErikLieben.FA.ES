# Causation and Correlation IDs for Idempotency in Event Sourcing

## Table of Contents
- [Introduction](#introduction)
- [Understanding the IDs](#understanding-the-ids)
- [How They Enable Idempotency](#how-they-enable-idempotency)
- [Simple Operation: Single Event](#simple-operation-single-event)
- [Cascading Operations: The Challenge](#cascading-operations-the-challenge)
- [Solution: Hierarchical Causation IDs](#solution-hierarchical-causation-ids)
- [Solution: Operation State Tracking](#solution-operation-state-tracking)
- [Solution: Saga Pattern with Causation Chain](#solution-saga-pattern-with-causation-chain)
- [Complete Working Examples](#complete-working-examples)
- [Best Practices](#best-practices)
- [Troubleshooting Common Scenarios](#troubleshooting-common-scenarios)

---

## Introduction

When building idempotent operations in event sourcing, you need to track which operations have already been processed. The ErikLieben.FA.ES framework provides two critical fields in `ActionMetadata`:

- **`CausationId`**: Identifies the specific command/action that caused this event
- **`CorrelationId`**: Groups all events that are part of the same business transaction

These IDs are essential for making batch operations idempotent, especially when dealing with cascading operations.

---

## Understanding the IDs

### ActionMetadata Structure

From `src/ErikLieben.FA.ES/ActionMetadata.cs`:

```csharp
public record ActionMetadata(
    string? CorrelationId = null,      // Business transaction ID
    string? CausationId = null,         // Command/action that caused this event
    VersionToken? OriginatedFromUser = null,
    DateTimeOffset? EventOccuredAt = null
)
```

### CorrelationId: The Business Transaction

**Purpose**: Groups all events that belong to the same business operation

**Example**: Processing a customer order
```
CorrelationId: "order-process-abc123"

Events with this CorrelationId:
1. Order.Placed (Order aggregate)
2. Inventory.Reserved (Inventory aggregate)
3. Payment.Captured (Payment aggregate)
4. Shipping.Scheduled (Shipping aggregate)
5. Customer.NotificationSent (Notification aggregate)
```

**All these events** across different aggregates share the same `CorrelationId` because they're part of the same "place order" business transaction.

### CausationId: The Direct Cause

**Purpose**: Identifies the specific command or parent event that directly caused this event

**Example**: Same order process
```
Command: PlaceOrderCommand (id: "cmd-xyz789")
  ↓ causes
Event: Order.Placed (causationId: "cmd-xyz789")
  ↓ causes
Command: ReserveInventoryCommand (id: "cmd-inv-456")
  ↓ causes
Event: Inventory.Reserved (causationId: "cmd-inv-456")
```

**Each event** has its own `CausationId` pointing to what directly caused it.

### Visual Comparison

```
┌─────────────────────────────────────────────────────────────┐
│ Business Transaction: Place Order                           │
│ CorrelationId: "order-abc123" (same for all)               │
└─────────────────────────────────────────────────────────────┘
         │
         ├─> Command: PlaceOrderCommand
         │   CausationId: "cmd-place-order-1"
         │   ↓
         │   Event: Order.Placed
         │   CausationId: "cmd-place-order-1"
         │   CorrelationId: "order-abc123"
         │
         ├─> Command: ReserveInventoryCommand
         │   CausationId: "cmd-reserve-inv-2"
         │   ↓
         │   Event: Inventory.Reserved
         │   CausationId: "cmd-reserve-inv-2"
         │   CorrelationId: "order-abc123"
         │
         └─> Command: CapturePaymentCommand
             CausationId: "cmd-capture-pay-3"
             ↓
             Event: Payment.Captured
             CausationId: "cmd-capture-pay-3"
             CorrelationId: "order-abc123"
```

---

## How They Enable Idempotency

### Simple Case: Single Operation, Single Event

```csharp
public partial class Customer : Aggregate
{
    private HashSet<string> _processedCausationIds = new();

    private void When(CustomerUpdated @event)
    {
        // Track that we've processed this causation
        if (@event.ActionMetadata?.CausationId != null)
        {
            _processedCausationIds.Add(@event.ActionMetadata.CausationId);
        }

        // Apply the update
        _name = @event.NewName;
        _email = @event.NewEmail;
    }

    public async Task UpdateCustomer(
        string newName,
        string newEmail,
        string operationId)  // ← This becomes CausationId
    {
        // Check if already processed
        if (_processedCausationIds.Contains(operationId))
        {
            return; // Already processed - idempotent!
        }

        await Stream.Session(context =>
        {
            var metadata = new ActionMetadata(
                CorrelationId: Guid.NewGuid().ToString(),
                CausationId: operationId,  // ← Track this operation
                EventOccuredAt: DateTimeOffset.UtcNow
            );

            context.Append(
                new CustomerUpdated(newName, newEmail),
                actionMetadata: metadata
            );

            return Fold(context);
        });
    }
}
```

**Result**: If `UpdateCustomer` is called twice with the same `operationId`, the second call is a no-op.

---

## Simple Operation: Single Event

### Batch Processing Example

```csharp
public async Task ProcessBatchOfCustomers(List<Customer> customers, string batchId)
{
    for (int i = 0; i < customers.Count; i++)
    {
        var customer = customers[i];
        var operationId = $"{batchId}-customer-{i}";

        // This is idempotent - safe to retry
        await UpdateCustomerInAggregate(
            customer.Id,
            customer.NewName,
            customer.NewEmail,
            operationId
        );
    }
}

// If batch fails at customer 7 and we restart:
// - Customers 0-6: Already have events with those causationIds → skipped
// - Customer 7: No event with that causationId → processed
// - Customers 8-15: No events → processed
```

**Key Point**: Each operation has a unique `CausationId` that combines `batchId` and item index.

---

## Cascading Operations: The Challenge

### The Problem

When one operation triggers multiple cascading operations, tracking idempotency becomes complex:

```csharp
Operation: UpdateAccountDetails (operationId: "batch-123-item-5")
  ↓
  Triggers:
    1. Update Project.AccountName ✅ (succeeds)
       ↓
       Triggers:
         a) Update Project.BillingInfo ✅ (succeeds)
         b) Notify AccountingSystem ❌ (FAILS)

    2. Update Customer.DefaultAccount (not started)

Problem on retry with same operationId "batch-123-item-5":
  - Main operation detects it already processed → returns early
  - BUT: Step 1b (Notify AccountingSystem) never completed!
  - AND: Step 2 (Update Customer.DefaultAccount) never started!
```

**The challenge**: A single `CausationId` at the top level doesn't track the state of cascading sub-operations.

### Real-World Example

```csharp
public async Task UpdateProjectAccount(
    string projectId,
    string newAccountName,
    string operationId)
{
    // Main operation
    var project = await GetProject(projectId);

    // Check idempotency
    if (project.HasProcessedOperation(operationId))
    {
        return; // Already processed
    }

    // Step 1: Update project
    await project.UpdateAccountName(newAccountName, operationId);

    // Step 2: Cascading operation - update related customer
    var customerId = project.CustomerId;
    await UpdateCustomerDefaultProject(customerId, projectId);

    // Step 3: Cascading operation - notify external system
    await notificationService.NotifyAccountChange(projectId, newAccountName);

    // What if Step 3 fails? Retry with same operationId will skip everything!
}
```

---

## Solution: Hierarchical Causation IDs

Use a hierarchical structure for cascading operations:

```
operationId: "batch-123-item-5"
  ├─ "batch-123-item-5:project-update"
  ├─ "batch-123-item-5:customer-update"
  └─ "batch-123-item-5:notification"
```

### Implementation

```csharp
[EventName("Project.AccountUpdateStarted")]
public record ProjectAccountUpdateStarted(
    string ProjectId,
    string OperationId,
    string NewAccountName
);

[EventName("Project.AccountNameUpdated")]
public record ProjectAccountNameUpdated(
    string ProjectId,
    string OperationId,
    string SubOperationId,  // ← Hierarchical ID
    string NewAccountName
);

[EventName("Customer.DefaultProjectUpdated")]
public record CustomerDefaultProjectUpdated(
    string CustomerId,
    string ProjectId,
    string OperationId,
    string SubOperationId  // ← Hierarchical ID
);

[EventName("ExternalSystem.NotificationSent")]
public record ExternalSystemNotificationSent(
    string ProjectId,
    string OperationId,
    string SubOperationId  // ← Hierarchical ID
);

[EventName("Project.AccountUpdateCompleted")]
public record ProjectAccountUpdateCompleted(
    string ProjectId,
    string OperationId,
    int CompletedSubOperations
);

public partial class ProjectAccountUpdateProcess : Aggregate
{
    private string? _operationId;
    private HashSet<string> _completedSubOperations = new();
    private ProjectAccountUpdateState _state = ProjectAccountUpdateState.NotStarted;

    private void When(ProjectAccountUpdateStarted @event)
    {
        _operationId = @event.OperationId;
        _state = ProjectAccountUpdateState.InProgress;
    }

    private void When(ProjectAccountNameUpdated @event)
    {
        _completedSubOperations.Add(@event.SubOperationId);
    }

    private void When(CustomerDefaultProjectUpdated @event)
    {
        _completedSubOperations.Add(@event.SubOperationId);
    }

    private void When(ExternalSystemNotificationSent @event)
    {
        _completedSubOperations.Add(@event.SubOperationId);
    }

    private void When(ProjectAccountUpdateCompleted @event)
    {
        _state = ProjectAccountUpdateState.Completed;
    }

    public async Task UpdateProjectAccount(
        string projectId,
        string newAccountName,
        string operationId)
    {
        // Check if already completed
        if (_state == ProjectAccountUpdateState.Completed && _operationId == operationId)
        {
            return; // Fully completed - idempotent!
        }

        // Start if not started
        if (_state == ProjectAccountUpdateState.NotStarted)
        {
            await Stream.Session(context =>
            {
                var metadata = new ActionMetadata(
                    CorrelationId: operationId,
                    CausationId: operationId,
                    EventOccuredAt: DateTimeOffset.UtcNow
                );

                context.Append(
                    new ProjectAccountUpdateStarted(projectId, operationId, newAccountName),
                    actionMetadata: metadata
                );
                return Fold(context);
            });
        }

        // Sub-operation 1: Update project account name
        var subOp1 = $"{operationId}:project-update";
        if (!_completedSubOperations.Contains(subOp1))
        {
            var project = await GetProject(projectId);
            await project.UpdateAccountName(newAccountName, subOp1);

            await Stream.Session(context =>
            {
                var metadata = new ActionMetadata(
                    CorrelationId: operationId,
                    CausationId: subOp1,
                    EventOccuredAt: DateTimeOffset.UtcNow
                );

                context.Append(
                    new ProjectAccountNameUpdated(
                        projectId,
                        operationId,
                        subOp1,
                        newAccountName
                    ),
                    actionMetadata: metadata
                );
                return Fold(context);
            });
        }

        // Sub-operation 2: Update customer default project
        var subOp2 = $"{operationId}:customer-update";
        if (!_completedSubOperations.Contains(subOp2))
        {
            var project = await GetProject(projectId);
            var customerId = project.CustomerId;

            await UpdateCustomerDefaultProject(customerId, projectId, subOp2);

            await Stream.Session(context =>
            {
                var metadata = new ActionMetadata(
                    CorrelationId: operationId,
                    CausationId: subOp2,
                    EventOccuredAt: DateTimeOffset.UtcNow
                );

                context.Append(
                    new CustomerDefaultProjectUpdated(
                        customerId,
                        projectId,
                        operationId,
                        subOp2
                    ),
                    actionMetadata: metadata
                );
                return Fold(context);
            });
        }

        // Sub-operation 3: Notify external system
        var subOp3 = $"{operationId}:notification";
        if (!_completedSubOperations.Contains(subOp3))
        {
            await notificationService.NotifyAccountChange(projectId, newAccountName);

            await Stream.Session(context =>
            {
                var metadata = new ActionMetadata(
                    CorrelationId: operationId,
                    CausationId: subOp3,
                    EventOccuredAt: DateTimeOffset.UtcNow
                );

                context.Append(
                    new ExternalSystemNotificationSent(
                        projectId,
                        operationId,
                        subOp3
                    ),
                    actionMetadata: metadata
                );
                return Fold(context);
            });
        }

        // All sub-operations completed
        await Stream.Session(context =>
        {
            var metadata = new ActionMetadata(
                CorrelationId: operationId,
                CausationId: operationId,
                EventOccuredAt: DateTimeOffset.UtcNow
            );

            context.Append(
                new ProjectAccountUpdateCompleted(
                    projectId,
                    operationId,
                    _completedSubOperations.Count
                ),
                actionMetadata: metadata
            );
            return Fold(context);
        });
    }
}

public enum ProjectAccountUpdateState
{
    NotStarted,
    InProgress,
    Completed
}
```

### How This Works on Retry

**First Attempt** (fails at sub-operation 3):
```
1. ProjectAccountUpdateStarted (operationId: "batch-123-item-5")
2. ProjectAccountNameUpdated (subOperationId: "batch-123-item-5:project-update")
3. CustomerDefaultProjectUpdated (subOperationId: "batch-123-item-5:customer-update")
4. FAILED at NotifyAccountChange ❌
```

**Retry with same operationId**:
```
1. Check: _state != Completed → Continue
2. Sub-op 1: "batch-123-item-5:project-update" in _completedSubOperations → SKIP ✅
3. Sub-op 2: "batch-123-item-5:customer-update" in _completedSubOperations → SKIP ✅
4. Sub-op 3: "batch-123-item-5:notification" NOT in _completedSubOperations → EXECUTE ✅
5. ProjectAccountUpdateCompleted
```

**Result**: Only the failed sub-operation is retried. Full idempotency preserved.

---

## Solution: Operation State Tracking

Alternative approach: Track operation state explicitly in an aggregate.

### Implementation

```csharp
[EventName("Operation.Registered")]
public record OperationRegistered(
    string OperationId,
    string OperationType,
    Dictionary<string, string> Parameters
);

[EventName("Operation.StepCompleted")]
public record OperationStepCompleted(
    string OperationId,
    string StepName,
    DateTimeOffset CompletedAt
);

[EventName("Operation.Completed")]
public record OperationCompleted(
    string OperationId,
    DateTimeOffset CompletedAt
);

[EventName("Operation.Failed")]
public record OperationFailed(
    string OperationId,
    string FailedStep,
    string Reason
);

public partial class OperationTracker : Aggregate
{
    private string? _operationId;
    private OperationStatus _status = OperationStatus.NotStarted;
    private HashSet<string> _completedSteps = new();

    private void When(OperationRegistered @event)
    {
        _operationId = @event.OperationId;
        _status = OperationStatus.InProgress;
    }

    private void When(OperationStepCompleted @event)
    {
        _completedSteps.Add(@event.StepName);
    }

    private void When(OperationCompleted @event)
    {
        _status = OperationStatus.Completed;
    }

    private void When(OperationFailed @event)
    {
        _status = OperationStatus.Failed;
    }

    public async Task<bool> IsOperationCompleted(string operationId)
    {
        return _operationId == operationId && _status == OperationStatus.Completed;
    }

    public async Task<bool> IsStepCompleted(string stepName)
    {
        return _completedSteps.Contains(stepName);
    }

    public async Task RegisterOperation(
        string operationId,
        string operationType,
        Dictionary<string, string> parameters)
    {
        if (_operationId == operationId)
        {
            return; // Already registered
        }

        await Stream.Session(context =>
        {
            context.Append(new OperationRegistered(
                operationId,
                operationType,
                parameters
            ));
            return Fold(context);
        });
    }

    public async Task RecordStepCompleted(string operationId, string stepName)
    {
        if (_completedSteps.Contains(stepName))
        {
            return; // Already completed
        }

        await Stream.Session(context =>
        {
            context.Append(new OperationStepCompleted(
                operationId,
                stepName,
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });
    }

    public async Task CompleteOperation(string operationId)
    {
        if (_status == OperationStatus.Completed)
        {
            return;
        }

        await Stream.Session(context =>
        {
            context.Append(new OperationCompleted(
                operationId,
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });
    }
}

public enum OperationStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed
}
```

### Usage

```csharp
public async Task UpdateProjectAccount(
    string projectId,
    string newAccountName,
    string operationId)
{
    // Get or create operation tracker
    var tracker = await GetOrCreateOperationTracker(operationId);

    // Check if fully completed
    if (await tracker.IsOperationCompleted(operationId))
    {
        return; // Already done
    }

    // Register operation
    await tracker.RegisterOperation(
        operationId,
        "UpdateProjectAccount",
        new Dictionary<string, string>
        {
            ["ProjectId"] = projectId,
            ["NewAccountName"] = newAccountName
        }
    );

    // Step 1: Update project
    if (!await tracker.IsStepCompleted("project-update"))
    {
        var project = await GetProject(projectId);
        await project.UpdateAccountName(newAccountName, $"{operationId}:project-update");
        await tracker.RecordStepCompleted(operationId, "project-update");
    }

    // Step 2: Update customer
    if (!await tracker.IsStepCompleted("customer-update"))
    {
        var project = await GetProject(projectId);
        var customerId = project.CustomerId;
        await UpdateCustomerDefaultProject(customerId, projectId, $"{operationId}:customer-update");
        await tracker.RecordStepCompleted(operationId, "customer-update");
    }

    // Step 3: Notify external system
    if (!await tracker.IsStepCompleted("notification"))
    {
        await notificationService.NotifyAccountChange(projectId, newAccountName);
        await tracker.RecordStepCompleted(operationId, "notification");
    }

    // Complete
    await tracker.CompleteOperation(operationId);
}
```

---

## Solution: Saga Pattern with Causation Chain

For complex cascading operations, use the Saga pattern with explicit causation tracking.

### Implementation

```csharp
[EventName("AccountUpdateSaga.Started")]
public record AccountUpdateSagaStarted(
    string SagaId,
    string AccountId,
    string NewAccountName,
    List<string> ProjectIds
);

[EventName("AccountUpdateSaga.ProjectUpdateRequested")]
public record AccountUpdateSagaProjectUpdateRequested(
    string SagaId,
    string ProjectId,
    string RequestId  // ← Used as CausationId for project update
);

[EventName("AccountUpdateSaga.ProjectUpdateCompleted")]
public record AccountUpdateSagaProjectUpdateCompleted(
    string SagaId,
    string ProjectId,
    string RequestId
);

[EventName("AccountUpdateSaga.ProjectUpdateFailed")]
public record AccountUpdateSagaProjectUpdateFailed(
    string SagaId,
    string ProjectId,
    string RequestId,
    string Reason
);

[EventName("AccountUpdateSaga.Completed")]
public record AccountUpdateSagaCompleted(
    string SagaId,
    int SuccessfulUpdates,
    int FailedUpdates
);

public partial class AccountUpdateSaga : Aggregate
{
    private string? _sagaId;
    private List<string> _projectIds = new();
    private Dictionary<string, string> _projectRequestIds = new();  // projectId -> requestId
    private HashSet<string> _completedProjects = new();
    private HashSet<string> _failedProjects = new();
    private SagaStatus _status = SagaStatus.NotStarted;

    private void When(AccountUpdateSagaStarted @event)
    {
        _sagaId = @event.SagaId;
        _projectIds = @event.ProjectIds;
        _status = SagaStatus.InProgress;
    }

    private void When(AccountUpdateSagaProjectUpdateRequested @event)
    {
        _projectRequestIds[@event.ProjectId] = @event.RequestId;
    }

    private void When(AccountUpdateSagaProjectUpdateCompleted @event)
    {
        _completedProjects.Add(@event.ProjectId);
    }

    private void When(AccountUpdateSagaProjectUpdateFailed @event)
    {
        _failedProjects.Add(@event.ProjectId);
    }

    private void When(AccountUpdateSagaCompleted @event)
    {
        _status = SagaStatus.Completed;
    }

    public async Task ExecuteSaga(
        string accountId,
        string newAccountName,
        List<string> projectIds,
        string sagaId)
    {
        // Check if already completed
        if (_status == SagaStatus.Completed && _sagaId == sagaId)
        {
            return; // Already done - idempotent!
        }

        // Start saga
        if (_status == SagaStatus.NotStarted)
        {
            await Stream.Session(context =>
            {
                var metadata = new ActionMetadata(
                    CorrelationId: sagaId,  // ← All events share this
                    CausationId: sagaId,
                    EventOccuredAt: DateTimeOffset.UtcNow
                );

                context.Append(
                    new AccountUpdateSagaStarted(
                        sagaId,
                        accountId,
                        newAccountName,
                        projectIds
                    ),
                    actionMetadata: metadata
                );
                return Fold(context);
            });
        }

        // Process each project
        foreach (var projectId in _projectIds)
        {
            // Skip if already completed or failed
            if (_completedProjects.Contains(projectId) ||
                _failedProjects.Contains(projectId))
            {
                continue;
            }

            // Generate request ID for this project (idempotency key)
            var requestId = _projectRequestIds.ContainsKey(projectId)
                ? _projectRequestIds[projectId]
                : $"{sagaId}:project:{projectId}";

            // Record request
            if (!_projectRequestIds.ContainsKey(projectId))
            {
                await Stream.Session(context =>
                {
                    var metadata = new ActionMetadata(
                        CorrelationId: sagaId,
                        CausationId: sagaId,
                        EventOccuredAt: DateTimeOffset.UtcNow
                    );

                    context.Append(
                        new AccountUpdateSagaProjectUpdateRequested(
                            sagaId,
                            projectId,
                            requestId
                        ),
                        actionMetadata: metadata
                    );
                    return Fold(context);
                });
            }

            // Execute project update with requestId as operationId
            try
            {
                await UpdateProject(projectId, newAccountName, requestId, sagaId);

                // Record success
                await Stream.Session(context =>
                {
                    var metadata = new ActionMetadata(
                        CorrelationId: sagaId,
                        CausationId: requestId,  // ← Caused by the request
                        EventOccuredAt: DateTimeOffset.UtcNow
                    );

                    context.Append(
                        new AccountUpdateSagaProjectUpdateCompleted(
                            sagaId,
                            projectId,
                            requestId
                        ),
                        actionMetadata: metadata
                    );
                    return Fold(context);
                });
            }
            catch (Exception ex)
            {
                // Record failure
                await Stream.Session(context =>
                {
                    var metadata = new ActionMetadata(
                        CorrelationId: sagaId,
                        CausationId: requestId,
                        EventOccuredAt: DateTimeOffset.UtcNow
                    );

                    context.Append(
                        new AccountUpdateSagaProjectUpdateFailed(
                            sagaId,
                            projectId,
                            requestId,
                            ex.Message
                        ),
                        actionMetadata: metadata
                    );
                    return Fold(context);
                });

                throw; // Re-throw to stop saga
            }
        }

        // Complete saga
        await Stream.Session(context =>
        {
            var metadata = new ActionMetadata(
                CorrelationId: sagaId,
                CausationId: sagaId,
                EventOccuredAt: DateTimeOffset.UtcNow
            );

            context.Append(
                new AccountUpdateSagaCompleted(
                    sagaId,
                    _completedProjects.Count,
                    _failedProjects.Count
                ),
                actionMetadata: metadata
            );
            return Fold(context);
        });
    }

    private async Task UpdateProject(
        string projectId,
        string newAccountName,
        string operationId,
        string correlationId)
    {
        // This is the cascading operation with its own sub-steps
        var process = await GetProjectUpdateProcess(projectId);

        await process.UpdateProjectAccount(
            projectId,
            newAccountName,
            operationId  // ← Used for idempotency
        );
    }
}

public enum SagaStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed
}
```

### Causation Chain

```
SagaId: "saga-123"
  │ (CorrelationId for all events)
  │
  ├─> AccountUpdateSagaStarted
  │   CausationId: "saga-123"
  │   CorrelationId: "saga-123"
  │
  ├─> AccountUpdateSagaProjectUpdateRequested (Project A)
  │   CausationId: "saga-123"
  │   CorrelationId: "saga-123"
  │   RequestId: "saga-123:project:A"
  │   │
  │   └─> ProjectAccountUpdateStarted (in Project A aggregate)
  │       CausationId: "saga-123:project:A"
  │       CorrelationId: "saga-123"
  │       │
  │       └─> ProjectAccountNameUpdated
  │           CausationId: "saga-123:project:A:project-update"
  │           CorrelationId: "saga-123"
  │
  └─> AccountUpdateSagaProjectUpdateRequested (Project B)
      CausationId: "saga-123"
      CorrelationId: "saga-123"
      RequestId: "saga-123:project:B"
      │
      └─> ProjectAccountUpdateStarted (in Project B aggregate)
          CausationId: "saga-123:project:B"
          CorrelationId: "saga-123"
```

**Key Points**:
- **CorrelationId**: Same for all events ("saga-123")
- **CausationId**: Changes at each level to track direct cause
- **RequestId**: Becomes the operationId for sub-operations

---

## Complete Working Examples

### Example 1: Batch with Simple Operations

```csharp
public class BatchProcessor
{
    public async Task ProcessCustomerBatch(List<Customer> customers, string batchId)
    {
        for (int i = 0; i < customers.Count; i++)
        {
            var customer = customers[i];

            // Unique operation ID combining batch and index
            var operationId = $"{batchId}-customer-{i}";
            var correlationId = batchId;

            var aggregate = await GetCustomerAggregate(customer.Id);

            await aggregate.UpdateCustomerDetails(
                customer.NewName,
                customer.NewEmail,
                operationId,
                correlationId
            );
        }
    }
}

public partial class Customer : Aggregate
{
    private HashSet<string> _processedOperations = new();
    private string? _name;
    private string? _email;

    private void When(CustomerDetailsUpdated @event)
    {
        _name = @event.NewName;
        _email = @event.NewEmail;

        if (@event.ActionMetadata?.CausationId != null)
        {
            _processedOperations.Add(@event.ActionMetadata.CausationId);
        }
    }

    public async Task UpdateCustomerDetails(
        string newName,
        string newEmail,
        string operationId,
        string correlationId)
    {
        // Idempotency check
        if (_processedOperations.Contains(operationId))
        {
            return; // Already processed
        }

        await Stream.Session(context =>
        {
            var metadata = new ActionMetadata(
                CorrelationId: correlationId,
                CausationId: operationId,
                EventOccuredAt: DateTimeOffset.UtcNow
            );

            context.Append(
                new CustomerDetailsUpdated(newName, newEmail),
                actionMetadata: metadata
            );

            return Fold(context);
        });
    }
}
```

**On retry**: Each customer checks its own `_processedOperations` set. Already-processed customers are skipped.

### Example 2: Batch with Cascading Operations

```csharp
public class ComplexBatchProcessor
{
    public async Task ProcessProjectAccountUpdates(
        List<Project> projects,
        string newAccountName,
        string batchId)
    {
        for (int i = 0; i < projects.Count; i++)
        {
            var project = projects[i];

            // Main operation ID
            var operationId = $"{batchId}-project-{i}";

            // Create/get process aggregate for this operation
            var process = await GetOrCreateProjectUpdateProcess(operationId);

            // Execute with cascading operations
            await process.UpdateProjectAccount(
                project.Id,
                newAccountName,
                operationId
            );
        }
    }
}

// Use the ProjectAccountUpdateProcess from earlier with hierarchical IDs
// operationId: "batch-123-project-5"
//   ├─ "batch-123-project-5:project-update"
//   ├─ "batch-123-project-5:customer-update"
//   └─ "batch-123-project-5:notification"
```

**On retry**: The process aggregate tracks which sub-operations completed, only retrying failed ones.

---

## Best Practices

### 1. Always Use Both IDs

```csharp
// ✅ GOOD: Both IDs provided
var metadata = new ActionMetadata(
    CorrelationId: batchId,           // Groups entire batch
    CausationId: operationId,         // Unique per operation
    EventOccuredAt: DateTimeOffset.UtcNow
);

// ❌ BAD: Missing causation - can't track idempotency
var metadata = new ActionMetadata(
    CorrelationId: batchId,
    EventOccuredAt: DateTimeOffset.UtcNow
);
```

### 2. Make Operation IDs Deterministic

```csharp
// ✅ GOOD: Deterministic - same inputs = same ID
var operationId = $"{batchId}-{aggregateType}-{aggregateId}-{index}";

// ❌ BAD: Random - can't check idempotency
var operationId = Guid.NewGuid().ToString();
```

### 3. Use Hierarchical IDs for Cascading Operations

```csharp
// ✅ GOOD: Clear hierarchy
var mainOperationId = "batch-123-item-5";
var subOp1 = $"{mainOperationId}:step-1";
var subOp2 = $"{mainOperationId}:step-2";
var subOp3 = $"{mainOperationId}:step-3";

// ❌ BAD: Flat structure loses relationship
var subOp1 = Guid.NewGuid().ToString();
var subOp2 = Guid.NewGuid().ToString();
```

### 4. Track Completed Operations in Aggregate State

```csharp
public partial class MyAggregate : Aggregate
{
    // ✅ GOOD: Track processed operations
    private HashSet<string> _processedOperations = new();

    private void When(MyEvent @event)
    {
        // Apply business logic
        _value = @event.NewValue;

        // Track operation
        if (@event.ActionMetadata?.CausationId != null)
        {
            _processedOperations.Add(@event.ActionMetadata.CausationId);
        }
    }

    public async Task DoSomething(string operationId)
    {
        // Check idempotency
        if (_processedOperations.Contains(operationId))
        {
            return; // Already done
        }

        // Execute operation...
    }
}
```

### 5. Use Separate Process Aggregates for Complex Operations

```csharp
// ✅ GOOD: Dedicated aggregate tracks multi-step process
public class OrderFulfillmentProcess : Aggregate
{
    private HashSet<string> _completedSteps = new();
    // ... handles orchestration
}

// ❌ BAD: Mixing process orchestration into business aggregate
public class Order : Aggregate
{
    // Business state mixed with process tracking
    private HashSet<string> _completedSteps = new();
    private Dictionary<string, bool> _notificationsSent = new();
    // ... becomes messy
}
```

### 6. Preserve CorrelationId Across Boundaries

```csharp
// When calling external systems or other aggregates
public async Task ProcessOrder(string orderId, string correlationId)
{
    // Pass correlationId through entire call chain
    await reserveInventory.Execute(orderId, correlationId);
    await capturePayment.Execute(orderId, correlationId);
    await scheduleShipping.Execute(orderId, correlationId);

    // All events will have same CorrelationId
    // Easy to trace entire transaction
}
```

---

## Troubleshooting Common Scenarios

### Scenario 1: Operation Retried but Sub-Operation Missing

**Problem**:
```
Operation "batch-123-item-5" is retried
Main aggregate says: "Already processed" (returns early)
But sub-operation 2 never completed!
```

**Solution**: Use hierarchical operation tracking

```csharp
// Track sub-operations separately
private HashSet<string> _completedSubOperations = new();

public async Task Execute(string operationId)
{
    // Don't just check main operation
    // if (_processedOperations.Contains(operationId)) return;

    // Instead, check each sub-operation
    var subOp1 = $"{operationId}:step-1";
    if (!_completedSubOperations.Contains(subOp1))
    {
        await ExecuteStep1(subOp1);
        _completedSubOperations.Add(subOp1);
    }

    var subOp2 = $"{operationId}:step-2";
    if (!_completedSubOperations.Contains(subOp2))
    {
        await ExecuteStep2(subOp2);
        _completedSubOperations.Add(subOp2);
    }
}
```

### Scenario 2: Lost Track of Which Sub-Operations Completed

**Problem**: Aggregate crashed mid-operation, lost in-memory state of what completed.

**Solution**: Record sub-operation completion as events

```csharp
[EventName("Operation.SubStepCompleted")]
public record OperationSubStepCompleted(
    string OperationId,
    string SubStepId,
    DateTimeOffset CompletedAt
);

private void When(OperationSubStepCompleted @event)
{
    _completedSubSteps.Add(@event.SubStepId);
}

// After each sub-step
await Stream.Session(context =>
{
    context.Append(new OperationSubStepCompleted(
        operationId,
        subStepId,
        DateTimeOffset.UtcNow
    ));
    return Fold(context);
});
```

### Scenario 3: Same CausationId Used for Different Operations

**Problem**: Two different operations accidentally use the same causationId.

**Prevention**: Include operation type in the ID

```csharp
// ✅ GOOD: Operation type in ID
var updateOperationId = $"update-{batchId}-{index}";
var deleteOperationId = $"delete-{batchId}-{index}";

// ❌ BAD: Same structure for different operations
var operationId = $"{batchId}-{index}"; // Used for both update and delete
```

### Scenario 4: External System Called Multiple Times

**Problem**: External system doesn't support idempotency, gets called multiple times.

**Solution**: Track external calls in aggregate

```csharp
[EventName("ExternalSystemCallCompleted")]
public record ExternalSystemCallCompleted(
    string CallId,
    string SystemName,
    string OperationId,
    DateTimeOffset CompletedAt
);

private HashSet<string> _completedExternalCalls = new();

private void When(ExternalSystemCallCompleted @event)
{
    _completedExternalCalls.Add(@event.CallId);
}

public async Task CallExternalSystem(string operationId)
{
    var callId = $"{operationId}:external-call";

    if (_completedExternalCalls.Contains(callId))
    {
        return; // Already called - don't call again!
    }

    // Make external call
    await externalSystem.DoSomething();

    // Record completion
    await Stream.Session(context =>
    {
        context.Append(new ExternalSystemCallCompleted(
            callId,
            "ExternalSystem",
            operationId,
            DateTimeOffset.UtcNow
        ));
        return Fold(context);
    });
}
```

---

## Summary

### Key Takeaways

1. **CorrelationId**: Groups all events in a business transaction
2. **CausationId**: Identifies the direct cause of each event (used for idempotency)
3. **Simple operations**: Use `CausationId` = `operationId` for idempotency
4. **Cascading operations**: Use hierarchical IDs: `{parentId}:{subOperation}`
5. **Track completed operations**: Store in aggregate state, check before executing
6. **Record sub-operation completion**: Emit events for each sub-step
7. **Make IDs deterministic**: Same inputs = same ID for idempotency to work

### Decision Guide

**Do you have cascading operations?**

**NO** → Use simple approach:
```csharp
var operationId = $"{batchId}-{index}";
if (_processedOperations.Contains(operationId)) return;
```

**YES** → Use one of these:
- **Hierarchical IDs**: `{operationId}:sub-step-name`
- **Process Aggregate**: Dedicated aggregate tracks sub-operations
- **Saga Pattern**: For complex multi-aggregate coordination

---

**Document Version:** 1.0
**Last Updated:** 2025-11-04
**Author:** ErikLieben.FA.ES Documentation Team
**Related Docs**:
- `handling-failures-and-rollbacks.md` - General failure patterns
- `batch-processing-failure-recovery.md` - Batch-specific failure recovery
