# CLI Tool Reference

The `dotnet faes` CLI tool generates boilerplate code for aggregates, projections, and their supporting infrastructure.

## Installation

### Global Installation (Recommended)

```bash
dotnet tool install --global ErikLieben.FA.ES.CLI
```

### Local Installation (Per-Project)

```bash
dotnet new tool-manifest  # If not already present
dotnet tool install ErikLieben.FA.ES.CLI
```

### Update to Latest Version

```bash
dotnet tool update --global ErikLieben.FA.ES.CLI
```

## Commands

### Generate Code

```bash
dotnet faes [path] [options]
```

Analyzes your solution and generates supporting code for all aggregates and projections.

#### Arguments

| Argument | Description | Default |
|----------|-------------|---------|
| `path` | Path to solution (.sln) or project (.csproj) | Current directory |

#### Options

| Option | Description |
|--------|-------------|
| `--with-diff` | Show what will be generated without writing files |
| `--verbose` | Show detailed output during generation |
| `--help` | Display help information |

#### Examples

```bash
# Generate code for current directory
dotnet faes

# Generate code for specific solution
dotnet faes ./MyApp.sln

# Preview changes without writing
dotnet faes --with-diff

# Generate with verbose output
dotnet faes --verbose
```

### Watch Mode

```bash
dotnet faes watch [path] [options]
```

Continuously monitors for file changes and regenerates code automatically.

#### Examples

```bash
# Watch current directory
dotnet faes watch

# Watch specific solution
dotnet faes watch ./MyApp.sln
```

Press `Ctrl+C` to stop watching.

## What Gets Generated

### For Each Aggregate

Given an aggregate like:

```csharp
[Aggregate]
public partial class Order : Aggregate
{
    private void When(OrderCreated @event) { }
    private void When(OrderShipped @event) { }
}
```

The CLI generates `Order.Generated.cs`:

```csharp
public partial class Order : Aggregate, IBase, IOrder
{
    // Static factory for AOT compatibility
    public static string ObjectName => "order";
    public static Order Create(IEventStream stream) => new Order(stream);

    // Generated Fold method dispatches events to When handlers
    public override void Fold(IEvent @event)
    {
        switch (@event.EventType)
        {
            case "Order.Created":
                When(JsonEvent.To(@event, OrderCreatedJsonSerializerContext.Default.OrderCreated));
                break;
            case "Order.Shipped":
                When(JsonEvent.To(@event, OrderShippedJsonSerializerContext.Default.OrderShipped));
                break;
        }
    }
}

// JSON serialization context for AOT
[JsonSerializable(typeof(OrderCreated))]
internal partial class OrderCreatedJsonSerializerContext : JsonSerializerContext { }
```

### For Each Projection

Similar to aggregates, projections get generated Fold methods and JSON contexts.

### Domain Extensions

For each project containing aggregates, a `{ProjectName}Extensions.Generated.cs` file is created:

```csharp
public partial class MyAppFactory : AggregateFactory, IAggregateFactory
{
    public static void Register(IServiceCollection serviceCollection)
    {
        // Registers all aggregate factories
        serviceCollection.AddSingleton<IAggregateFactory<Order>, OrderFactory>();
        serviceCollection.AddSingleton<IOrderFactory, OrderFactory>();
        serviceCollection.AddScoped<IOrderRepository, OrderRepository>();
        // ... more registrations
    }
}

public static class MyAppExtensions
{
    public static IServiceCollection ConfigureMyAppFactory(this IServiceCollection services)
    {
        MyAppFactory.Register(services);
        return services;
    }
}
```

## File Naming Conventions

| Source File | Generated File |
|-------------|----------------|
| `Order.cs` | `Order.Generated.cs` |
| `OrderDashboard.cs` | `OrderDashboard.Generated.cs` |
| (project root) | `{ProjectName}Extensions.Generated.cs` |

## When to Run

Run `dotnet faes` after:

- Adding a new aggregate or projection
- Adding a new event type
- Adding or modifying a `When` method
- Adding or modifying public properties on an aggregate
- Changing `[EventName]` attributes
- Adding `[UseUpcaster]` attributes

## Analyzer Integration

The library includes Roslyn analyzers that detect when regeneration is needed:

| Rule | Description |
|------|-------------|
| FAES0002 | Appended event is not applied to state (missing `Fold`) |
| FAES0003 | Aggregate class should be partial |
| FAES0005 | Generated file missing |
| FAES0006 | Generated code is out of date |
| FAES0007 | Property not in generated interface |

These warnings appear in your IDE and build output, reminding you to run `dotnet faes`.

## Troubleshooting

### "No aggregates or projections found"

Ensure your classes:
1. Have the `[Aggregate]` or `[BlobJsonProjection]` attribute
2. Inherit from `Aggregate` or `Projection`
3. Are marked as `partial`

### "Failed to load solution"

The CLI uses MSBuild to analyze your code. Ensure:
1. The solution builds successfully (`dotnet build`)
2. All NuGet packages are restored (`dotnet restore`)
3. You have the .NET SDK installed

### "Generated code doesn't compile"

This can happen if:
1. Event types are missing `[EventName]` attributes
2. When methods have unsupported signatures
3. There are circular dependencies

Run `dotnet faes --verbose` for detailed error information.

### Watch mode not detecting changes

Ensure you're saving files (not just editing). The watcher monitors file system events.

## Performance Tips

### Large Solutions

For solutions with many projects, specify the project path directly:

```bash
dotnet faes ./src/MyDomain/MyDomain.csproj
```

### CI/CD Integration

In CI pipelines, run generation as part of the build:

```yaml
- name: Generate Event Sourcing Code
  run: dotnet faes

- name: Build
  run: dotnet build
```

### Pre-commit Hook

Add a pre-commit hook to ensure generated code is up to date:

```bash
#!/bin/sh
dotnet faes --with-diff
if [ $? -ne 0 ]; then
    echo "Generated code is out of date. Run 'dotnet faes' and commit the changes."
    exit 1
fi
```

## Configuration

The CLI currently uses conventions and doesn't require configuration files. Future versions may support `.faesrc` or similar configuration.

## See Also

- [Getting Started](GettingStarted.md) - Initial setup guide
- [Aggregates](Aggregates.md) - Aggregate patterns and best practices
- [Projections](Projections.md) - Projection types and configuration
- [Analyzer Rules](AnalyzerRules.md) - Roslyn analyzer reference
