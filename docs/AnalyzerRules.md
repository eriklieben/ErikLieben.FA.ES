# Analyzer Rules Reference

The `ErikLieben.FA.ES.Analyzers` package provides Roslyn analyzers that detect common issues and enforce best practices in your event sourcing code. These analyzers run during compilation and in your IDE.

## Overview

Analyzers are automatically included when you reference the main `ErikLieben.FA.ES` package. No additional setup required.

### Rule Categories

| Category | Rules | Description |
|----------|-------|-------------|
| Usage | FAES0002, FAES0003 | Detect incorrect API usage patterns |
| Code Generation | FAES0005, FAES0006, FAES0007 | Ensure generated code is up to date |

## Usage Rules

### FAES0002: Appended event is not applied to active state

**Severity:** Warning
**Category:** Usage

This rule detects when an event is appended to a stream within an Aggregate's session but not applied to the aggregate's active state. This is almost always a bug.

#### Incorrect

```csharp
public async Task Ship(string trackingNumber)
{
    // Event is appended but state isn't updated
    context.Append(new OrderShipped(trackingNumber));
}
```

#### Correct

```csharp
public async Task Ship(string trackingNumber)
{
    // Event is appended AND applied to state
    Fold(context.Append(new OrderShipped(trackingNumber)));
}
```

#### How to Fix

Wrap the `context.Append(...)` call with `Fold(...)` or `When(...)` to apply the event to the aggregate's state.

---

### FAES0003: Aggregate-derived class should be partial

**Severity:** Warning
**Category:** Usage

Classes inheriting from `Aggregate` or `Projection` must be declared as `partial` so the CLI tool can generate supporting code.

#### How to Fix

1. Add the `partial` keyword to your class declaration:
   ```csharp
   public partial class OrderAggregate : Aggregate<OrderState>
   ```

2. Run the CLI tool to generate supporting code:
   ```bash
   dotnet faes
   ```

## Code Generation Rules

### FAES0005: Generated file missing

**Severity:** Warning
**Category:** CodeGeneration

The analyzer detected an Aggregate or Projection class but couldn't find the corresponding generated file (`{ClassName}.Generated.cs`).

#### How to Fix

Run `dotnet faes` in your project directory to generate the supporting code. For continuous development, use `dotnet faes watch`.

---

### FAES0006: Generated code is out of date

**Severity:** Warning
**Category:** CodeGeneration

A `When` method or `[When<T>]` attribute was added to an Aggregate or Projection, but the generated `Fold` method doesn't include a case for it.

#### This warning appears when:

- You add a new `When(EventType evt)` method
- You add a new `[When<EventType>]` attribute
- You rename an event type
- The generated file was manually edited or deleted

#### How to Fix

Run `dotnet faes` to regenerate the `Fold` method with the new event handler.

---

### FAES0007: Property not in generated interface

**Severity:** Warning
**Category:** CodeGeneration

A public property was added to an Aggregate or Projection class, but the generated interface (`I{ClassName}`) doesn't include it.

#### This warning appears when:

- You add a new public property to an Aggregate
- You change a property from private to public
- You add an expression-bodied property

#### How to Fix

Run `dotnet faes` to regenerate the interface with the new property.

## Configuration

You can configure analyzer severity or suppress specific rules using standard .NET mechanisms.

### EditorConfig

```ini
# .editorconfig

# Treat FAES0002 as an error
dotnet_diagnostic.FAES0002.severity = error

# Suppress FAES0005 warnings
dotnet_diagnostic.FAES0005.severity = none

# Set all FAES rules to warning
dotnet_diagnostic.FAES.severity = warning
```

### Project File

```xml
<!-- .csproj -->
<PropertyGroup>
  <NoWarn>$(NoWarn);FAES0005</NoWarn>
</PropertyGroup>
```

### Pragma Directives

```csharp
#pragma warning disable FAES0005
public partial class MyAggregate : Aggregate
{
    // Generated file intentionally missing
}
#pragma warning restore FAES0005
```

## Severity Levels

| Severity | Description |
|----------|-------------|
| **error** | Fails the build |
| **warning** | Shows in build output (default) |
| **suggestion** | IDE-only, not in build |
| **silent / none** | Disabled |

## Best Practices

1. **Keep analyzers enabled** - They catch common mistakes early
2. **Run `dotnet faes` regularly** - Keeps generated code in sync
3. **Use `dotnet faes watch` during development** - Auto-regenerates on changes
4. **Treat FAES0002 as error** - Unapplied events are almost always bugs
5. **Review suppressions** - Ensure suppressions are intentional
