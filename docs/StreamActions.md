# Stream Actions

Stream actions provide hooks into the event stream lifecycle, allowing you to execute custom logic before or after events are appended to the stream.

## Overview

The event sourcing framework provides three types of stream actions:

| Action Type | Interface | When Executed |
|-------------|-----------|---------------|
| Pre-Append | `IPreAppendAction` | Before an event is appended to the stream |
| Post-Append | `IPostAppendAction` | After an event is appended (before commit) |
| Post-Commit | `IAsyncPostCommitAction` | After events are committed to storage |

## Action Types

### Pre-Append Actions

Pre-append actions execute before an event is added to the stream. Use them for:
- Validation
- Enrichment
- Authorization checks
- Metrics collection

```csharp
public class ValidationAction : IPreAppendAction
{
    public Func<T> PreAppend<T>(T data, JsonEvent @event, IObjectDocument objectDocument)
        where T : class
    {
        // Validate the event before it's appended
        if (@event.EventType == "OrderCreated")
        {
            var payload = JsonSerializer.Deserialize<OrderCreatedPayload>(@event.Payload);
            if (payload?.Amount <= 0)
            {
                throw new ValidationException("Order amount must be positive");
            }
        }

        // Return the data unchanged (or modify if needed)
        return () => data;
    }
}
```

### Post-Append Actions

Post-append actions execute after an event is appended but before the session is committed. Use them for:
- Metrics tracking
- Logging
- Secondary writes (within the same transaction boundary)

```csharp
public class MetricsAction : IPostAppendAction
{
    private readonly IMetricsService _metrics;

    public MetricsAction(IMetricsService metrics)
    {
        _metrics = metrics;
    }

    public Func<T> PostAppend<T>(T data, JsonEvent @event, IObjectDocument document)
        where T : class
    {
        // Track metrics after event is appended
        _metrics.IncrementCounter("events_appended", new Dictionary<string, string>
        {
            ["event_type"] = @event.EventType,
            ["object_name"] = document.ObjectName
        });

        return () => data;
    }
}
```

### Post-Commit Actions

Post-commit actions execute asynchronously after events are committed to storage. Use them for:
- Sending notifications
- Triggering external integrations
- Updating read models
- Audit logging

```csharp
public class NotificationAction : IAsyncPostCommitAction
{
    private readonly INotificationService _notifications;

    public NotificationAction(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task PostCommitAsync(IEnumerable<JsonEvent> events, IObjectDocument document)
    {
        // Send notifications after events are committed
        foreach (var evt in events)
        {
            if (evt.EventType == "OrderShipped")
            {
                await _notifications.SendAsync(new OrderShippedNotification
                {
                    OrderId = document.ObjectId,
                    EventVersion = evt.EventVersion
                });
            }
        }
    }
}
```

## Registration

### Direct Registration on Event Stream

```csharp
// Register actions on the event stream
eventStream.RegisterAction(new ValidationAction());
eventStream.RegisterAction(new MetricsAction(metricsService));
eventStream.RegisterAction(new NotificationAction(notificationService));
```

### Dependency Injection Registration

```csharp
// Or register via dependency injection
services.AddSingleton<IPreAppendAction, ValidationAction>();
services.AddSingleton<IPostAppendAction, MetricsAction>();
services.AddSingleton<IAsyncPostCommitAction, NotificationAction>();
```

## Use Cases

### Business Rule Validation

```csharp
public class BusinessRuleValidationAction : IPreAppendAction
{
    private readonly IBusinessRuleEngine _rules;

    public BusinessRuleValidationAction(IBusinessRuleEngine rules)
    {
        _rules = rules;
    }

    public Func<T> PreAppend<T>(T data, JsonEvent @event, IObjectDocument objectDocument)
        where T : class
    {
        // Run business rules before appending
        var context = new ValidationContext
        {
            EventType = @event.EventType,
            Payload = @event.Payload,
            CurrentState = data,
            ObjectId = objectDocument.ObjectId
        };

        var result = _rules.Validate(context);
        if (!result.IsValid)
        {
            throw new BusinessRuleViolationException(result.Errors);
        }

        return () => data;
    }
}
```

### Audit Logging

```csharp
public class AuditLogAction : IAsyncPostCommitAction
{
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentUserService _currentUser;

    public AuditLogAction(IAuditLogService auditLog, ICurrentUserService currentUser)
    {
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public async Task PostCommitAsync(IEnumerable<JsonEvent> events, IObjectDocument document)
    {
        var auditEntries = events.Select(evt => new AuditEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = _currentUser.UserId,
            Action = evt.EventType,
            ObjectType = document.ObjectName,
            ObjectId = document.ObjectId,
            EventVersion = evt.EventVersion,
            Payload = evt.Payload
        });

        await _auditLog.WriteEntriesAsync(auditEntries);
    }
}
```

## Best Practices

1. **Keep pre-append actions fast** - They execute synchronously and block event appending
2. **Use post-commit for external integrations** - Ensures events are durably stored first
3. **Handle failures gracefully** - Post-commit actions shouldn't fail silently
4. **Consider idempotency** - Post-commit actions may be retried in failure scenarios
5. **Log action execution** - Helps with debugging and auditing
