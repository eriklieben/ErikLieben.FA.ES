# ErikLieben.FA.ES v2 Completeness Review

**Date:** 2026-02-16
**Branch:** vnext
**Reviewer:** Automated (Claude Opus 4.6)

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Project Structure Overview](#project-structure-overview)
3. [Target Framework Alignment](#target-framework-alignment)
4. [NuGet Packaging & Version Consistency](#nuget-packaging--version-consistency)
5. [API Surface Completeness](#api-surface-completeness)
6. [Storage Provider Feature Parity Matrix](#storage-provider-feature-parity-matrix)
7. [Test Coverage Analysis](#test-coverage-analysis)
8. [Roslyn Analyzer Coverage](#roslyn-analyzer-coverage)
9. [Result Type Consistency](#result-type-consistency)
10. [Missing Features & Gaps](#missing-features--gaps)
11. [Documentation Accuracy vs Implementation](#documentation-accuracy-vs-implementation)
12. [Breaking Changes from Previous Previews](#breaking-changes-from-previous-previews)
13. [Recommendations](#recommendations)

---

## Executive Summary

The ErikLieben.FA.ES library is a comprehensive event sourcing framework with strong foundations. The library provides four storage providers (Azure Blob, Azure Table, CosmosDB, S3), a code generation CLI, Roslyn analyzers, testing utilities, ASP.NET Core and Azure Functions integration, event stream management/migration, and observability via OpenTelemetry.

**Overall assessment: Near v2-ready with a few notable gaps.**

Key strengths:
- AOT-compatible design (no reflection)
- Multi-provider support with fluent builder API
- Comprehensive Roslyn analyzer suite (10 rules + 3 refactoring providers)
- Rich event stream management (migration, backup, transformation, verification)
- OpenTelemetry metrics and distributed tracing
- Snapshot lifecycle management and retention policies

Key concerns:
- CosmosDB provider targets only `net9.0` (not `net10.0`)
- WebJobs.Isolated.Extensions targets only `net8.0`
- No `IStreamMetadataProvider` or `IProjectionStatusCoordinator` for S3
- No tiering service outside Azure Blob
- CosmosDB hardcoded `Version` of `1.0.0`
- PublicAPI tracking is currently disabled (RS0016 suppressed)

---

## Project Structure Overview

### Source Projects (12)

| Project | Role |
|---------|------|
| `ErikLieben.FA.ES` | Core library: aggregates, projections, events, interfaces |
| `ErikLieben.FA.ES.AzureStorage` | Azure Blob + Table storage providers |
| `ErikLieben.FA.ES.CosmosDb` | Azure CosmosDB storage provider |
| `ErikLieben.FA.ES.S3` | S3-compatible storage provider (AWS, MinIO, etc.) |
| `ErikLieben.FA.ES.EventStreamManagement` | Migration, transformation, backup, repair |
| `ErikLieben.FA.ES.AspNetCore.MinimalApis` | ASP.NET Core Minimal API parameter binding |
| `ErikLieben.FA.ES.Azure.Functions.Worker.Extensions` | Azure Functions isolated worker binding |
| `ErikLieben.FA.ES.WebJobs.Isolated.Extensions` | Azure WebJobs binding (legacy) |
| `ErikLieben.FA.ES.CLI` | `dotnet faes` code generation tool |
| `ErikLieben.FA.ES.CodeAnalysis` | Shared Roslyn analysis utilities |
| `ErikLieben.FA.ES.Analyzers` | Roslyn analyzers and code fixes |
| `ErikLieben.FA.ES.Testing` | In-memory test doubles and test builders |

### Test Projects (13)

Every source project has a corresponding test project, plus `ErikLieben.FA.ES.S3.Benchmarks`.

---

## Target Framework Alignment

### Source Projects

| Project | Target Frameworks | AOT Compatible | Issue |
|---------|------------------|----------------|-------|
| `ErikLieben.FA.ES` | `net9.0;net10.0` | Yes | -- |
| `ErikLieben.FA.ES.AzureStorage` | `net9.0;net10.0` | Yes | -- |
| **`ErikLieben.FA.ES.CosmosDb`** | **`net9.0`** | Yes | **Missing `net10.0`** |
| `ErikLieben.FA.ES.S3` | `net9.0;net10.0` | Yes | -- |
| `ErikLieben.FA.ES.EventStreamManagement` | `net9.0;net10.0` | Yes | -- |
| `ErikLieben.FA.ES.AspNetCore.MinimalApis` | `net9.0;net10.0` | Yes | -- |
| `ErikLieben.FA.ES.Azure.Functions.Worker.Extensions` | `net9.0;net10.0` | -- | -- |
| **`ErikLieben.FA.ES.WebJobs.Isolated.Extensions`** | **`net8.0`** | -- | **net8.0 only; no net9/10** |
| `ErikLieben.FA.ES.CLI` | `net9.0;net10.0` | -- | -- |
| `ErikLieben.FA.ES.CodeAnalysis` | `netstandard2.0` | -- | Correct for analyzers |
| `ErikLieben.FA.ES.Analyzers` | `netstandard2.0` | -- | Correct for analyzers |
| `ErikLieben.FA.ES.Testing` | `net9.0;net10.0` | -- | -- |

### Test Projects

| Project | Target Frameworks | Issue |
|---------|------------------|-------|
| `ErikLieben.FA.ES.Tests` | `net9.0;net10.0` | -- |
| `ErikLieben.FA.ES.AzureStorage.Tests` | `net9.0;net10.0` | -- |
| **`ErikLieben.FA.ES.CosmosDb.Tests`** | **`net9.0`** | **Missing `net10.0`** |
| `ErikLieben.FA.ES.S3.Tests` | `net9.0;net10.0` | -- |
| `ErikLieben.FA.ES.EventStreamManagement.Tests` | `net9.0;net10.0` | -- |
| `ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests` | `net9.0;net10.0` | -- |
| `ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests` | `net9.0;net10.0` | -- |
| **`ErikLieben.FA.ES.WebJobs.Isolated.Extensions.Tests`** | **`net9.0`** | Mismatch with src (net8.0) |
| `ErikLieben.FA.ES.CLI.Tests` | `net9.0;net10.0` | -- |
| `ErikLieben.FA.ES.CodeAnalysis.Tests` | `net9.0` | OK (analyzer tests) |
| `ErikLieben.FA.ES.Analyzers.Tests` | `net9.0` | OK (analyzer tests) |
| `ErikLieben.FA.ES.Testing.Tests` | `net9.0;net10.0` | -- |
| `ErikLieben.FA.ES.S3.Benchmarks` | `net9.0` | OK (benchmark only) |

**Findings:**
1. **CosmosDB** targets only `net9.0` while all other providers target `net9.0;net10.0`. This is a v2 blocker for users targeting .NET 10.
2. **WebJobs.Isolated.Extensions** targets `net8.0`, which is legacy. Consider whether this package should be deprecated for v2 or upgraded.
3. WebJobs test project targets `net9.0` but the source targets `net8.0` -- a framework mismatch.

---

## NuGet Packaging & Version Consistency

### Version Management

| Aspect | Status | Details |
|--------|--------|---------|
| Central version props | Partial | `Directory.Build.props` manages some versions centrally but not all |
| PackageVersion | Conditional | Most use `$(PackageVersion)` conditional on `$(Version)` |
| **CosmosDB hardcoded version** | **Issue** | Hardcoded `<Version>1.0.0</Version>` instead of using `$(PackageVersion)` pattern |
| Lock files | Enabled | `RestorePackagesWithLockFile` is `true` via `Directory.Build.props` |
| Symbol packages | Enabled | All projects generate `.snupkg` files |
| Source link | Enabled | `PublishRepositoryUrl` and `EmbedUntrackedSources` set |
| Documentation | Partial | `GenerateDocumentationFile` not set on all projects |
| README/LICENSE in packages | Yes | All packable projects include README.md and LICENSE |

### Package Metadata Consistency

| Aspect | Status |
|--------|--------|
| Authors | Consistent: "Erik Lieben" |
| Company | Consistent: "Erik Lieben" |
| RepositoryUrl | Consistent (most projects) |
| **Description** | **Inconsistent** -- Core/AzureStorage/Testing/CLI/Functions/WebJobs/Analyzers use generic "AOT-friendly Event Sourcing toolkit" while CosmosDB, S3, EventStreamManagement, MinimalApis have specific descriptions |
| PackageTags | Inconsistent across projects |

### PublicAPI Tracking

- `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` exist in the core project
- Both files contain only `#nullable enable` -- no API surface tracked
- `RS0016` (Symbol is not part of declared public API) is suppressed via `NoWarn`
- **Finding:** Public API tracking is effectively disabled. For v2, this should be populated to prevent accidental breaking changes.
- Only the core project references `Microsoft.CodeAnalysis.PublicApiAnalyzers`; storage providers do not have PublicAPI files.

---

## API Surface Completeness

### Core Interfaces

| Interface | Purpose | Status |
|-----------|---------|--------|
| `IEventStream` | Read, append, session, snapshot, register operations | Complete |
| `IEventStreamFactory` / `IEventStreamFactory<T>` | Create event streams | Complete |
| `IObjectDocumentFactory` | Create/get object documents | Complete |
| `IDocumentStore` | CRUD for object documents | Complete |
| `ISnapShotStore` | Get, Set, List, Delete, DeleteMany snapshots | Complete |
| `IObjectIdProvider` | Enumerate object IDs, check existence, count | Complete |
| `IDocumentTagStore` | Tag-based document management | Complete |
| `IDocumentTagDocumentFactory` | Create tag documents | Complete |
| `IStreamMetadataProvider` | Stream metadata for retention | Complete |
| `IProjectionStatusCoordinator` | Projection lifecycle management | Complete |
| `ICatchUpDiscoveryService` | Projection catch-up orchestration | Complete |
| `IProjectionLoader` | Versioned projection loading | Complete |
| `ICheckpointDiffService` | Checkpoint diffing | Complete |

### Aggregate Operations

| Operation | Supported |
|-----------|-----------|
| Create aggregate | Yes |
| Load (hydrate from events) | Yes |
| Append events (Session + Fold) | Yes |
| Optimistic concurrency | Yes (via `Constraint.Strict`) |
| Snapshots (create/restore) | Yes |
| Event upcasting | Yes (AOT-compatible, versioned) |
| Pre/post append actions | Yes |
| Post-read actions | Yes |
| Async post-commit actions | Yes |
| Stream closing | Yes (via `EventStreamClosedException`) |
| Validation / Decision context | Yes |
| Inline snapshot management | Yes |

### Projection Operations

| Operation | Supported |
|-----------|-----------|
| Global projections | Yes |
| Per-entity (routed) projections | Yes |
| Multi-document projections | Yes (CosmosDB) |
| Checkpoint tracking (per-stream) | Yes |
| Checkpoint fingerprinting (SHA-256) | Yes |
| Schema versioning | Yes (`[ProjectionVersion]`) |
| Schema mismatch detection | Yes |
| Status lifecycle (Active/Rebuilding/CatchingUp/Ready/Failed/Disabled/Archived) | Yes |
| Rebuild coordination (blocking/blue-green) | Yes |
| Version routing | Yes |
| When parameter factories | Yes (version token aware) |

### Event Stream Management (Migration)

| Feature | Status |
|---------|--------|
| Migration builder (fluent API) | Yes |
| Bulk migration builder | Yes |
| Migration executor | Yes |
| Live migration (zero-downtime) | Yes |
| Transformation pipelines | Yes (composite, function) |
| Stream routing (cutover phases) | Yes |
| Backup/restore | Yes |
| Stream repair | Yes |
| Progress tracking | Yes |
| Verification/planning | Yes |
| Distributed locking | Yes (interface + blob lease impl) |
| Book closing | Yes |

---

## Storage Provider Feature Parity Matrix

### Core Event Stream Operations

| Feature | Blob | Table | CosmosDB | S3 |
|---------|:----:|:-----:|:--------:|:--:|
| EventStream | Yes | Yes | Yes | Yes |
| EventStreamFactory | Yes | Yes | Yes | Yes |
| ObjectDocumentFactory | Yes | Yes | Yes | Yes |
| DocumentStore | Yes | Yes | Yes | Yes |
| ObjectIdProvider | Yes | Yes | Yes | Yes |
| DocumentTagStore | Yes | Yes | Yes | Yes |
| TagFactory | Yes | Yes | Yes | Yes |
| DataStore | Yes | Yes | Yes | Yes |
| SnapShotStore | Yes | Yes | Yes | Yes |
| ProjectionFactory | Yes | Yes | Yes | Yes |
| Fluent builder (`IFaesBuilder`) | Yes | Yes | Yes | Yes |
| Classic `IServiceCollection` config | Yes | Yes | Yes | Yes |
| Health checks | Yes | Yes | Yes | Yes |

### Advanced Features

| Feature | Blob | Table | CosmosDB | S3 |
|---------|:----:|:-----:|:--------:|:--:|
| IStreamMetadataProvider (retention) | Yes | **No** | **No** | Yes |
| IProjectionStatusCoordinator | Yes | Yes | Yes | **No** |
| Stream tiering (access tier control) | Yes | N/A | N/A | **No** |
| Rehydration from archive | Yes | N/A | N/A | **No** |
| Multi-document projections | No | No | Yes | No |
| Custom projection attributes | `[BlobJsonProjection]` | `[TableProjection]` | `[CosmosDbJsonProjection]`, `[CosmosDbMultiDocumentProjection]` | No (uses Blob?) |
| Routed projection factory | Yes | No | No | No |
| Migration routing table | Yes | **No** | **No** | **No** |
| Backup provider | Yes | **No** | **No** | **No** |
| Distributed lock provider | Yes (blob lease) | **No** | **No** | **No** |
| Integration tests | Yes | Yes | Yes (CosmosDB emulator) | No |
| net10.0 support | Yes | Yes | **No** | Yes |
| EventStreamManagement dependency | Yes | Yes | No | Yes |

### Detailed Provider Gaps

#### CosmosDB Missing
- `net10.0` target framework
- `IStreamMetadataProvider` -- cannot evaluate retention policies
- `IMigrationRoutingTable` -- cannot do cutover migrations
- `IBackupProvider` -- no native backup support
- `IDistributedLockProvider` -- no lock implementation (could use lease on CosmosDB)
- Dependency on `Newtonsoft.Json` (alongside System.Text.Json) -- AOT concern
- Hardcoded `<Version>1.0.0</Version>`

#### S3 Missing
- `IProjectionStatusCoordinator` -- cannot manage projection rebuild lifecycle
- Custom projection attribute (relies on blob-style projections?)
- `IMigrationRoutingTable` -- cannot do cutover migrations
- `IBackupProvider` -- no native backup support
- `IDistributedLockProvider` -- no lock implementation
- Stream tiering (S3 has storage classes but no implementation)
- Integration tests (no container fixture for MinIO)

#### Azure Table Missing
- `IStreamMetadataProvider` -- cannot evaluate retention policies
- `IMigrationRoutingTable` -- cannot do cutover migrations
- `IBackupProvider` -- no native backup support
- `IDistributedLockProvider` -- no lock implementation (could use table leases)
- Routed projection factory

---

## Test Coverage Analysis

### Test File Counts by Project

| Test Project | Test Files | Integration Tests |
|-------------|-----------|-------------------|
| ErikLieben.FA.ES.Tests | ~120 files | No |
| ErikLieben.FA.ES.AzureStorage.Tests | ~48 files | Yes (Blob + Table via Azurite) |
| ErikLieben.FA.ES.CosmosDb.Tests | ~25 files | Yes (CosmosDB emulator) |
| ErikLieben.FA.ES.S3.Tests | ~20 files | **No** |
| ErikLieben.FA.ES.EventStreamManagement.Tests | ~35 files | Yes (Blob-to-CosmosDB migration) |
| ErikLieben.FA.ES.CLI.Tests | ~50+ files | No |
| ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests | ~12 files | No |
| ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests | ~12 files | No |
| ErikLieben.FA.ES.WebJobs.Isolated.Extensions.Tests | ~6 files | No |
| ErikLieben.FA.ES.Testing.Tests | ~28 files | No |
| ErikLieben.FA.ES.Analyzers.Tests | 11 files | No |
| ErikLieben.FA.ES.CodeAnalysis.Tests | 2 files | No |

### Test Coverage Observations

**Well-Covered Areas:**
- Core library (aggregates, projections, events, version tokens, checkpoints)
- All exception types (including serialization tests)
- All attributes
- Builder/configuration
- Results/Error types
- Retention policies
- Snapshot policies
- Observability metrics
- Event stream management (migration, transformation, backup, verification)
- CLI code generation and analysis
- Analyzer rules (all 10 rules + code fix + refactoring providers)
- In-memory testing utilities

**Coverage Gaps:**
- **S3 provider has no integration tests** -- unit tests mock `IAmazonS3` but no MinIO container fixture
- CosmosDB tests target only `net9.0` (no net10.0 test execution)
- No property-based or fuzz tests for serialization boundaries (except `GeneratorsTests.cs` in Testing)
- No explicit concurrency stress tests (optimistic concurrency is tested but not under load)

---

## Roslyn Analyzer Coverage

### Shipped Rules (10)

| Rule ID | Category | Severity | Analyzer | Tests |
|---------|----------|----------|----------|-------|
| FAES0001 | Usage | Warning | `WhenUsageAnalyzer` | Yes |
| FAES0002 | Usage | Warning | `AppendWithoutApplyAnalyzer` | Yes |
| FAES0003 | Usage | Warning | `NonPartialAggregateAnalyzer` | Yes |
| FAES0004 | Usage | Info | `UnusedWhenEventParameterAnalyzer` | Yes |
| FAES0005 | CodeGeneration | Warning | `CodeGenerationRequiredAnalyzer` | Yes |
| FAES0006 | CodeGeneration | Warning | `CodeGenerationRequiredAnalyzer` | Yes |
| FAES0007 | CodeGeneration | Warning | `CodeGenerationRequiredAnalyzer` | Yes |
| FAES0012 | CodeGeneration | Warning | `ExtensionsRegistrationAnalyzer` | Yes |
| FAES0014 | CodeGeneration | Warning | `ExtensionsRegistrationAnalyzer` | Yes |
| FAES0015 | CodeGeneration | Warning | `VersionTokenGenerationAnalyzer` | Yes |

### Code Fixes (1)

| Rule | Fix | Tests |
|------|-----|-------|
| FAES0004 | Convert unused event parameter to `[When<T>]` attribute | Yes |

### Refactoring Providers (3)

| Provider | Tests |
|----------|-------|
| `StreamActionRefactoringProvider` | Yes |
| `ObjectNameRefactoringProvider` | Yes |
| `EventNameRefactoringProvider` | Yes |

### Missing Analyzer Rules (potential additions for v2)

| Suggested Rule | Description |
|----------------|-------------|
| FAES0008-0011 | Gap in rule ID sequence (reserved?) |
| FAES0013 | Gap in rule ID sequence (reserved?) |
| State mutation in commands | Detect property assignments outside `When` methods |
| Non-record events | Warn when event types are classes instead of records |
| Missing `[EventName]` | Events without `[EventName]` attribute |
| Missing `[Aggregate]` | Classes inheriting `Aggregate` without `[Aggregate]` attribute |
| Direct `When()` call in projection | Similar to FAES0001 but for projections |

**Note:** Rule IDs FAES0008-0011 and FAES0013 are gaps -- either reserved for future use or removed rules.

---

## Result Type Consistency

### Result Types

The library provides `Result` and `Result<T>` in `ErikLieben.FA.ES.Results` namespace.

| Feature | `Result` | `Result<T>` |
|---------|----------|-------------|
| IsSuccess / IsFailure | Yes | Yes |
| Error property | Yes | Yes |
| Success factory | `Result.Success()` | `Result<T>.Success(value)` |
| Failure factory | `Result.Failure(error)` | `Result<T>.Failure(error)` |
| Implicit conversion from Error | Yes | Yes |
| Implicit conversion from T | N/A | Yes |
| Map | N/A | Yes |
| Bind | N/A | Yes |
| OnSuccess | N/A | Yes |
| OnFailure | N/A | Yes |
| GetValueOrDefault | N/A | Yes |

### Error Factories (`EventSourcingErrors`)

| Error | Code | Tests |
|-------|------|-------|
| StreamNotFound | `STREAM_NOT_FOUND` | Yes |
| ConcurrencyConflict | `CONCURRENCY_CONFLICT` | Yes |
| AggregateNotFound | `AGGREGATE_NOT_FOUND` | Yes |
| AggregateAlreadyExists | `AGGREGATE_ALREADY_EXISTS` | Yes |
| EventDeserializationFailed | `EVENT_DESERIALIZATION_FAILED` | Yes |
| ProjectionNotFound | `PROJECTION_NOT_FOUND` | Yes |
| ProjectionSaveFailed | `PROJECTION_SAVE_FAILED` | Yes |
| SnapshotNotFound | `SNAPSHOT_NOT_FOUND` | Yes |
| StorageOperationFailed | `STORAGE_OPERATION_FAILED` | Yes |
| OperationCancelled | `OPERATION_CANCELLED` | Yes |
| Timeout | `TIMEOUT` | Yes |
| ValidationFailed | `VALIDATION_FAILED` | Yes |
| Error.Unknown | `UNKNOWN` | Yes |
| Error.NullValue | `NULL_VALUE` | Yes |
| Error.FromException | `EXCEPTION.{Type}` | Yes |

### Consistency Issues

1. **Result types are not used consistently across the library API.** Most public APIs throw exceptions rather than returning Result types. The `IEventStream.ReadAsync` returns `IReadOnlyCollection<IEvent>` and throws on failure rather than returning `Result<IReadOnlyCollection<IEvent>>`. The Result types appear to be for consumer use, not internal use.

2. **No `Result` versions of core operations** -- `IEventStream.Session()`, `IEventStream.ReadAsync()`, `IDocumentStore.GetAsync()`, etc., all return plain `Task` or `Task<T>` and throw exceptions. This is consistent with the current design (Result types are for application-level error handling, not library internals), but the documentation in CLAUDE.md implies they are for the library's own operations.

3. **No `OnFailure` on non-generic `Result`** -- The `Result` struct has no `OnSuccess`/`OnFailure`/`Map` methods. Only `Result<T>` has these functional combinators.

---

## Missing Features & Gaps

### Features Present in Code but Incomplete

| Feature | Status | Details |
|---------|--------|---------|
| Stream tiering | Blob only | `IBlobStreamTieringService` exists but no abstraction in core or S3/CosmosDB/Table |
| Retention evaluation | Partial | `IRetentionPolicyProvider` and `RetentionPolicy` exist; `IStreamMetadataProvider` only implemented for Blob and S3, not Table or CosmosDB |
| Snapshot cleanup | Core only | `ISnapshotCleanupService` and `SnapshotCleanupService` exist in core; rely on `ISnapShotStore.ListSnapshotsAsync` and `DeleteManyAsync` which are defined on the interface but provider implementations need verification |
| Backup/restore | Blob only | `IBackupProvider` and `AzureBlobBackupProvider` -- no Table, CosmosDB, or S3 implementations |
| Distributed locking | Blob only | `IDistributedLockProvider` with `BlobLeaseDistributedLockProvider` -- no other providers |
| Migration routing | Blob only | `IMigrationRoutingTable` implemented only as `BlobMigrationRoutingTable` |

### Features Not Yet Implemented

| Feature | Priority for v2 | Notes |
|---------|----------------|-------|
| Event replay (re-read all events) | Low | Covered via `ReadAsync(startVersion: 0)` |
| Global event stream subscription | Medium | No real-time subscription model; polling-based catch-up only |
| Event versioning in CLI | Done | `[EventVersion]` attribute and upcast support exist |
| Projection archival | Done | `ProjectionStatus.Archived` status exists |
| Dead letter handling | Low | No explicit dead letter queue for failed projections |
| Multi-region support | Low | No cross-region replication abstractions |
| Event encryption at rest | Low | Relies on storage provider encryption |

---

## Documentation Accuracy vs Implementation

### CLAUDE.md Accuracy Check

| Documented Feature | Accurate? | Notes |
|-------------------|-----------|-------|
| Aggregate pattern (`[Aggregate]`, partial, Fold) | Yes | Matches implementation |
| Event pattern (records, `[EventName]`) | Yes | Matches |
| Projection pattern (`[BlobJsonProjection]`) | Yes | Matches |
| `ObjectIdWhenValueFactory` | Yes | Exists in `Projections/ObjectIdWhenValueFactory.cs` |
| Fluent builder API (`AddFaes()`) | Yes | `FaesServiceCollectionExtensions.AddFaes()` exists |
| Classic `ConfigureBlobEventStore` | Yes | Exists in `ServiceCollectionExtensions` |
| `ConfigureTableEventStore` | Yes | Exists |
| `ConfigureCosmosDbEventStore` | Yes | Exists (as `ConfigureCosmosDbEventStore` in `ServiceCollectionExtensions.cs`) |
| `ConfigureS3EventStore` | Yes | Exists in `FaesBuilderExtensions.cs` |
| `[EventStream]` attribute for MinimalApis | Yes | Exists |
| `[Projection]` attribute for MinimalApis | Yes | Exists |
| Testing pattern (`AggregateTestBuilder`) | Yes | Exists in Testing project |
| Event upcasting (`IEventUpcast<TFrom, TTo>`) | Partially | The interface name in code is `IUpcastEvent` (not `IEventUpcast`). CLAUDE.md shows `[UseUpcaster<T>]` but the actual attribute is `[Upcaster]` per `UpcasterAttribute.cs`. |
| Result types | Yes | `Result`, `Result<T>`, `Error`, `EventSourcingErrors` all match |
| Catch-up discovery | Yes | `ICatchUpDiscoveryService` matches documentation |
| `dotnet faes` CLI | Yes | PackAsTool with ToolCommandName "faes" |

### Documentation Gaps

1. **Retention policies** are not documented in CLAUDE.md but are fully implemented (`[RetentionPolicy]` attribute, `IRetentionPolicyProvider`, `RetentionDiscoveryService`).
2. **Snapshot policies** are not documented in CLAUDE.md but are fully implemented (`[SnapshotPolicy]` attribute, `ISnapshotPolicyProvider`, `InlineSnapshotHandler`, `SnapshotCleanupService`).
3. **Observability** (OpenTelemetry metrics, `FaesMetrics`, `FaesInstrumentation`) is not documented.
4. **Projection status lifecycle** (Rebuilding, CatchingUp, Ready, Failed, Disabled, Archived) is not documented.
5. **Projection versioning** (`[ProjectionVersion]`, `NeedsSchemaUpgrade`, `IProjectionLoader`) is not documented.
6. **Stream actions** (`[StreamAction<T>]`, `IPreAppendAction`, `IPostAppendAction`, `IPostReadAction`, `IAsyncPostCommitAction`) are not documented.
7. **Aggregate storage registry** (`IAggregateStorageRegistry`) for per-aggregate provider selection is not documented.
8. **Uniqueness** (`UniqueIdGenerator`) is not documented.
9. **Validation** (`AggregateValidationExtensions`, `CheckpointValidation`, `DecisionContext`) is not documented.
10. **Event stream management** (migration, transformation, backup, repair) is mentioned only via catch-up reference.
11. **Upcaster naming** -- CLAUDE.md shows `IEventUpcast<TFrom, TTo>` and `[UseUpcaster<T>]` but actual code uses `IUpcastEvent` and `[Upcaster]`. This could confuse AI assistants.

---

## Breaking Changes from Previous Previews

Based on code analysis:

### Deprecated APIs (marked `[Obsolete]`)

1. **Projection.Fold with IObjectDocument** -- Three overloads of `Fold()` that take `IObjectDocument` are marked obsolete in favor of `VersionToken`-based overloads:
   - `Fold<T>(IEvent, IObjectDocument, T?, IExecutionContext?)`
   - `Fold(IEvent, IObjectDocument)`
   - `Fold(IEvent, IObjectDocument, IExecutionContext?)`

2. **GetWhenParameterValue with IObjectDocument** -- `GetWhenParameterValue<T, Te>(string, IObjectDocument, IEvent)` is deprecated in favor of version token-based overload.

### CLI Migrations (automated code updates)

The CLI includes migration support for:
- `DeprecatedFoldOverloadMigration` -- migrates old Fold calls
- `RenameIEventUpcasterMigration` -- renames upcaster interfaces
- `StaticDocumentTagFactoryMigration` -- updates document tag factory usage
- `UpdateUpcasterNamingConventionMigration` -- updates upcaster naming

### Potential Breaking Changes for v2

1. **VersionToken-based Projection.Fold** is now the primary API; IObjectDocument-based overloads are deprecated
2. **Upcaster naming conventions** changed (migration available)
3. **DocumentTagFactory** usage changed (migration available)
4. **CosmosDB Newtonsoft.Json dependency** may conflict with STJ-only consumers

---

## Recommendations

### Critical (v2 blockers)

1. **Add `net10.0` target to CosmosDB provider** (`src/ErikLieben.FA.ES.CosmosDb/ErikLieben.FA.ES.CosmosDb.csproj`)
   - Change `<TargetFramework>net9.0</TargetFramework>` to `<TargetFrameworks>net9.0;net10.0</TargetFrameworks>`
   - Update CosmosDB test project similarly
   - **File:** `D:\ErikLieben.FA.ES\src\ErikLieben.FA.ES.CosmosDb\ErikLieben.FA.ES.CosmosDb.csproj:4`

2. **Remove hardcoded `<Version>1.0.0</Version>` from CosmosDB** and use `$(PackageVersion)` pattern like other projects
   - **File:** `D:\ErikLieben.FA.ES\src\ErikLieben.FA.ES.CosmosDb\ErikLieben.FA.ES.CosmosDb.csproj:10`

3. **Decide on WebJobs.Isolated.Extensions** -- target `net8.0` only and deprecate, or upgrade to `net9.0;net10.0`
   - **File:** `D:\ErikLieben.FA.ES\src\ErikLieben.FA.ES.WebJobs.Isolated.Extensions\ErikLieben.FA.ES.WebJobs.Isolated.Extensions.csproj:3`

### High Priority

4. **Populate `PublicAPI.Shipped.txt`** for the core library and consider adding PublicAPI tracking to all packable projects. Re-enable RS0016 to catch accidental API surface changes.
   - **File:** `D:\ErikLieben.FA.ES\src\ErikLieben.FA.ES\PublicAPI.Shipped.txt`

5. **Implement `IStreamMetadataProvider` for Table and CosmosDB** to enable retention policy evaluation across all providers.

6. **Implement `IProjectionStatusCoordinator` for S3** to enable projection rebuild lifecycle on S3.

7. **Fix upcaster naming in CLAUDE.md** -- Update `IEventUpcast<TFrom, TTo>` to `IUpcastEvent` and `[UseUpcaster<T>]` to `[Upcaster]` (or whichever is the current name).

8. **Standardize package descriptions** -- CosmosDB, S3, EventStreamManagement, and MinimalApis have specific descriptions while others use the generic "AOT-friendly Event Sourcing toolkit" text.

### Medium Priority

9. **Add S3 integration tests** with a MinIO container fixture (similar to `AzuriteContainerFixture` and `CosmosDbContainerFixture`).

10. **Document undocumented features** in CLAUDE.md: retention policies, snapshot policies, observability, projection status lifecycle, stream actions, aggregate storage registry, validation, and event stream management.

11. **Add `GenerateDocumentationFile` to Testing project** -- currently missing (other projects have it).

12. **Consider abstracting tiering** -- Create an `IStreamTieringService` in core and implement for S3 (S3 storage classes) and CosmosDB (throughput scaling).

### Low Priority

13. **Consider adding functional combinators to non-generic `Result`** -- `OnSuccess(Action)` and `OnFailure(Action<Error>)` for consistency with `Result<T>`.

14. **Fill in analyzer rule ID gaps** (FAES0008-0011, FAES0013) -- either reserve them explicitly or implement new rules.

15. **Evaluate Newtonsoft.Json dependency in CosmosDB** -- The Microsoft.Azure.Cosmos SDK requires it, but for AOT-first consumers this adds complexity. Consider if a System.Text.Json-only serializer (which exists as `CosmosDbSystemTextJsonSerializer`) can fully replace the Newtonsoft dependency.

16. **Consider adding `IBackupProvider` implementations** for Table, CosmosDB, and S3 to enable backup/restore across all providers.
