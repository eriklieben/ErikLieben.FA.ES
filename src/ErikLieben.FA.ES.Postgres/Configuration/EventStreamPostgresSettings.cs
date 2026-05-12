namespace ErikLieben.FA.ES.Postgres.Configuration;

/// <summary>
/// Configuration for the Postgres event store provider.
/// </summary>
public record EventStreamPostgresSettings
{
    private const string DefaultStoreType = "postgres";

    /// <summary>
    /// The Npgsql connection string. Required.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Controls whether the schema bootstrapper runs and creates/updates the schema on startup.
    /// </summary>
    public SchemaBootstrapMode AutoCreate { get; init; } = SchemaBootstrapMode.CreateOrUpdate;

    /// <summary>
    /// Append mode. Optimistic relies on the document row's version check inside <c>faes_append</c>.
    /// Exclusive additionally acquires a per-stream <c>pg_advisory_xact_lock</c> for hot streams.
    /// </summary>
    public AppendMode AppendMode { get; init; } = AppendMode.Optimistic;

    /// <summary>
    /// When true and the object_name has no dedicated partition yet, the bootstrapper creates one.
    /// When false, new object_names fall into <c>faes_events_default</c>.
    /// </summary>
    public bool AutoCreatePartitionsPerObjectName { get; init; }

    /// <summary>
    /// The default data store type identifier. Default: "postgres".
    /// </summary>
    public string DefaultDataStore { get; init; } = DefaultStoreType;

    /// <summary>
    /// The default document store type identifier. Default: "postgres".
    /// </summary>
    public string DefaultDocumentStore { get; init; } = DefaultStoreType;

    /// <summary>
    /// The default snapshot store type identifier. Default: "postgres".
    /// </summary>
    public string DefaultSnapShotStore { get; init; } = DefaultStoreType;

    /// <summary>
    /// The default document tag store type identifier. Default: "postgres".
    /// </summary>
    public string DefaultDocumentTagStore { get; init; } = DefaultStoreType;

    /// <summary>
    /// The default stream tag store type identifier. Default: "postgres".
    /// </summary>
    public string DefaultEventStreamTagType { get; init; } = DefaultStoreType;

    /// <summary>
    /// Page size for streaming reads. Default: 256.
    /// </summary>
    public int StreamingPageSize { get; init; } = 256;
}

/// <summary>
/// Schema bootstrap behavior.
/// </summary>
public enum SchemaBootstrapMode
{
    /// <summary>Do not touch the schema. Production-safe default for hand-managed migrations.</summary>
    None = 0,
    /// <summary>Create missing tables/functions. Never drops or alters existing objects.</summary>
    CreateOrUpdate = 1,
}

/// <summary>
/// Per-stream append concurrency strategy.
/// </summary>
public enum AppendMode
{
    /// <summary>Document-row version check inside <c>faes_append</c>. Default.</summary>
    Optimistic = 0,
    /// <summary>Adds a per-stream <c>pg_advisory_xact_lock</c> to serialize writers on hot streams.</summary>
    Exclusive = 1,
}
