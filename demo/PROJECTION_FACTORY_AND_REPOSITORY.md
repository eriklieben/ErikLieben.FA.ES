# Projection Factory and Repository Pattern Documentation

## Current State

### Projection Factory Generation

Unlike Aggregates which always generate a Factory class, Projections have **conditional** factory generation:

#### When is a Projection Factory Generated?

A factory is **only** generated when the projection is configured as a **Blob Projection** with specific storage settings.

**Example Blob Projection Attribute:**
```csharp
[BlobProjection(Connection = "BlobStorage", Container = "projections")]
public partial class MyProjection : Projection
{
    // ... projection logic
}
```

#### Generated Factory Structure

When blob projection settings are present, the CLI generates:

```csharp
public class MyProjectionFactory(
    IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
    IObjectDocumentFactory objectDocumentFactory,
    IEventStreamFactory eventStreamFactory)
    : BlobProjectionFactory<MyProjection>(
        blobServiceClientFactory,
        "BlobStorage",      // Connection name
        "projections")      // Container name
{
    protected override bool HasExternalCheckpoint => false;

    protected override MyProjection New()
    {
        return new MyProjection(objectDocumentFactory, eventStreamFactory);
    }
}
```

**Key characteristics:**
- Inherits from `BlobProjectionFactory<T>`
- Responsible for creating new projection instances
- Manages blob storage connection for persisted projections
- Handles checkpoint management (internal vs external)

### Current TaskFlow Projections

Neither `ProjectDashboard` nor `ActiveWorkItems` currently have factories because they don't use blob projection storage:

#### ProjectDashboard.Generated.cs
```csharp
// No factory class generated

public partial class ProjectDashboard : IProjectDashboard
{
    public ProjectDashboard() : base() { }

    public ProjectDashboard(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
        : base(documentFactory, eventStreamFactory) { }

    // Static factory method instead
    public static ProjectDashboard? LoadFromJson(
        string json,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory)
    {
        var obj = JsonSerializer.Deserialize(json, ...);
        return new ProjectDashboard(documentFactory, eventStreamFactory);
    }
}
```

## Aggregate vs Projection Comparison

| Feature | Aggregates | Projections |
|---------|-----------|-------------|
| **Factory Generation** | Always (for partial classes) | Conditional (blob projections only) |
| **Factory Purpose** | Create/Load aggregates from event streams | Create projection instances for blob storage |
| **Repository** | Always generated | ❌ Not currently generated |
| **Factory Methods** | `CreateAsync`, `GetAsync`, `GetFirstByDocumentTag`, etc. | `New()` only |
| **DI Registration** | Factory + Repository (Scoped) | Factory only (when applicable) |
| **Storage Model** | Event-sourced (append-only event stream) | Materialized view (snapshot of current state) |
| **Querying** | Via Repository with pagination | Direct access to projection instance |

## Key Differences Explained

### 1. Storage Semantics

**Aggregates:**
- Event-sourced: reconstituted from event history
- Factory loads events and calls `Fold()` to rebuild state
- Each aggregate instance represents a single entity

**Projections:**
- Materialized views: pre-computed state stored as blob/JSON
- Factory creates new instances or loads serialized state
- Single projection instance can represent many entities (e.g., dashboard showing all projects)

### 2. Factory Responsibilities

**Aggregate Factory:**
```csharp
// Handles event stream loading and folding
public async Task<Order> CreateAsync(Guid id)
{
    var document = await objectDocumentFactory.GetOrCreateAsync(ObjectName, id.ToString());
    var eventStream = eventStreamFactory.Create(document);
    var obj = new Order(eventStream);
    await obj.Fold();  // ← Rebuild from events
    return obj;
}
```

**Projection Factory (when used):**
```csharp
// Simple instantiation for blob storage
protected override ProjectDashboard New()
{
    return new ProjectDashboard(objectDocumentFactory, eventStreamFactory);
}
```

### 3. Querying Patterns

**Aggregates:**
- Need Repository to list/query instances
- Pagination required for large datasets
- Each aggregate is independent

**Projections:**
- Typically singleton or few instances
- Query methods built into projection class
- Example: `GetProjectMetrics(projectId)`, `GetActiveProjects()`

## Do Projections Need Repositories?

### Current Design Philosophy

Projections are designed differently from Aggregates:

1. **Single Instance Pattern**: Many projections are singletons (one instance for entire system)
   - Example: `ProjectDashboard` - one instance tracking all projects
   - No need to "find" or "list" dashboard instances

2. **Built-in Querying**: Projections expose query methods directly
   ```csharp
   public class ProjectDashboard : Projection
   {
       public ProjectMetrics? GetProjectMetrics(string projectId) { }
       public IEnumerable<ProjectMetrics> GetActiveProjects() { }
   }
   ```

3. **Lifecycle**: Projections are typically:
   - Created once
   - Updated continuously as events are processed
   - Persisted periodically to blob storage (if configured)

### When Would a Projection Repository Be Useful?

A repository pattern might make sense for projections if:

