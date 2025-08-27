# ErikLieben.FA.ES

[![NuGet](https://img.shields.io/nuget/v/ErikLieben.FA.ES?style=flat-square)](https://www.nuget.org/packages/ErikLieben.FA.ES)
[![Changelog](https://img.shields.io/badge/Changelog-docs-informational?style=flat-square)](docs/CHANGELOG.md)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)


[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=sqale_index)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=ncloc)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=coverage)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Known Vulnerabilities](https://snyk.io/test/github/eriklieben/ErikLieben.FA.ES/badge.svg)](https://snyk.io/test/github/eriklieben/ErikLieben.FA.ES)

> A lightweight, AOT-friendly Event Sourcing toolkit for .NET. Build aggregates, append and read events, create snapshots, upcast historical data, and integrate with Azure storage and Functions.

## 👋 A Friendly Note

This is an **opinionated library** built primarily for my own projects and coding style. You're absolutely free to use it (it's MIT licensed!), but please don't expect free support or feature requests. If it works for you, great! If not, there are many other excellent libraries in the .NET ecosystem. For commercially supported event-sourcing platforms, consider EventStoreDB (https://www.eventstore.com/eventstoredb) or AxonIQ's Axon Server/Axon Framework (https://www.axoniq.io/).

That said, I do welcome bug reports and thoughtful contributions. If you're thinking about a feature or change, please open an issue first to discuss it - this helps avoid disappointment if it doesn't align with the library's direction. 😊

> 🚧 Still a bit under construction while moving from in-process Azure Function support to isolated (out-of-process) Azure Function support and full support for AOT. API isn't compatible with older versions, versions before 1.0.0 🚧

## What is ErikLieben.FA.ES?

ErikLieben.FA.ES is an event sourcing toolkit/framework designed to be:
- AOT-friendly and trimming-safe (no reflection-heavy patterns in hot paths)
- Simple to use with clear, testable primitives
- Flexible: plug different storage providers and processing behaviors

## Key Features
- Aggregate-first developer experience with minimal ceremony
- Strong typing and source generation for AOT-friendly serialization
- Upcasters for evolving events over time
- Optional snapshots to accelerate very long streams (snapshot‑free by default)
- Test helpers for fast and deterministic unit tests
- Azure Functions input bindings, works without Azure Functions as well (not depended on)

## Install

Install the CLI tool (locally):
```bash
dotnet new tool-manifest
dotnet tool install ErikLieben.FA.ES.CLI --local
```

Add the NuGet package to your domain class library:
```bash
dotnet add package ErikLieben.FA.ES
```

Decide upon a storage provider and add the corresponding package:
```bash
dotnet add package ErikLieben.FA.ES.AzureStorage
```
*Currently only Azure Storage support is released due to lacking support for AOT in the Azure SDK's for Azure Table Storage & CosmosDB.*

For Azure Functions:
```bash
dotnet add package ErikLieben.FA.ES.Azure.Functions.Worker.Extensions
```

For your unit test projects (inMemory):
```bash
dotnet add package ErikLieben.FA.ES.Testing
```
Requirements: .NET 9.0+

## Quick start

> 🚧 More documentation needs to be added, these are some of the basics. 🚧

Start with creating an aggregate:
```csharp
public partial class Customer(IEventStream stream) : Aggregate(stream)
{
}
```
This class is partial, the CLI will generate the remaining supporting code. 

Because incremental generators can’t be ordered, running our generator alongside `System.Text.Json`’s `JsonSerializable` source generator results in conflicts, because the generators would run at the same time. So this code isn’t generated via a Roslyn incremental generator, but needs to be manually generator through the CLI tool.

What this means:
- Aggregate base class: Aggregate provides common behaviors like folding events and snapshot integration. You only focus on domain logic; infrastructure is handled by the base.
- IEventStream: Injected stream that represents the append-only log for a single aggregate identity (e.g., `Customer/123`). The storage provider implements the stream mechanics.
- Partial type: The CLI generates companion code (factory, snapshot, serializers, bindings) into a `.Generated.cs` file so your hand-written code stays clean and small.
- Testability: Because the stream is an interface, you can use the Testing package to run aggregates purely in-memory.

Next, define an event that represents the business action:

```csharp
[EventName("Customer.Registered.ThroughWebsite")]
public record CustomerRegisteredThroughWebsite(string CustomerName);
```
> ℹ️ Note: This sample uses a minimal event that includes only the customer's name to keep the example focused and easier to understand.

A few event tips:
- **EventName attribute**: This is the serialized/distributed name. It decouples code identifiers from the persisted contract. You can rename the CLR type without breaking the stream.
- **Immutability**: Events represent facts; once appended they aren’t changed. New facts (events) describe subsequent changes.
- **Meaningful names**: Prefer verbs in past tense (`Customer.Registered.ThroughWebsite` not `Register.Customer`) so the stream reads like a history of facts; the dots are optional in the event name.
 
In the Customer aggregate, we're now adding a method to apply the event to the state when it's appended to/ replayed from the eventstream:
```csharp
public partial class Customer(IEventStream stream) : Aggregate(stream)
{
    public string? CustomerName { get; private set; }

    private void When(CustomerRegisteredThroughWebsite @event)
    {
        this.CustomerName = @event.CustomerName;
    }
}
```
We add a `When` method to the aggregate; it's invoked when the stream of events is folded to rebuild the latest state; add one `When` per event type.

More about folding:
- **Deterministic replay**: Given the same sequence of events, the same state should result. Keep When methods side-effect free and don't throw exceptions/ perform validations to them (they are fact's that occured, they occured in this way, even if you don't like that today).
- **Ordering and idempotency**: Events are applied in order. The `When` methods are called in-order.

Next up, we add a method to perform a command/action:

```csharp
public partial class Customer(IEventStream stream) : Aggregate(stream)
{
    public string? CustomerName { get; private set; }

    public Task RegisterCustomerThroughWebsite(string customerName)
    {
        ArgumentNullException.ThrowIfNull(customerName);
        
        return Stream.Session(context =>
            Fold(context.Append(
                new CustomerRegisteredThroughWebsite(customerName)))); 
    }
    
    private void When(CustomerRegisteredThroughWebsite @event)
    {
        this.CustomerName = @event.CustomerName;
    }
}
```
In `RegisterCustomerThroughWebsite`, we validate inputs, append the event, and immediately fold it over the current state.
This simple example omits domain validation; in a real system you could also check whether the current state allows the change (for example, the customer's account isn't blocked).

What the call does:
- `Stream.Session`: Opens a short-lived unit of work with the event stream. Providers can batch writes and handle concurrency.
- `context.Append(...)`: Records the new event as a fact to the stream.
- `Fold(...)`: Applies the newly appended event(s) to your in-memory state so the aggregate is up-to-date after the command.

Next, generate the supporting code with the CLI tool:
```bash
dotnet tool run faes 
```
This generates supporting code for the `Customer` aggregate (in `Customer.Generated.cs`):
- Creates a mapping in the `Fold` method from the event name `Customer.Registered.ThroughWebsite` to the `When` method handling it.
- Generates an `ICustomer` interface containing the public properties of your aggregate.
- Generates a snapshot class used to persist and quickly restore state.
- Generates a `JsonSerializerContext` for the aggregate and all events (this enables AOT-friendly serialization).
- Generates a `CustomerFactory` class to create aggregate instances.
- Generates an `ICustomerFactory` interface.

It also generates a library-wide helper `DemoApp.DomainExtensions.Generated.cs` that contains:
- An extension method for `IServiceCollection` to register the factories and interfaces as singletons to DI.
- A `JsonSerializerContext` for the events (this enables AOT-friendly serialization).

After generation, next steps:
- **Register services**: Call the generated IServiceCollection extension during application startup so factories and serializers are available via DI.
- **Use the factory**: Resolve ICustomerFactory (generated) to create Customer instances for a given identity, backed by the configured stream.
- Run the app or tests: You can now issue commands on your aggregate; events will be appended to the configured provider (e.g., Blob storage) and your state will fold accordingly.

```csharp
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddAzureClients(clientBuilder =>
{
    var store = builder.Configuration.GetConnectionString("Store");
    if (string.IsNullOrEmpty(store))
    {
        throw new InvalidOperationException("AzureWebJobsStorage connection string not found");
    }

    // Create a Azure client with the "Store" name, later used in EventStreamBlobSettings below
    clientBuilder
        .AddBlobServiceClient(store)
        .WithName("Store");

});

// Register the generated factories and code from DemoApp.Domain class library
builder.Services.ConfigureDemoAppDomainFactory();
// Register services of the Azure Blob storage provider and set it up
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store", autoCreateContainer: true));
// Setup the framework to use the blob storage provider by default
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

var app = builder.Build();
await app.RunAsync();
```

### How it works (concepts)
- Event Stream: an append-only log of domain events per aggregate identity.
- Aggregate: encapsulates state and behavior; rebuilds state by folding events.
- Fold: replays events to materialize the latest state; optimized by snapshots.

### Version tokens and object document routing
A version token points to an exact position in an event stream while also anchoring that position to the object and the specific stream instance used at the time.

Key terms:
- **Object**: The aggregate identity (e.g., name + id like Customer/123).
- **Event stream**: The concrete stream instance that holds the events for one object. Over time, you may “roll” to a new stream (e.g., to keep streams small or to refactor schema incompatibilities), but older streams remain available for auditing.
- **Object document**: A small metadata document per object that stores a reference to the active stream for that object. When the active stream changes, the object document is updated to route future reads/writes to the new stream, while old streams remain readable.
- **Version token**: A compact value that captures: object name, object id, the stream identifier, and the exact index/version within that stream where an event was written.

Why it matters:
- Stream rollover/refactor: If you close an overgrown stream or refactor history (remove dated event definitions from the active stream), you keep the old stream intact and create a new stream. The object document switches to the new stream for the object. Existing version tokens continue to reference the original stream and exact position (useful for reproducibility and audits), while new operations route via the object document to the active stream.
- Resume and checkpoints: Projections and readers can store a version token to resume from the precise position they last processed, regardless of later stream routing changes.

Example scenario:
- You have object Customer/123 with stream S1. You decide to “close the books” for S1 and create S2.
- Update the object document for Customer/123 to point to S2. New writes go to S2.
- A previously stored version token that pointed to S1@42 still resolves to the old stream and position for exact replays or audits.
- After reading from S2, you’ll obtain new version tokens like S2@5, which you can persist for concurrency checks or resume points.

In short, the version token unambiguously identifies an event position, and the object document ensures your application always routes to the current active stream for new work—without losing access to historical streams.

### Projections
Projections are read models that materialize one or more streams into a shape that’s fast to query. They’re ideal for list pages, lookups, dashboards, and cross-aggregate views. Instead of replaying every event from every aggregate to render a page (which can be slow), you incrementally fold events into a compact structure.

Key characteristics:
- **Eventually consistent**: projections trail the write-side; they are not a real-time view of the stream.
- **Incremental**: they update from the last processed position (checkpoint) forward.
- **Simple to test**: they’re pure folds over events, like aggregates, but optimized for reads.

Define a projection by declaring a partial class and `When` handlers. The CLI will generate the `Fold` method and JSON serializer context for you.

```csharp
public partial class CustomerListProjection : Projection
{
    public List<string> CustomerNames { get; set; } = new();

    // Called when a new customer is registered
    public void When(CustomerRegisteredThroughWebsite @event)
    {
        if (!CustomerNames.Contains(@event.CustomerName))
        {
            CustomerNames.Add(@event.CustomerName);
        }
    }
}
```

#### Updating a projection
```csharp
// A versionToken can be created manually or captured from your aggregate metadata
var versionToken = new VersionToken("Customer", "12345", "0000000001", 1);

// Resolve dependencies (in production you’d typically get these from DI)
var documentFactory = serviceProvider.GetRequiredService<IObjectDocumentFactory>();
var eventStreamFactory = serviceProvider.GetRequiredService<IEventStreamFactory>();

var projection = new CustomerListProjection(documentFactory, eventStreamFactory);

// Bring the projection up to the specific version; and keep a reference to the object and latest version in the checkpoint
await projection.UpdateToVersion(versionToken);

// Later, you can try to advance it to the latest version (for all tracked streams)
await projection.UpdateToLatestVersion();
```

Tips
- Keep `When` methods idempotent and side-effect free. They should only update in-memory state.
- The base class maintains a `Checkpoint` dictionary and a `CheckpointFingerprint`. You can persist the projection (e.g., as JSON via ToJson()) together with the checkpoint to resume efficiently.
- If you manage checkpoints outside the projection document, annotate your projection with `ProjectionWithExternalCheckpointAttribute`.
- For large numbers of events, prefer updating projections in a background process and serving queries from the materialized state.
- Projections can subscribe to multiple event types—add one `When` method per event type.

### CLI tool: for one-time code generation
The CLI scans your aggregates and events to generate:
- Fold mappings from event names to When methods
- Strongly-typed factory interfaces and registrations
- Source-generated JsonSerializerContext types for AOT-friendly serialization
- Snapshot types and DI extension methods

Run it during development whenever you add or modify aggregates or events.

This is not working with incremental source generators, because there is no way to order incremental source generators and it will run into conflict with the incremental source generators for JSON serialization.

### Testing

Testing an aggregate is straightforward. Use the `TestSetup` class to create an in-memory event stream and assert on the results:
```csharp
[Fact]
public async Task Should_append_event()
{
    // Arrange
    var serviceProvider = Substitute.For<IServiceProvider>();

    // The context can be used for projections, when using multiple seperate streams
    var context = TestSetup.GetContext(serviceProvider, DemoAppDomainFactory.Get);
    
    var eventStream = await context.GetEventStreamFor("Customer", "12345");
    var sut = new Account(eventStream);
    
    // if you want to test based upon previously added events add them here:
    // await eventStream.Session(ctx => ctx.Append(new CustomerRegisteredThroughWebsite(customerName)));
    // sut.Fold();
    
    var customerName = "ABC";
    
    // Act
    await sut.RegisterCustomerThroughWebsite(customerName);
    
    // Assert
    context.Assert
        .ShouldHaveObject("Customer", "12345")
        .WithEventCount(1)
        .WithEventAtLastPosition(new CustomerRegisteredThroughWebsite(customerName));
}
```
Use the Testing package for fast, deterministic unit tests.

## AOT and trimming
- No reflection-heavy code paths in hot areas.
- Source-generated serializers (JsonSerializerContext) are used to stay trimming-safe.
- Works well with Native AOT scenarios.

#### AOT support by storage provider (as of 2025-08-24)
- Azure Blob Storage (ErikLieben.FA.ES.AzureStorage – Blobs): Full Native AOT support.
  - Reason: The blob provider in this library avoids reflection-heavy code paths and uses source-generated serializers; the required Azure Storage Blobs SDK usage is compatible with Native AOT in practice.

Currently only in development branch:
- Azure Table Storage (ErikLieben.FA.ES.AzureStorage – Tables): Not yet Native AOT supported.
  - Reason: Depends on `Azure.Data.Tables`, which does not currently provide full Native AOT/trimming support (e.g., lacks complete trimming annotations and uses reflection-based patterns in key areas).
- Azure Cosmos DB (ErikLieben.FA.ES.AzureCosmosDB): Not yet Native AOT supported.
  - Reason: Depends on `Azure.Cosmos`, which does not currently offer full Native AOT support; relevant work is pending in the Azure SDK ecosystem.

### Observability
- ActivitySource integration points allow you to trace reads, writes, folds, and snapshots.

## 📄 License

MIT License - see the [LICENSE](LICENSE) file for details.
