# ErikLieben.FA.ES

[![NuGet](https://img.shields.io/nuget/v/ErikLieben.FA.ES?style=flat-square)](https://www.nuget.org/packages/ErikLieben.FA.ES)
[![Changelog](https://img.shields.io/badge/Changelog-docs-informational?style=flat-square)](docs/CHANGELOG.md)
[![.NET 9.0 | 10.0](https://img.shields.io/badge/.NET-9.0%20%7C%2010.0-blue?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/10.0)


[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=sqale_index)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=ncloc)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=eriklieben_ErikLieben.FA.ES&metric=coverage)](https://sonarcloud.io/summary/new_code?id=eriklieben_ErikLieben.FA.ES)
[![Known Vulnerabilities](https://snyk.io/test/github/eriklieben/ErikLieben.FA.ES/badge.svg)](https://snyk.io/test/github/eriklieben/ErikLieben.FA.ES)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/eriklieben/ErikLieben.FA.ES/badge)](https://scorecard.dev/viewer/?uri=github.com/eriklieben/ErikLieben.FA.ES)

> A lightweight, AOT-friendly Event Sourcing toolkit for .NET. Build aggregates, append and read events, create snapshots, upcast historical data, and integrate with Azure storage and Functions.

## A Friendly Note

This is an **opinionated library** built primarily for my own projects and coding style. You're absolutely free to use it (it's MIT licensed!), but please don't expect free support or feature requests. If it works for you, great! If not, there are many other excellent libraries in the .NET ecosystem. For commercially supported event-sourcing platforms, consider [EventStoreDB](https://www.eventstore.com/eventstoredb) or [AxonIQ's Axon Server/Framework](https://www.axoniq.io/).

That said, I do welcome bug reports and thoughtful contributions. If you're thinking about a feature or change, please open an issue first to discuss it.

## Getting Started

The fastest way to explore the library is to run the **TaskFlow** demo application, a full-stack Aspire app showcasing aggregates, projections, Minimal APIs, and Azure Functions.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) or [Podman](https://podman.io/) (for Azurite and CosmosDB emulators)

### Run the Demo

```bash
# Clone the repository
git clone https://github.com/eriklieben/ErikLieben.FA.ES.git
cd ErikLieben.FA.ES

# Run the Aspire demo
dotnet run --project demo/src/TaskFlow.AppHost
```

This starts:
- **API** - ASP.NET Core Minimal API with event-sourced aggregates
- **Functions** - Azure Functions with EventStream and Projection bindings
- **Frontend** - Angular app (optional, at `demo/taskflow-web`)
- **Azurite** - Azure Storage emulator for blobs/tables/queues
- **CosmosDB Emulator** - For CosmosDB-backed event streams (optional)

Open the Aspire dashboard (URL shown in console) to see all services and explore.

### Optional: Persist Storage

To keep data across restarts:
```bash
dotnet run --project demo/src/TaskFlow.AppHost -- --PersistStorage=true
```

## Key Features

| Feature | Description |
|---------|-------------|
| Aggregates | Encapsulate state and behavior; rebuild state by folding events |
| Projections | Read models that materialize streams into queryable shapes |
| CLI Tool | Generates Fold mappings, factories, and JSON serializers |
| AOT-friendly | Source-generated serializers, no reflection in hot paths |
| Storage Providers | Azure Blob, Table, and Cosmos DB support |
| Minimal APIs | `[EventStream]` and `[Projection]` parameter binding |
| Azure Functions | Input bindings for aggregates and projections |
| Testing | In-memory streams with Given-When-Then assertions |

## Packages

```bash
# Core library
dotnet add package ErikLieben.FA.ES

# CLI tool (local)
dotnet new tool-manifest
dotnet tool install ErikLieben.FA.ES.CLI --local

# Storage providers
dotnet add package ErikLieben.FA.ES.AzureStorage
dotnet add package ErikLieben.FA.ES.CosmosDb

# Integrations
dotnet add package ErikLieben.FA.ES.AspNetCore.MinimalApis
dotnet add package ErikLieben.FA.ES.Azure.Functions.Worker.Extensions

# Testing
dotnet add package ErikLieben.FA.ES.Testing
```

Requirements: .NET 9.0 or .NET 10.0

## Documentation

| Topic | Description |
|-------|-------------|
| [Storage Providers](docs/StorageProviders.md) | Azure Blob, Table, and Cosmos DB setup |
| [Concurrency](docs/Concurrency.md) | Optimistic concurrency and constraints |
| [Testing](docs/Testing.md) | AggregateTestBuilder and Given-When-Then patterns |
| [Minimal APIs](docs/MinimalApis.md) | `[EventStream]` and `[Projection]` bindings |
| [Stream Actions](docs/StreamActions.md) | Append, read, and fold operations |
| [Notifications](docs/Notifications.md) | Event notifications and subscriptions |
| [Live Migration](docs/LiveMigration.md) | Migrating event streams without downtime |
| [Event Stream Management](docs/EventStreamManagement.md) | Stream rollover and archiving |
| [Analyzer Rules](docs/AnalyzerRules.md) | Code analyzers and diagnostics |
| [Changelog](docs/CHANGELOG.md) | Version history and release notes |

### Exception Reference

Structured error codes are documented in [docs/exceptions/](docs/exceptions/).

## Quick Example

```csharp
// Define an aggregate
public partial class Customer(IEventStream stream) : Aggregate(stream)
{
    public string? Name { get; private set; }

    public Task Register(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Stream.Session(ctx => Fold(ctx.Append(new CustomerRegistered(name))));
    }

    private void When(CustomerRegistered e) => Name = e.Name;
}

// Define an event
[EventName("Customer.Registered")]
public record CustomerRegistered(string Name);

// Generate supporting code
// dotnet tool run faes
```

See the [demo/src/TaskFlow.Domain](demo/src/TaskFlow.Domain) folder for complete aggregate and projection examples.

## License

MIT License - see the [LICENSE](LICENSE) file for details.