1. **Multiple Projection Instances Per Type**
   - Example: One `UserProfile` projection per user
   - Need to list/page through user profiles
   - Similar cardinality to aggregates

2. **Dynamic Projection Discovery**
   - Need to find which projections exist
   - Query projections by metadata/tags

3. **Projection Management**
   - Rebuild/reset specific projections
   - List all projections of a certain type
   - Check projection health/checkpoint status

### Proposed Repository Interface (If Needed)

```csharp
public partial interface IProjectDashboardRepository
{
    /// <summary>
    /// Gets the singleton projection instance (or creates if doesn't exist)
    /// </summary>
    Task<ProjectDashboard> GetOrCreateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current projection state to blob storage
    /// </summary>
    Task SaveAsync(ProjectDashboard projection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current checkpoint position
    /// </summary>
    Task<Checkpoint> GetCheckpointAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the projection (deletes stored state and checkpoint)
    /// </summary>
    Task ResetAsync(CancellationToken cancellationToken = default);
}
```

For projections with multiple instances:

```csharp
public partial interface IUserProfileRepository
{
    /// <summary>
    /// Lists all user profile projection instances
    /// </summary>
    Task<PagedResult<string>> GetProjectionIdsAsync(
        string? continuationToken = null,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific user profile projection
    /// </summary>
    Task<UserProfile?> GetByIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user profile projection exists
    /// </summary>
    Task<bool> ExistsAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
```

## Recommendations

### For Singleton Projections (Current Pattern)

**Examples**: `ProjectDashboard`, `ActiveWorkItems`, system-wide reports

**No Repository Needed**:
- Access via DI: `IProjectDashboard` or concrete `ProjectDashboard`
- Built-in query methods on projection class
- BlobProjectionFactory handles persistence (when configured)

**Usage:**
```csharp
public class MyService
{
    private readonly ProjectDashboard dashboard;

    public MyService(ProjectDashboard dashboard)
    {
        this.dashboard = dashboard;
    }

    public ProjectMetrics GetMetrics(string projectId)
    {
        return dashboard.GetProjectMetrics(projectId);
    }
}
```

### For Multi-Instance Projections (If Needed)

**Examples**: Per-user views, per-tenant reports, geo-specific aggregations

**Repository Would Help**:
- Similar to Aggregate pattern
- List/page through projection instances
- Check existence, count instances
- Consistent API with Aggregate repositories

**Decision Factors:**
1. **Cardinality**: How many instances? (1 = no repository, many = repository)
2. **Discovery**: Need to list/find instances? (yes = repository)
3. **Lifecycle**: Created once or dynamically? (dynamic = repository)

## Code Generation Implications

### If Repository Generation is Added

Would need to detect projection cardinality:

**Option 1: Attribute-Based**
```csharp
[Projection(Cardinality = ProjectionCardinality.Singleton)]
public partial class ProjectDashboard : Projection { }

[Projection(Cardinality = ProjectionCardinality.Multiple)]
public partial class UserProfile : Projection { }
```

**Option 2: Convention-Based**
- Blob projections with parameterless constructor = Singleton
- Blob projections with ID parameter = Multiple instances

**Option 3: Explicit Repository Attribute**
```csharp
[GenerateRepository]
public partial class UserProfile : Projection { }
```

### DI Registration

```csharp
// Singleton projection - register instance
services.AddSingleton<ProjectDashboard>();
services.AddSingleton<IProjectDashboard>(sp => sp.GetRequiredService<ProjectDashboard>());

// Multi-instance projection - register repository
services.AddScoped<IUserProfileRepository, UserProfileRepository>();
```

## Current Implementation Status

✅ **Implemented:**
- Conditional projection factory generation (blob projections)
- Static `LoadFromJson` method for all projections
- Interface generation for all projections
- Built-in query methods pattern

❌ **Not Implemented:**
- Repository pattern for projections
- Pagination for multi-instance projections
- Projection instance discovery/listing
- Consistent CRUD API across projections

## Questions to Consider

1. **Do we have use cases for multi-instance projections?**
   - User-specific views?
   - Tenant-specific aggregations?
   - Region-specific reports?

2. **Should repository generation be:**
   - Always enabled (like aggregates)?
   - Opt-in via attribute?
   - Based on cardinality detection?

3. **Checkpoint management:**
   - Should repositories handle checkpoint operations?
   - External vs internal checkpoint strategies?

4. **Backward compatibility:**
   - Keep current singleton pattern?
   - Migrate existing projections?

## Next Steps

If repository generation for projections is desired:

1. ✅ Document current state (this file)
2. ⏳ Decide on generation strategy (always/opt-in/cardinality-based)
3. ⏳ Design repository interface for projections
4. ⏳ Implement generation in `GenerateProjectionCode.cs`
5. ⏳ Update `GenerateExtensionCode.cs` for DI registration
6. ⏳ Write unit tests
7. ⏳ Update documentation and migration guide

---

**Document Status**: Draft
**Last Updated**: 2025-11-15
**Related**: [REPOSITORY_GENERATION_PROPOSAL.md](./REPOSITORY_GENERATION_PROPOSAL.md) (Aggregates)
