# Batch Processing and Failure Recovery in Event Sourcing

## Table of Contents
- [Introduction](#introduction)
- [Fail Forward vs Rollback: The Event Sourcing Philosophy](#fail-forward-vs-rollback-the-event-sourcing-philosophy)
- [The Challenge: Mid-Batch Failures](#the-challenge-mid-batch-failures)
- [Understanding Failure Types](#understanding-failure-types)
- [Decision Framework: Resume vs Restart](#decision-framework-resume-vs-restart)
- [Strategy 1: Checkpoint-Based Recovery with Smart Resume](#strategy-1-checkpoint-based-recovery-with-smart-resume)
- [Strategy 2: Transactional Batch with All-or-Nothing Semantics](#strategy-2-transactional-batch-with-all-or-nothing-semantics)
- [Strategy 3: Resilient Batch with Skip-on-Error](#strategy-3-resilient-batch-with-skip-on-error)
- [Strategy 4: Hybrid Approach with Failure Classification](#strategy-4-hybrid-approach-with-failure-classification)
- [Best Practices from Industry](#best-practices-from-industry)
- [Production Considerations](#production-considerations)
- [Testing Failure Scenarios](#testing-failure-scenarios)

---

## Introduction

Batch processing in event-sourced systems presents unique challenges when failures occur mid-process. Unlike traditional CRUD systems where you can rollback a transaction, event sourcing requires careful orchestration to maintain consistency and auditability while handling partial batch completion.

This document provides comprehensive guidance on handling batch failures, with a focus on the critical decision: **should you resume from the failure point or restart from the beginning?**

---

## Fail Forward vs Rollback: The Event Sourcing Philosophy

### Traditional Rollback (Backward Recovery)

In traditional CRUD systems, when something fails, you **rollback** - literally reversing changes:

```
Traditional Database Transaction:
1. BEGIN TRANSACTION
2. UPDATE record 1   ✅
3. UPDATE record 2   ✅
4. UPDATE record 3   ❌ FAILS
5. ROLLBACK         ← Undo changes to records 1 and 2
```

**Result**: Database returns to state before transaction began. History is erased.

### Fail Forward (Forward Recovery)

In event sourcing, you cannot delete history. Instead, you **fail forward** - recording the failure and compensating:

```
Event Sourcing:
1. ProjectUpdated (project 1)  ✅ Event written
2. ProjectUpdated (project 2)  ✅ Event written
3. ProjectUpdated (project 3)  ❌ FAILS
4. Options:
   a) Append compensating events (ProjectUpdateCancelled for 1 & 2)
   b) Resume from checkpoint (retry project 3)
   c) Skip failed item, continue to project 4
   d) Mark batch as failed, require manual intervention
```

**Result**: Full audit trail preserved. You move forward by adding more events, not by erasing history.

### Why Fail Forward?

| Aspect | Rollback (Traditional) | Fail Forward (Event Sourcing) |
|--------|------------------------|-------------------------------|
| **History** | Erased | Preserved |
| **Audit** | No trace of failure | Complete failure trail |
| **Debugging** | Hard - no evidence | Easy - all attempts recorded |
| **Compliance** | Risky - missing history | Safe - immutable record |
| **Recovery** | Start over from scratch | Resume from checkpoint |
| **Distributed Systems** | Difficult/impossible | Natural fit |

### Event Sourcing = Always Fail Forward

```csharp
// ❌ IMPOSSIBLE in Event Sourcing
public async Task Rollback()
{
    // Cannot delete events - they are immutable facts!
    eventStore.DeleteEvent(event1);  // NOT ALLOWED
    eventStore.DeleteEvent(event2);  // NOT ALLOWED
}

// ✅ CORRECT in Event Sourcing
public async Task FailForward_Option1_Compensate()
{
    // Append compensating events
    await stream.Session(context =>
    {
        context.Append(new ProjectUpdateCancelled(
            projectId: "project-1",
            reason: "Batch failed at project 3",
            processId: batchId
        ));
        context.Append(new ProjectUpdateCancelled(
            projectId: "project-2",
            reason: "Batch failed at project 3",
            processId: batchId
        ));
        return Fold(context);
    });
}

// ✅ CORRECT in Event Sourcing
public async Task FailForward_Option2_Resume()
{
    // Record the failure, then continue forward
    await stream.Session(context =>
    {
        context.Append(new BatchItemFailed(
            batchId: batchId,
            itemIndex: 3,
            reason: "Transient failure",
            retryable: true
        ));
        context.Append(new BatchResumeRequested(
            batchId: batchId,
            resumeFromIndex: 3,  // Continue forward from failure point
            reason: "Retrying after transient failure"
        ));
        return Fold(context);
    });
}

// ✅ CORRECT in Event Sourcing
public async Task FailForward_Option3_Skip()
{
    // Record the failure, move forward past it
    await stream.Session(context =>
    {
        context.Append(new BatchItemFailed(
            batchId: batchId,
            itemIndex: 3,
            reason: "Permanent validation error"
        ));
        context.Append(new BatchItemSkipped(
            batchId: batchId,
            itemIndex: 3,
            reason: "Continuing batch despite permanent failure"
        ));
        // Continue processing from index 4 →
        return Fold(context);
    });
}
```

### The Strategies in This Document Are All "Fail Forward"

1. **Checkpoint-Based Recovery** = Fail forward by resuming from last good state
2. **Transactional Batch with Provisional Events** = Fail forward by cancelling pending changes
3. **Resilient Batch** = Fail forward by skipping failures and continuing
4. **Hybrid Approach** = Fail forward using different strategies based on failure type

**None of these strategies delete events.** They all move forward by appending new events.

### Practical Example: Your 16 Objects Scenario

**If you restart from beginning after failing at object 14:**

```
Attempt 1:
  Object 1-13: ProjectUpdated events written ✅
  Object 14: FAILED ❌

Decision: Restart from beginning

Attempt 2:
  Object 1: ProjectUpdated event written AGAIN
  Object 2: ProjectUpdated event written AGAIN
  ...
  Object 16: ProjectUpdated event written ✅

Event stream now contains:
  - 13 ProjectUpdated events (attempt 1)
  - 16 ProjectUpdated events (attempt 2)
  Total: 29 events for 16 objects
```

**This is still "fail forward"** - you didn't delete the first 13 events, you added 16 more events. The idempotency in the aggregate's `When` methods ensures the state is correct despite duplicate events.

**If you resume from checkpoint:**

```
Attempt 1:
  Object 1-13: ProjectUpdated events written ✅
  Object 14: FAILED ❌
  Checkpoint saved at object 13 ✅

Decision: Resume from checkpoint

Attempt 2 (resume):
  Object 14: ProjectUpdated event written ✅
  Object 15-16: ProjectUpdated events written ✅

Event stream contains:
  - 13 ProjectUpdated events (attempt 1)
  - 1 BatchCheckpoint event
  - 1 BatchResumeRequested event
  - 3 ProjectUpdated events (attempt 2, objects 14-16)
  Total: 18 events for 16 objects
```

**Also "fail forward"** - but more efficient because you recorded progress and resumed.

### Key Insight

> **"Fail forward" doesn't mean "never retry" or "never compensate". It means "never delete history". All recovery strategies in event sourcing are about moving forward in time by appending new events, whether those events are retries, compensations, or failure records.**

---

## The Challenge: Mid-Batch Failures

### Scenario

You're processing a batch of 16 objects. The batch fails at object 14. Now you face uncertainty:

```
Batch Processing: Update 16 Customer Records
  Object 1:  ✅ Success
  Object 2:  ✅ Success
  Object 3:  ✅ Success
  ...
  Object 13: ✅ Success
  Object 14: ❌ FAILED - "Database connection timeout"
  Object 15: ⏸️  Not processed
  Object 16: ⏸️  Not processed

Questions:
1. Was the failure transient (network glitch) or permanent (invalid data)?
2. Can we safely resume from object 14, or must we restart from object 1?
3. Should the batch be marked as "failed" to block further processing?
4. How do we prevent duplicate processing if we retry?
5. What if object 14 was partially processed?
```

### Why This Is Hard

1. **State Uncertainty**: You don't know if object 14 was partially processed
2. **Failure Classification**: Is this a transient error (retry will work) or permanent (needs intervention)?
3. **Consistency Requirements**: Some batches require all-or-nothing atomicity
4. **Idempotency**: Retrying must not create duplicate events
5. **Audit Trail**: Every attempt and failure must be recorded

---

## Understanding Failure Types

Before deciding on a recovery strategy, classify the failure type.

### Transient Failures

**Characteristics**: Temporary issues that may resolve on their own

**Examples**:
- Network timeouts or connection drops
- Temporary service unavailability (503 errors)
- Database deadlocks or lock timeouts
- Resource exhaustion (CPU spike, memory pressure)
- Rate limiting / throttling
- Concurrent access conflicts

**Recovery Strategy**: ✅ **Retry with exponential backoff, resume from checkpoint**

**Example Code**:
```csharp
public class TransientFailureDetector
{
    public static bool IsTransient(Exception ex)
    {
        return ex switch
        {
            TimeoutException => true,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.ServiceUnavailable => true,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.TooManyRequests => true,
            SqlException sqlEx when IsTransientSqlError(sqlEx.Number) => true,
            SocketException => true,
            _ => false
        };
    }

    private static bool IsTransientSqlError(int errorNumber)
    {
        // SQL Server transient error codes
        return errorNumber switch
        {
            -2 => true,      // Timeout
            1205 => true,    // Deadlock
            40197 => true,   // Service unavailable
            40501 => true,   // Service busy
            40613 => true,   // Database unavailable
            _ => false
        };
    }
}
```

---

### Permanent Failures

**Characteristics**: Persistent issues that won't resolve with retries

**Examples**:
- Invalid data format or schema violations
- Business rule validation failures
- Authorization/authentication errors (401, 403)
- Missing required resources (404)
- Corrupted or inconsistent data
- Logic errors in code
- Resource not found errors

**Recovery Strategy**: ❌ **Do not retry automatically. Log for manual review, skip item, or fail batch**

**Example Code**:
```csharp
public class PermanentFailureDetector
{
    public static bool IsPermanent(Exception ex)
    {
        return ex switch
        {
            ArgumentException => true,
            ArgumentNullException => true,
            InvalidOperationException => true,
            FormatException => true,
            UnauthorizedAccessException => true,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.Unauthorized => true,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.Forbidden => true,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.NotFound => true,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.BadRequest => true,
            ValidationException => true,
            _ => false
        };
    }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
```

---

### Ambiguous Failures

**Characteristics**: Cannot determine if transient or permanent without more context

**Examples**:
- Generic `Exception` with unclear message
- External service errors without status codes
- Infrastructure failures
- Unknown error states

**Recovery Strategy**: ⚠️ **Retry with limited attempts, then escalate to manual review**

---

## Decision Framework: Resume vs Restart

Use this decision tree to determine the appropriate recovery strategy:

```
┌─────────────────────────────┐
│  Batch Failed at Object N   │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│ Classify Failure Type       │
└──────────┬──────────────────┘
           │
           ├─────────────────────────────────────┐
           │                                     │
           ▼                                     ▼
    ┌─────────────┐                      ┌─────────────┐
    │ TRANSIENT   │                      │ PERMANENT   │
    └──────┬──────┘                      └──────┬──────┘
           │                                     │
           ▼                                     ▼
    ┌─────────────────────────────┐      ┌──────────────────────────┐
    │ Are operations idempotent?  │      │ Can item be skipped?     │
    └──────┬──────────────────────┘      └──────┬───────────────────┘
           │                                     │
      ┌────┴────┐                           ┌───┴────┐
      │         │                           │        │
     Yes       No                          Yes       No
      │         │                           │        │
      ▼         ▼                           ▼        ▼
  ┌────────┐ ┌──────────┐            ┌─────────┐ ┌───────────┐
  │ RESUME │ │ RESTART  │            │  SKIP   │ │ FAIL BATCH│
  │ from N │ │ from 1   │            │  ITEM   │ │   STOP    │
  └────────┘ └──────────┘            └─────────┘ └───────────┘
      │         │                           │         │
      │         │                           │         │
      ▼         ▼                           ▼         ▼
  ┌────────────────────────────────────────────────────┐
  │  Apply Strategy (see below)                        │
  └────────────────────────────────────────────────────┘
```

### Decision Criteria

| Criteria | Resume from Checkpoint | Restart from Beginning |
|----------|------------------------|------------------------|
| Failure Type | Transient | Permanent (data-dependent) |
| Idempotency | ✅ Operations are idempotent | ❌ Not idempotent / uncertain |
| Batch Size | Large (100+) | Small (< 20) |
| Processing Cost | High (expensive per item) | Low (cheap per item) |
| Consistency Requirement | Eventually consistent OK | Strong consistency required |
| State Tracking | Has reliable checkpoints | No checkpoint mechanism |
| Business Rules | Partial success acceptable | All-or-nothing required |

---

## Strategy 1: Checkpoint-Based Recovery with Smart Resume

**Best for**: Large batches (50+), expensive operations, transient failures

**Key Features**:
- Saves progress at regular intervals
- Classifies failures as transient or permanent
- Resumes from last checkpoint for transient failures
- Supports manual intervention for permanent failures

### Event Definitions

```csharp
[EventName("Batch.Started")]
public record BatchStarted(
    string BatchId,
    string BatchType,
    int TotalItems,
    DateTimeOffset StartedAt,
    Dictionary<string, string> BatchMetadata
);

[EventName("Batch.ItemProcessing")]
public record BatchItemProcessing(
    string BatchId,
    int ItemIndex,
    string ItemId,
    DateTimeOffset ProcessingAt
);

[EventName("Batch.ItemCompleted")]
public record BatchItemCompleted(
    string BatchId,
    int ItemIndex,
    string ItemId,
    DateTimeOffset CompletedAt
);

[EventName("Batch.ItemFailed")]
public record BatchItemFailed(
    string BatchId,
    int ItemIndex,
    string ItemId,
    string FailureReason,
    string FailureType,  // "Transient" or "Permanent"
    string ExceptionType,
    string StackTrace,
    DateTimeOffset FailedAt
);

[EventName("Batch.CheckpointReached")]
public record BatchCheckpointReached(
    string BatchId,
    int CheckpointIndex,
    int SuccessfulItems,
    int FailedItems,
    DateTimeOffset CheckpointAt
);

[EventName("Batch.Failed")]
public record BatchFailed(
    string BatchId,
    int FailedAtIndex,
    string Reason,
    int SuccessfulItems,
    int FailedItems,
    DateTimeOffset FailedAt
);

[EventName("Batch.Completed")]
public record BatchCompleted(
    string BatchId,
    int TotalProcessed,
    int SuccessfulItems,
    int FailedItems,
    int SkippedItems,
    DateTimeOffset CompletedAt
);

[EventName("Batch.ResumeRequested")]
public record BatchResumeRequested(
    string BatchId,
    int ResumeFromIndex,
    string Reason,
    DateTimeOffset RequestedAt
);

[EventName("Batch.RestartRequested")]
public record BatchRestartRequested(
    string BatchId,
    string Reason,
    DateTimeOffset RequestedAt
);

[EventName("Batch.Blocked")]
public record BatchBlocked(
    string BatchId,
    string Reason,
    bool RequiresManualIntervention,
    DateTimeOffset BlockedAt
);
```

### Aggregate Implementation

```csharp
public partial class BatchProcess : Aggregate
{
    private string? _batchId;
    private BatchStatus _status = BatchStatus.NotStarted;
    private int _totalItems;
    private int _currentIndex;
    private int _lastCheckpointIndex;
    private int _successfulItems;
    private int _failedItems;
    private int _skippedItems;
    private Dictionary<int, ItemProcessingResult> _itemResults = new();
    private List<int> _permanentFailures = new();
    private int _consecutiveTransientFailures = 0;
    private bool _isBlocked;

    // Event handlers
    private void When(BatchStarted @event)
    {
        _batchId = @event.BatchId;
        _totalItems = @event.TotalItems;
        _status = BatchStatus.Running;
        _currentIndex = 0;
    }

    private void When(BatchItemProcessing @event)
    {
        _currentIndex = @event.ItemIndex;
    }

    private void When(BatchItemCompleted @event)
    {
        _successfulItems++;
        _itemResults[@event.ItemIndex] = ItemProcessingResult.Success;
        _consecutiveTransientFailures = 0; // Reset counter on success
    }

    private void When(BatchItemFailed @event)
    {
        _failedItems++;
        _itemResults[@event.ItemIndex] = ItemProcessingResult.Failed;

        if (@event.FailureType == "Permanent")
        {
            _permanentFailures.Add(@event.ItemIndex);
        }
        else if (@event.FailureType == "Transient")
        {
            _consecutiveTransientFailures++;
        }
    }

    private void When(BatchCheckpointReached @event)
    {
        _lastCheckpointIndex = @event.CheckpointIndex;
    }

    private void When(BatchFailed @event)
    {
        _status = BatchStatus.Failed;
    }

    private void When(BatchCompleted @event)
    {
        _status = BatchStatus.Completed;
    }

    private void When(BatchResumeRequested @event)
    {
        _status = BatchStatus.Resuming;
        _currentIndex = @event.ResumeFromIndex;
        _consecutiveTransientFailures = 0;
    }

    private void When(BatchRestartRequested @event)
    {
        _status = BatchStatus.Restarting;
        _currentIndex = 0;
        _successfulItems = 0;
        _failedItems = 0;
        _itemResults.Clear();
        _permanentFailures.Clear();
        _consecutiveTransientFailures = 0;
    }

    private void When(BatchBlocked @event)
    {
        _isBlocked = true;
        _status = BatchStatus.Blocked;
    }

    // Business logic
    public async Task<BatchExecutionResult> ExecuteBatch<T>(
        List<T> items,
        Func<T, int, Task> processItem,
        BatchOptions options)
    {
        if (_isBlocked)
        {
            throw new InvalidOperationException(
                $"Batch {_batchId} is blocked and requires manual intervention");
        }

        var batchId = _batchId ?? Guid.NewGuid().ToString();

        // Start or resume
        if (_status == BatchStatus.NotStarted)
        {
            await Stream.Session(context =>
            {
                context.Append(new BatchStarted(
                    batchId,
                    typeof(T).Name,
                    items.Count,
                    DateTimeOffset.UtcNow,
                    options.Metadata
                ));
                return Fold(context);
            });
        }

        // Process items
        for (int i = _currentIndex; i < items.Count; i++)
        {
            // Check if we should stop due to too many consecutive failures
            if (_consecutiveTransientFailures >= options.MaxConsecutiveTransientFailures)
            {
                await RecordFailureAndBlock(
                    i,
                    $"Too many consecutive transient failures ({_consecutiveTransientFailures})",
                    requiresManualIntervention: true
                );
                return CreateResult();
            }

            var item = items[i];

            // Record processing start
            await Stream.Session(context =>
            {
                context.Append(new BatchItemProcessing(
                    batchId,
                    i,
                    GetItemId(item),
                    DateTimeOffset.UtcNow
                ));
                return Fold(context);
            });

            // Process the item with retry logic
            var result = await ProcessItemWithRetry(
                item,
                i,
                processItem,
                options
            );

            // Record result
            if (result.IsSuccess)
            {
                await Stream.Session(context =>
                {
                    context.Append(new BatchItemCompleted(
                        batchId,
                        i,
                        GetItemId(item),
                        DateTimeOffset.UtcNow
                    ));
                    return Fold(context);
                });
            }
            else
            {
                await Stream.Session(context =>
                {
                    context.Append(new BatchItemFailed(
                        batchId,
                        i,
                        GetItemId(item),
                        result.FailureReason!,
                        result.FailureType!,
                        result.ExceptionType!,
                        result.StackTrace ?? "",
                        DateTimeOffset.UtcNow
                    ));
                    return Fold(context);
                });

                // Handle failure based on type and policy
                if (result.FailureType == "Permanent")
                {
                    if (options.SkipPermanentFailures)
                    {
                        _skippedItems++;
                        // Continue processing next item
                    }
                    else
                    {
                        // Stop batch on permanent failure
                        await RecordFailureAndBlock(
                            i,
                            $"Permanent failure at item {i}: {result.FailureReason}",
                            requiresManualIntervention: true
                        );
                        return CreateResult();
                    }
                }
                else if (result.FailureType == "Transient")
                {
                    if (_consecutiveTransientFailures >= options.MaxConsecutiveTransientFailures)
                    {
                        // Already checked above, but safety check
                        await RecordFailureAndBlock(
                            i,
                            $"Too many consecutive transient failures",
                            requiresManualIntervention: true
                        );
                        return CreateResult();
                    }
                    // Continue - will retry on resume
                }
            }

            // Checkpoint periodically
            if ((i + 1) % options.CheckpointInterval == 0)
            {
                await Stream.Session(context =>
                {
                    context.Append(new BatchCheckpointReached(
                        batchId,
                        i,
                        _successfulItems,
                        _failedItems,
                        DateTimeOffset.UtcNow
                    ));
                    return Fold(context);
                });
            }
        }

        // Complete
        await Stream.Session(context =>
        {
            context.Append(new BatchCompleted(
                batchId,
                items.Count,
                _successfulItems,
                _failedItems,
                _skippedItems,
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });

        return CreateResult();
    }

    private async Task<ItemResult> ProcessItemWithRetry<T>(
        T item,
        int index,
        Func<T, int, Task> processItem,
        BatchOptions options)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < options.MaxRetries; attempt++)
        {
            try
            {
                await processItem(item, index);
                return ItemResult.Success();
            }
            catch (Exception ex)
            {
                lastException = ex;

                // Classify failure
                if (PermanentFailureDetector.IsPermanent(ex))
                {
                    return ItemResult.Failure(
                        ex.Message,
                        "Permanent",
                        ex.GetType().Name,
                        ex.StackTrace
                    );
                }

                if (TransientFailureDetector.IsTransient(ex))
                {
                    // Wait before retry with exponential backoff
                    if (attempt < options.MaxRetries - 1)
                    {
                        var delay = TimeSpan.FromSeconds(
                            Math.Pow(2, attempt) * options.BaseRetryDelaySeconds
                        );
                        await Task.Delay(delay);
                        continue;
                    }

                    // Max retries exhausted - treat as transient failure
                    return ItemResult.Failure(
                        ex.Message,
                        "Transient",
                        ex.GetType().Name,
                        ex.StackTrace
                    );
                }

                // Ambiguous failure - retry with limit
                if (attempt < options.MaxRetries - 1)
                {
                    var delay = TimeSpan.FromSeconds(
                        Math.Pow(2, attempt) * options.BaseRetryDelaySeconds
                    );
                    await Task.Delay(delay);
                    continue;
                }

                // Treat as permanent after exhausting retries
                return ItemResult.Failure(
                    ex.Message,
                    "Permanent",
                    ex.GetType().Name,
                    ex.StackTrace
                );
            }
        }

        // Should not reach here, but safety fallback
        return ItemResult.Failure(
            lastException?.Message ?? "Unknown error",
            "Permanent",
            lastException?.GetType().Name ?? "Unknown",
            lastException?.StackTrace
        );
    }

    private async Task RecordFailureAndBlock(
        int failedAtIndex,
        string reason,
        bool requiresManualIntervention)
    {
        await Stream.Session(context =>
        {
            context.Append(new BatchFailed(
                _batchId!,
                failedAtIndex,
                reason,
                _successfulItems,
                _failedItems,
                DateTimeOffset.UtcNow
            ));

            context.Append(new BatchBlocked(
                _batchId!,
                reason,
                requiresManualIntervention,
                DateTimeOffset.UtcNow
            ));

            return Fold(context);
        });
    }

    public async Task Resume(string reason)
    {
        if (!_isBlocked && _status != BatchStatus.Failed)
        {
            throw new InvalidOperationException(
                "Can only resume blocked or failed batches");
        }

        if (_permanentFailures.Any())
        {
            throw new InvalidOperationException(
                $"Cannot resume - batch has permanent failures at items: " +
                string.Join(", ", _permanentFailures));
        }

        await Stream.Session(context =>
        {
            context.Append(new BatchResumeRequested(
                _batchId!,
                _lastCheckpointIndex,
                reason,
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });

        _isBlocked = false;
    }

    public async Task Restart(string reason)
    {
        if (_status == BatchStatus.Running)
        {
            throw new InvalidOperationException(
                "Cannot restart a running batch");
        }

        await Stream.Session(context =>
        {
            context.Append(new BatchRestartRequested(
                _batchId!,
                reason,
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });

        _isBlocked = false;
    }

    private string GetItemId<T>(T item)
    {
        // Try to get an ID property via reflection or use hash
        var idProp = typeof(T).GetProperty("Id") ?? typeof(T).GetProperty("ItemId");
        if (idProp != null)
        {
            return idProp.GetValue(item)?.ToString() ?? item!.GetHashCode().ToString();
        }
        return item!.GetHashCode().ToString();
    }

    private BatchExecutionResult CreateResult()
    {
        return new BatchExecutionResult(
            _batchId!,
            _status,
            _successfulItems,
            _failedItems,
            _skippedItems,
            _isBlocked,
            _permanentFailures
        );
    }
}

// Supporting types
public enum BatchStatus
{
    NotStarted,
    Running,
    Failed,
    Completed,
    Blocked,
    Resuming,
    Restarting
}

public enum ItemProcessingResult
{
    Success,
    Failed,
    Skipped
}

public class BatchOptions
{
    public int CheckpointInterval { get; set; } = 10;
    public int MaxRetries { get; set; } = 3;
    public double BaseRetryDelaySeconds { get; set; } = 2.0;
    public int MaxConsecutiveTransientFailures { get; set; } = 5;
    public bool SkipPermanentFailures { get; set; } = false;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public record ItemResult(
    bool IsSuccess,
    string? FailureReason,
    string? FailureType,
    string? ExceptionType,
    string? StackTrace)
{
    public static ItemResult Success() =>
        new ItemResult(true, null, null, null, null);

    public static ItemResult Failure(
        string reason,
        string failureType,
        string exceptionType,
        string? stackTrace) =>
        new ItemResult(false, reason, failureType, exceptionType, stackTrace);
}

public record BatchExecutionResult(
    string BatchId,
    BatchStatus Status,
    int SuccessfulItems,
    int FailedItems,
    int SkippedItems,
    bool IsBlocked,
    List<int> PermanentFailures
);
```

### Usage Example

```csharp
// Create batch processor
var batchProcess = new BatchProcess();
await batchProcess.InitializeStream(eventStream);

// Define items to process
var customers = await GetCustomersToUpdate(); // 16 customers

// Define processing logic
async Task ProcessCustomer(Customer customer, int index)
{
    // Your business logic here
    await customerService.UpdateCustomerAccount(customer.Id, newAccountData);
}

// Execute with options
var options = new BatchOptions
{
    CheckpointInterval = 5,  // Checkpoint every 5 items
    MaxRetries = 3,
    BaseRetryDelaySeconds = 2.0,
    MaxConsecutiveTransientFailures = 3,
    SkipPermanentFailures = false,  // Stop on permanent failures
    Metadata = new Dictionary<string, string>
    {
        ["AccountChangeId"] = accountChangeId,
        ["TriggeredBy"] = userId
    }
};

var result = await batchProcess.ExecuteBatch(
    customers,
    ProcessCustomer,
    options
);

// Check result
if (result.IsBlocked)
{
    Console.WriteLine($"Batch blocked. Permanent failures at: {string.Join(", ", result.PermanentFailures)}");

    // Manual intervention: Fix data issue, then:
    // await batchProcess.Resume("Data issue resolved");
    // Or restart from beginning:
    // await batchProcess.Restart("Starting fresh after data cleanup");
}
else if (result.Status == BatchStatus.Completed)
{
    Console.WriteLine($"Batch completed. Success: {result.SuccessfulItems}, Failed: {result.FailedItems}");
}
```

---

## Strategy 2: Transactional Batch with All-or-Nothing Semantics

**Best for**: Small batches (< 20), strong consistency requirements, cheap operations

**Key Features**:
- Uses provisional events (like two-phase commit)
- All items must succeed or all are cancelled
- No partial completion
- Clean rollback on failure

### Event Definitions

```csharp
[EventName("TransactionalBatch.Started")]
public record TransactionalBatchStarted(
    string BatchId,
    int TotalItems,
    DateTimeOffset StartedAt
);

[EventName("TransactionalBatch.ItemProvisioned")]
public record TransactionalBatchItemProvisioned(
    string BatchId,
    int ItemIndex,
    string ItemId,
    DateTimeOffset ProvisionedAt
);

[EventName("TransactionalBatch.AllProvisioned")]
public record TransactionalBatchAllProvisioned(
    string BatchId,
    int TotalProvisioned,
    DateTimeOffset AllProvisionedAt
);

[EventName("TransactionalBatch.Committed")]
public record TransactionalBatchCommitted(
    string BatchId,
    int TotalCommitted,
    DateTimeOffset CommittedAt
);

[EventName("TransactionalBatch.Failed")]
public record TransactionalBatchFailed(
    string BatchId,
    int FailedAtIndex,
    string ItemId,
    string Reason,
    DateTimeOffset FailedAt
);

[EventName("TransactionalBatch.RolledBack")]
public record TransactionalBatchRolledBack(
    string BatchId,
    int ItemsRolledBack,
    string Reason,
    DateTimeOffset RolledBackAt
);
```

### Implementation

```csharp
public partial class TransactionalBatch : Aggregate
{
    private string? _batchId;
    private TransactionalBatchStatus _status = TransactionalBatchStatus.NotStarted;
    private int _totalItems;
    private HashSet<int> _provisionedItems = new();

    private void When(TransactionalBatchStarted @event)
    {
        _batchId = @event.BatchId;
        _totalItems = @event.TotalItems;
        _status = TransactionalBatchStatus.Provisioning;
    }

    private void When(TransactionalBatchItemProvisioned @event)
    {
        _provisionedItems.Add(@event.ItemIndex);
    }

    private void When(TransactionalBatchAllProvisioned @event)
    {
        _status = TransactionalBatchStatus.AllProvisioned;
    }

    private void When(TransactionalBatchCommitted @event)
    {
        _status = TransactionalBatchStatus.Committed;
    }

    private void When(TransactionalBatchFailed @event)
    {
        _status = TransactionalBatchStatus.Failed;
    }

    private void When(TransactionalBatchRolledBack @event)
    {
        _status = TransactionalBatchStatus.RolledBack;
    }

    public async Task<TransactionalBatchResult> ExecuteTransactional<T>(
        List<T> items,
        Func<T, int, string, Task> provisionItem,
        Func<T, int, string, Task> commitItem,
        Func<T, int, string, Task> rollbackItem)
    {
        var batchId = Guid.NewGuid().ToString();

        // Start
        await Stream.Session(context =>
        {
            context.Append(new TransactionalBatchStarted(
                batchId,
                items.Count,
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });

        // Phase 1: Provision all items
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var itemId = GetItemId(item);

                await provisionItem(item, i, batchId);

                await Stream.Session(context =>
                {
                    context.Append(new TransactionalBatchItemProvisioned(
                        batchId,
                        i,
                        itemId,
                        DateTimeOffset.UtcNow
                    ));
                    return Fold(context);
                });
            }

            // All provisioned
            await Stream.Session(context =>
            {
                context.Append(new TransactionalBatchAllProvisioned(
                    batchId,
                    items.Count,
                    DateTimeOffset.UtcNow
                ));
                return Fold(context);
            });
        }
        catch (Exception ex)
        {
            // Provisioning failed - rollback
            await Stream.Session(context =>
            {
                context.Append(new TransactionalBatchFailed(
                    batchId,
                    _provisionedItems.Count,
                    "",
                    ex.Message,
                    DateTimeOffset.UtcNow
                ));
                return Fold(context);
            });

            // Rollback provisioned items
            await RollbackItems(items, batchId, rollbackItem, ex.Message);

            return new TransactionalBatchResult(
                batchId,
                TransactionalBatchStatus.RolledBack,
                0,
                ex.Message
            );
        }

        // Phase 2: Commit all items
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                await commitItem(items[i], i, batchId);
            }

            await Stream.Session(context =>
            {
                context.Append(new TransactionalBatchCommitted(
                    batchId,
                    items.Count,
                    DateTimeOffset.UtcNow
                ));
                return Fold(context);
            });

            return new TransactionalBatchResult(
                batchId,
                TransactionalBatchStatus.Committed,
                items.Count,
                null
            );
        }
        catch (Exception ex)
        {
            // Commit failed - this is problematic
            // Items are provisioned but not committed
            // Typically, commit should be very lightweight and not fail
            await Stream.Session(context =>
            {
                context.Append(new TransactionalBatchFailed(
                    batchId,
                    items.Count,
                    "",
                    $"Commit phase failed: {ex.Message}",
                    DateTimeOffset.UtcNow
                ));
                return Fold(context);
            });

            // Rollback
            await RollbackItems(items, batchId, rollbackItem, ex.Message);

            return new TransactionalBatchResult(
                batchId,
                TransactionalBatchStatus.RolledBack,
                0,
                $"Commit failed: {ex.Message}"
            );
        }
    }

    private async Task RollbackItems<T>(
        List<T> items,
        string batchId,
        Func<T, int, string, Task> rollbackItem,
        string reason)
    {
        int rolledBack = 0;

        foreach (var index in _provisionedItems.OrderByDescending(x => x))
        {
            try
            {
                await rollbackItem(items[index], index, batchId);
                rolledBack++;
            }
            catch (Exception ex)
            {
                // Log rollback failure but continue
                // This is a serious issue - manual intervention needed
            }
        }

        await Stream.Session(context =>
        {
            context.Append(new TransactionalBatchRolledBack(
                batchId,
                rolledBack,
                reason,
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });
    }

    private string GetItemId<T>(T item) => item!.GetHashCode().ToString();
}

public enum TransactionalBatchStatus
{
    NotStarted,
    Provisioning,
    AllProvisioned,
    Committed,
    Failed,
    RolledBack
}

public record TransactionalBatchResult(
    string BatchId,
    TransactionalBatchStatus Status,
    int CommittedItems,
    string? FailureReason
);
```

### Usage Example

```csharp
var batch = new TransactionalBatch();
await batch.InitializeStream(eventStream);

var customers = await GetCustomersToUpdate(); // 16 customers

var result = await batch.ExecuteTransactional(
    customers,
    provisionItem: async (customer, index, batchId) =>
    {
        // Phase 1: Provision (write pending state)
        var project = await GetProject(customer.ProjectId);
        await project.ApplyAccountUpdateProvisionally(
            batchId,
            customer.NewAccountName,
            customer.NewAccountDetails
        );
    },
    commitItem: async (customer, index, batchId) =>
    {
        // Phase 2: Commit (confirm pending state)
        var project = await GetProject(customer.ProjectId);
        await project.ConfirmAccountUpdate(batchId);
    },
    rollbackItem: async (customer, index, batchId) =>
    {
        // Rollback: Cancel pending state
        var project = await GetProject(customer.ProjectId);
        await project.CancelAccountUpdate(batchId, "Batch failed");
    }
);

if (result.Status == TransactionalBatchStatus.Committed)
{
    Console.WriteLine($"All {result.CommittedItems} items committed successfully");
}
else
{
    Console.WriteLine($"Batch failed and rolled back: {result.FailureReason}");
    // Decision: Retry entire batch or investigate failure
}
```

---

## Strategy 3: Resilient Batch with Skip-on-Error

**Best for**: Large batches where partial success is acceptable, independent items

**Key Features**:
- Continues processing on failure
- Skips failed items
- Records all failures for later review
- No blocking

### Implementation

```csharp
public partial class ResilientBatch : Aggregate
{
    private string? _batchId;
    private int _successCount;
    private int _failureCount;
    private int _skippedCount;
    private Dictionary<int, string> _failures = new();

    public async Task<ResilientBatchResult> ExecuteResilient<T>(
        List<T> items,
        Func<T, int, Task> processItem,
        ResilientBatchOptions options)
    {
        var batchId = Guid.NewGuid().ToString();

        await Stream.Session(context =>
        {
            context.Append(new BatchStarted(
                batchId,
                typeof(T).Name,
                items.Count,
                DateTimeOffset.UtcNow,
                new Dictionary<string, string>()
            ));
            return Fold(context);
        });

        for (int i = 0; i < items.Count; i++)
        {
            try
            {
                await processItem(items[i], i);

                _successCount++;

                await Stream.Session(context =>
                {
                    context.Append(new BatchItemCompleted(
                        batchId,
                        i,
                        GetItemId(items[i]),
                        DateTimeOffset.UtcNow
                    ));
                    return Fold(context);
                });
            }
            catch (Exception ex)
            {
                _failureCount++;
                _failures[i] = ex.Message;

                await Stream.Session(context =>
                {
                    context.Append(new BatchItemFailed(
                        batchId,
                        i,
                        GetItemId(items[i]),
                        ex.Message,
                        "Unknown",
                        ex.GetType().Name,
                        ex.StackTrace ?? "",
                        DateTimeOffset.UtcNow
                    ));
                    return Fold(context);
                });

                // Continue to next item
            }
        }

        await Stream.Session(context =>
        {
            context.Append(new BatchCompleted(
                batchId,
                items.Count,
                _successCount,
                _failureCount,
                _skippedCount,
                DateTimeOffset.UtcNow
            ));
            return Fold(context);
        });

        return new ResilientBatchResult(
            batchId,
            _successCount,
            _failureCount,
            _failures
        );
    }

    private string GetItemId<T>(T item) => item!.GetHashCode().ToString();
}

public class ResilientBatchOptions
{
    public bool ContinueOnError { get; set; } = true;
}

public record ResilientBatchResult(
    string BatchId,
    int SuccessCount,
    int FailureCount,
    Dictionary<int, string> Failures
);
```

---

## Strategy 4: Hybrid Approach with Failure Classification

**Best for**: Most production scenarios - balances all concerns

This combines the best aspects of the previous strategies.

```csharp
public class HybridBatchProcessor
{
    public static async Task<BatchExecutionResult> Execute<T>(
        BatchProcess batch,
        List<T> items,
        Func<T, int, Task> processItem)
    {
        var options = new BatchOptions
        {
            CheckpointInterval = Math.Max(5, items.Count / 10),
            MaxRetries = 3,
            BaseRetryDelaySeconds = 2.0,
            MaxConsecutiveTransientFailures = 3,
            SkipPermanentFailures = false
        };

        var result = await batch.ExecuteBatch(items, processItem, options);

        // Decision logic
        if (result.IsBlocked)
        {
            if (result.PermanentFailures.Any())
            {
                Console.WriteLine("Batch blocked due to permanent failures.");
                Console.WriteLine("Action required: Fix data issues, then restart batch.");
                return result;
            }

            if (result.FailedItems > items.Count * 0.5)
            {
                Console.WriteLine("More than 50% of items failed.");
                Console.WriteLine("Action required: Investigate systemic issue, then restart.");
                return result;
            }

            Console.WriteLine("Transient failures detected. Safe to resume.");
            await batch.Resume("Resuming after transient failure");

            // Retry by calling ExecuteBatch again
            return await batch.ExecuteBatch(items, processItem, options);
        }

        return result;
    }
}
```

---

## Best Practices from Industry

Based on research from Microsoft, AWS, Apache Flink, Spring Batch, and Google Cloud:

### 1. **Design for Idempotency**

**Every operation must be idempotent.**

```csharp
// BAD: Not idempotent
public async Task UpdateCustomer(string customerId, string newValue)
{
    var customer = await GetCustomer(customerId);
    customer.Counter++; // Non-idempotent!
    customer.Value = newValue;
    await SaveCustomer(customer);
}

// GOOD: Idempotent
public async Task UpdateCustomer(string customerId, string newValue, string operationId)
{
    var customer = await GetCustomer(customerId);

    if (customer.ProcessedOperations.Contains(operationId))
        return; // Already processed

    customer.Value = newValue;
    customer.ProcessedOperations.Add(operationId);
    await SaveCustomer(customer);
}
```

### 2. **Checkpoint After Expensive Operations**

Place checkpoints immediately after:
- Compute-intensive operations
- External API calls
- Database writes
- File I/O operations

```csharp
for (int i = 0; i < items.Count; i++)
{
    await ExpensiveOperation(items[i]);

    if ((i + 1) % checkpointInterval == 0)
    {
        await SaveCheckpoint(i);
    }
}
```

### 3. **Use Exponential Backoff for Transient Errors**

```csharp
public async Task<T> ExecuteWithRetry<T>(
    Func<Task<T>> operation,
    int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (TransientFailureDetector.IsTransient(ex))
        {
            if (i == maxRetries - 1)
                throw;

            var delay = TimeSpan.FromSeconds(Math.Pow(2, i));
            await Task.Delay(delay);
        }
    }

    throw new InvalidOperationException("Should not reach here");
}
```

### 4. **Monitor and Alert on Batch Health**

```csharp
public class BatchHealthMonitor
{
    public void RecordBatchMetrics(BatchExecutionResult result)
    {
        var successRate = (double)result.SuccessfulItems /
            (result.SuccessfulItems + result.FailedItems);

        // Alert if success rate drops below threshold
        if (successRate < 0.95)
        {
            AlertOps($"Batch {result.BatchId} has low success rate: {successRate:P}");
        }

        // Alert if batch is blocked
        if (result.IsBlocked)
        {
            AlertOps($"Batch {result.BatchId} is blocked - manual intervention required");
        }

        // Metrics for dashboards
        RecordMetric("batch.success.rate", successRate);
        RecordMetric("batch.failed.items", result.FailedItems);
        RecordMetric("batch.is.blocked", result.IsBlocked ? 1 : 0);
    }

    private void AlertOps(string message)
    {
        // Send to PagerDuty, Slack, etc.
    }

    private void RecordMetric(string name, double value)
    {
        // Send to Prometheus, CloudWatch, etc.
    }
}
```

### 5. **Implement Circuit Breakers for External Dependencies**

```csharp
public class CircuitBreaker
{
    private int _failureCount;
    private DateTime _lastFailureTime;
    private CircuitState _state = CircuitState.Closed;

    public async Task<T> Execute<T>(Func<Task<T>> operation)
    {
        if (_state == CircuitState.Open)
        {
            if (DateTime.UtcNow - _lastFailureTime > TimeSpan.FromMinutes(1))
            {
                _state = CircuitState.HalfOpen;
            }
            else
            {
                throw new CircuitBreakerOpenException();
            }
        }

        try
        {
            var result = await operation();

            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
            }

            return result;
        }
        catch (Exception)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= 5)
            {
                _state = CircuitState.Open;
            }

            throw;
        }
    }
}

public enum CircuitState { Closed, Open, HalfOpen }
public class CircuitBreakerOpenException : Exception { }
```

### 6. **Use Structured Logging**

```csharp
public async Task ProcessItem<T>(T item, int index, string batchId)
{
    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["BatchId"] = batchId,
        ["ItemIndex"] = index,
        ["ItemId"] = GetItemId(item)
    });

    try
    {
        logger.LogInformation("Processing item {ItemIndex}", index);

        await ProcessItemInternal(item);

        logger.LogInformation("Successfully processed item {ItemIndex}", index);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to process item {ItemIndex}: {ErrorMessage}",
            index, ex.Message);
        throw;
    }
}
```

### 7. **Dead Letter Queue for Permanent Failures**

```csharp
public class BatchProcessorWithDLQ
{
    private readonly IDeadLetterQueue _dlq;

    public async Task ProcessItem<T>(T item, int index)
    {
        try
        {
            await ProcessItemInternal(item);
        }
        catch (Exception ex) when (PermanentFailureDetector.IsPermanent(ex))
        {
            // Move to dead letter queue for manual review
            await _dlq.Enqueue(new FailedItem<T>
            {
                Item = item,
                Index = index,
                FailureReason = ex.Message,
                FailedAt = DateTimeOffset.UtcNow,
                StackTrace = ex.StackTrace
            });

            // Don't rethrow - item is handled
        }
    }
}

public interface IDeadLetterQueue
{
    Task Enqueue<T>(FailedItem<T> item);
    Task<List<FailedItem<T>>> GetFailedItems<T>();
}

public class FailedItem<T>
{
    public T Item { get; set; }
    public int Index { get; set; }
    public string FailureReason { get; set; }
    public DateTimeOffset FailedAt { get; set; }
    public string? StackTrace { get; set; }
}
```

---

## Production Considerations

### When to Resume vs Restart

| Scenario | Recommendation | Reasoning |
|----------|----------------|-----------|
| Network timeout during external API call | **Resume** from checkpoint | Transient failure, likely resolved |
| Database deadlock | **Resume** from checkpoint | Transient, retry will likely succeed |
| Invalid data format in item 14 | **Restart** after fixing data | Permanent failure, needs data correction |
| 50%+ items failing | **Restart** after investigation | Systemic issue |
| Transient failures < 10% of items | **Resume** from checkpoint | Normal noise |
| Code bug discovered mid-batch | **Restart** after deploying fix | Permanent issue |
| External service completely down | **Pause** and resume when up | Transient but extended |
| Single item has business rule violation | **Skip** item, continue batch | Isolated permanent failure |

### Operational Runbook

#### Scenario 1: Batch Fails at Item 14 with "Connection Timeout"

**Classification**: Transient failure

**Actions**:
1. Check external service status
2. Verify network connectivity
3. If service is up: Resume from checkpoint
4. If service is down: Wait for recovery, then resume
5. If failures persist: Investigate deeper (could be systemic)

**Commands**:
```csharp
// Resume
await batch.Resume("Connection timeout resolved");

// Or if need to wait
await Task.Delay(TimeSpan.FromMinutes(5));
await batch.Resume("Service recovered");
```

#### Scenario 2: Batch Fails at Item 14 with "Invalid Email Format"

**Classification**: Permanent failure

**Actions**:
1. Review item 14 data
2. Fix data in source system
3. Decide: Skip item or fix and restart
4. If fixing: Restart entire batch
5. If skipping: Configure `SkipPermanentFailures = true` and resume

**Commands**:
```csharp
// After fixing data
await batch.Restart("Data corrected for item 14");

// Or skip
var options = new BatchOptions { SkipPermanentFailures = true };
await batch.ExecuteBatch(items, processItem, options);
```

#### Scenario 3: Unknown if Item 14 Was Partially Processed

**Classification**: Ambiguous

**Actions**:
1. Query destination system to check item 14 state
2. If fully processed: Resume from item 15
3. If not processed: Resume from item 14 (idempotency handles duplicate)
4. If partially processed: Manual intervention to clean up, then resume

**Commands**:
```csharp
// Check state
var item14State = await CheckItemProcessingState(items[14]);

if (item14State == ProcessingState.Complete)
{
    // Manually adjust checkpoint
    await batch.AdvanceCheckpointTo(14);
    await batch.Resume("Item 14 confirmed complete");
}
else if (item14State == ProcessingState.NotStarted)
{
    await batch.Resume("Item 14 not processed, safe to retry");
}
else
{
    // Partial - clean up first
    await CleanupPartialProcessing(items[14]);
    await batch.Resume("Cleaned up partial processing");
}
```

---

## Testing Failure Scenarios

### Unit Tests

```csharp
[Fact]
public async Task BatchProcess_ShouldResumeFromCheckpoint_OnTransientFailure()
{
    // Arrange
    var batch = new BatchProcess();
    var items = Enumerable.Range(1, 16).ToList();
    int callCount = 0;

    async Task ProcessItem(int item, int index)
    {
        callCount++;
        if (index == 13 && callCount == 14) // Fail on first attempt at item 14
        {
            throw new TimeoutException("Simulated timeout");
        }
        await Task.Delay(10);
    }

    // Act
    var result = await batch.ExecuteBatch(items, ProcessItem, new BatchOptions
    {
        CheckpointInterval = 5,
        MaxRetries = 3
    });

    // If blocked, resume
    if (result.IsBlocked)
    {
        await batch.Resume("Retrying after transient failure");
        result = await batch.ExecuteBatch(items, ProcessItem, new BatchOptions());
    }

    // Assert
    Assert.Equal(BatchStatus.Completed, result.Status);
    Assert.Equal(16, result.SuccessfulItems);
}

[Fact]
public async Task BatchProcess_ShouldBlockAndNotResume_OnPermanentFailure()
{
    // Arrange
    var batch = new BatchProcess();
    var items = Enumerable.Range(1, 16).ToList();

    async Task ProcessItem(int item, int index)
    {
        if (index == 13) // Item 14 (0-indexed)
        {
            throw new ValidationException("Invalid data format");
        }
        await Task.Delay(10);
    }

    // Act
    var result = await batch.ExecuteBatch(items, ProcessItem, new BatchOptions
    {
        SkipPermanentFailures = false
    });

    // Assert
    Assert.True(result.IsBlocked);
    Assert.Contains(13, result.PermanentFailures);
    Assert.Equal(13, result.SuccessfulItems); // Only 0-12 succeeded

    // Verify resume throws
    await Assert.ThrowsAsync<InvalidOperationException>(
        async () => await batch.Resume("Should not resume"));
}
```

### Integration Tests

```csharp
[Fact]
public async Task Integration_BatchProcess_WithRealDatabase()
{
    // Arrange
    var batch = new BatchProcess();
    var customers = await TestDatabase.CreateTestCustomers(16);
    var connectionFailureSimulated = false;

    async Task UpdateCustomer(Customer customer, int index)
    {
        if (index == 13 && !connectionFailureSimulated)
        {
            connectionFailureSimulated = true;
            await TestDatabase.SimulateConnectionFailure();
            throw new SqlException("Connection timeout");
        }

        await customerRepository.Update(customer.Id, "NewValue");
    }

    // Act - First attempt (will fail at item 14)
    var result = await batch.ExecuteBatch(customers, UpdateCustomer, new BatchOptions());

    Assert.True(result.IsBlocked);

    // Restore connection
    await TestDatabase.RestoreConnection();

    // Resume
    await batch.Resume("Connection restored");
    result = await batch.ExecuteBatch(customers, UpdateCustomer, new BatchOptions());

    // Assert
    Assert.Equal(BatchStatus.Completed, result.Status);
    Assert.Equal(16, result.SuccessfulItems);

    // Verify all customers updated exactly once (idempotency)
    foreach (var customer in customers)
    {
        var updated = await customerRepository.Get(customer.Id);
        Assert.Equal("NewValue", updated.Value);
        Assert.Equal(1, updated.UpdateCount); // Updated exactly once
    }
}
```

---

## Summary

### Quick Decision Guide

**Your batch fails at item 14 of 16. What should you do?**

1. **Classify the failure** (transient vs permanent)
2. **Check your requirements** (can you skip items?)
3. **Decide**:

```
IF failure is transient AND operations are idempotent
    → RESUME from last checkpoint

IF failure is permanent AND item can be skipped
    → SKIP item, CONTINUE batch

IF failure is permanent AND item cannot be skipped
    → BLOCK batch, FIX data, RESTART from beginning

IF failure type unknown
    → TRY resume once with limited retries
    → IF fails again → Treat as permanent

IF more than 50% items failing
    → STOP, INVESTIGATE systemic issue, RESTART
```

### Key Takeaways

1. ✅ **Always design for idempotency** - enables safe retries
2. ✅ **Checkpoint frequently** - reduces lost work
3. ✅ **Classify failures** - transient vs permanent
4. ✅ **Monitor batch health** - alert on anomalies
5. ✅ **Block on permanent failures** - prevent bad data
6. ✅ **Resume on transient failures** - maximize throughput
7. ✅ **Log everything** - enable forensic analysis
8. ✅ **Test failure scenarios** - don't wait for production

---

**Document Version:** 1.0
**Last Updated:** 2025-11-04
**Author:** ErikLieben.FA.ES Documentation Team
**Based on**: Industry best practices from Microsoft, AWS, Apache Flink, Spring Batch, Google Cloud Run
