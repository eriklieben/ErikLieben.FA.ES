namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Indicates that a projection is stored as a JSONB row in a dedicated PostgreSQL table.
/// </summary>
/// <remarks>
/// Each projection type maps to one table with the shape
/// <c>(id text primary key, version bigint not null, updated_at timestamptz not null, payload jsonb not null)</c>.
/// The table is created at startup by <see cref="PostgresSchemaBootstrapper"/> if it does not already exist.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PostgresJsonbProjectionAttribute : Attribute
{
    /// <summary>
    /// Gets the table name used to store the projection rows.
    /// When <c>null</c>, codegen derives a snake_cased name from the projection class (e.g. <c>faes_proj_dev_tunnel_token_list</c>).
    /// </summary>
    public string? Table { get; init; }

    /// <summary>
    /// Gets the schema in which the projection table lives. Defaults to the event-store schema (typically <c>public</c>).
    /// </summary>
    public string? Schema { get; init; }

    /// <summary>
    /// Gets the named <c>NpgsqlDataSource</c> connection used to resolve the data source.
    /// When <c>null</c>, the default singleton <c>NpgsqlDataSource</c> is used.
    /// </summary>
    public string? Connection { get; init; }
}
