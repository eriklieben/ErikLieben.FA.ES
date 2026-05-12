using System.Text.Json;
using ErikLieben.FA.ES.Observability;
using ErikLieben.FA.ES.Projections;
using Npgsql;
using NpgsqlTypes;

namespace ErikLieben.FA.ES.Postgres.Projections;

/// <summary>
/// Base factory for projections persisted as a single JSONB row in a dedicated PostgreSQL table.
/// </summary>
/// <typeparam name="T">The projection type that inherits from <see cref="Projection"/>.</typeparam>
/// <remarks>
/// Table shape: <c>(id text primary key, version bigint not null, updated_at timestamptz not null, payload jsonb not null)</c>.
/// Status is stored inline under the <c>$status</c> property of the JSONB payload, mirroring the blob provider.
/// External checkpoints are stored in the shared <c>faes_projection_checkpoints</c> table, keyed by
/// <c>(projection_type, fingerprint)</c>; entries are immutable.
/// </remarks>
public abstract class PostgresJsonbProjectionFactory<T> : IProjectionFactory<T>, IProjectionFactory
    where T : Projection
{
    private const string StatusPropertyName = "$status";
    private const string CheckpointsTable = "faes_projection_checkpoints";

    private readonly NpgsqlDataSource dataSource;
    private readonly string qualifiedTable;
    private readonly string tableName;
    private readonly string schemaName;
    private readonly Lazy<Task> ensureTable;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="dataSource">The Npgsql data source.</param>
    /// <param name="table">Unquoted table name (e.g. <c>faes_proj_dev_tunnel_token_list</c>).</param>
    /// <param name="schema">Unquoted schema name; defaults to <c>public</c> when null/empty.</param>
    protected PostgresJsonbProjectionFactory(
        NpgsqlDataSource dataSource,
        string table,
        string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ValidateIdentifier(table, nameof(table));
        var resolvedSchema = string.IsNullOrWhiteSpace(schema) ? "public" : schema;
        ValidateIdentifier(resolvedSchema, nameof(schema));

        this.dataSource = dataSource;
        this.tableName = table;
        this.schemaName = resolvedSchema;
        this.qualifiedTable = $"\"{this.schemaName}\".\"{this.tableName}\"";
        this.ensureTable = new Lazy<Task>(EnsureTableAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private async Task EnsureTableAsync()
    {
        var ddl =
            $"CREATE TABLE IF NOT EXISTS {qualifiedTable} (" +
            "id text PRIMARY KEY, " +
            "version bigint NOT NULL, " +
            "updated_at timestamptz NOT NULL, " +
            "payload jsonb NOT NULL)";
        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(ddl, connection);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private Task EnsureTable() => ensureTable.Value;

    private static void ValidateIdentifier(string identifier, string paramName)
    {
        // Only [A-Za-z_][A-Za-z0-9_]* — Postgres unquoted-identifier shape, but applied to the
        // *content* of a quoted identifier. This guarantees no embedded double-quote can ever
        // break out of the "..."."..." quoting we apply in qualifiedTable.
        if (identifier.Length == 0)
        {
            throw new ArgumentException("Identifier must not be empty.", paramName);
        }
        var first = identifier[0];
        if (!(char.IsAsciiLetter(first) || first == '_'))
        {
            throw new ArgumentException(
                $"Postgres identifier '{identifier}' must start with a letter or underscore.", paramName);
        }
        for (var i = 1; i < identifier.Length; i++)
        {
            var c = identifier[i];
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                throw new ArgumentException(
                    $"Postgres identifier '{identifier}' may only contain ASCII letters, digits, or underscores.", paramName);
            }
        }
    }

    /// <summary>Gets a value indicating whether the projection externalizes its checkpoint.</summary>
    protected abstract bool HasExternalCheckpoint { get; }

    /// <summary>The underlying Npgsql data source.</summary>
    protected NpgsqlDataSource DataSource => dataSource;

    /// <summary>The schema in which projection rows live.</summary>
    protected string Schema => schemaName;

    /// <summary>The (unquoted) table name for this projection's rows.</summary>
    protected string Table => tableName;

    /// <summary>The fully-quoted <c>"schema"."table"</c> identifier.</summary>
    protected string QualifiedTable => qualifiedTable;

    /// <summary>Creates a new instance of the projection.</summary>
    protected abstract T New();

    /// <summary>Deserializes a projection from JSON using the generated <c>LoadFromJson</c>.</summary>
    protected abstract T? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory);

    /// <inheritdoc />
    public Type ProjectionType => typeof(T);

    /// <inheritdoc />
    public virtual async Task<T> GetOrCreateAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("PostgresJsonbProjectionFactory.GetOrCreate");
        var id = ResolveId(blobName);

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.ProjectionType, typeof(T).Name);
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemPostgres);
            activity.SetTag(FaesSemanticConventions.DbName, qualifiedTable);
        }

        var json = await LoadPayloadAsync(id, cancellationToken).ConfigureAwait(false);
        if (json is null)
        {
            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag(FaesSemanticConventions.LoadedFromCache, false);
            }
            return New();
        }

        var projection = LoadFromJson(json, documentFactory, eventStreamFactory) ?? New();
        await TryLoadExternalCheckpointAsync(projection, cancellationToken).ConfigureAwait(false);

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.LoadedFromCache, true);
        }
        return projection;
    }

    /// <inheritdoc />
    public virtual async Task SaveAsync(
        T projection,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        using var activity = FaesInstrumentation.Projections.StartActivity("PostgresJsonbProjectionFactory.Save");
        var id = ResolveId(blobName);

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.ProjectionType, typeof(T).Name);
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemPostgres);
            activity.SetTag(FaesSemanticConventions.DbName, qualifiedTable);
            activity.SetTag(FaesSemanticConventions.ProjectionStatus, projection.Status.ToString());
        }

        var json = projection.ToJson();

        await EnsureTable().ConfigureAwait(false);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql =
            $"INSERT INTO {qualifiedTable} (id, version, updated_at, payload) " +
            "VALUES ($1, 1, now(), $2::jsonb) " +
            "ON CONFLICT (id) DO UPDATE SET " +
            $"  version = {qualifiedTable}.version + 1, updated_at = now(), payload = EXCLUDED.payload";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = id });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json });
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (HasExternalCheckpoint && !string.IsNullOrEmpty(projection.CheckpointFingerprint))
        {
            await SaveCheckpointAsync(projection, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var id = ResolveId(blobName);
        await EnsureTable().ConfigureAwait(false);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand($"SELECT 1 FROM {qualifiedTable} WHERE id = $1 LIMIT 1", connection);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = id });
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    /// <inheritdoc />
    public virtual async Task<DateTimeOffset?> GetLastModifiedAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var id = ResolveId(blobName);
        await EnsureTable().ConfigureAwait(false);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand($"SELECT updated_at FROM {qualifiedTable} WHERE id = $1", connection);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = id });
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is DateTime dt ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)) : (DateTimeOffset?)null;
    }

    /// <inheritdoc />
    public virtual async Task SetStatusAsync(
        ProjectionStatus status,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var id = ResolveId(blobName);
        var existing = await LoadPayloadAsync(id, cancellationToken).ConfigureAwait(false);

        string updatedJson;
        if (existing is null)
        {
            updatedJson = JsonSerializer.Serialize(new { Status = status }, StatusOnlyJsonContext.Default.StatusOnly);
        }
        else
        {
            updatedJson = RewritePayloadStatus(existing, status);
        }

        await EnsureTable().ConfigureAwait(false);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql =
            $"INSERT INTO {qualifiedTable} (id, version, updated_at, payload) " +
            "VALUES ($1, 1, now(), $2::jsonb) " +
            "ON CONFLICT (id) DO UPDATE SET updated_at = now(), payload = EXCLUDED.payload";
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = id });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = updatedJson });
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<ProjectionStatus> GetStatusAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var id = ResolveId(blobName);
        var json = await LoadPayloadAsync(id, cancellationToken).ConfigureAwait(false);
        if (json is null)
        {
            return ProjectionStatus.Active;
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty(StatusPropertyName, out var statusElement)
            && statusElement.TryGetInt32(out var statusValue))
        {
            return (ProjectionStatus)statusValue;
        }
        return ProjectionStatus.Active;
    }

    /// <inheritdoc />
    public async Task<Projection> GetOrCreateProjectionAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default)
        => await GetOrCreateAsync(documentFactory, eventStreamFactory, blobName, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SaveProjectionAsync(
        Projection projection,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        if (projection is not T typed)
        {
            throw new ArgumentException($"Projection must be of type {typeof(T).Name}", nameof(projection));
        }
        await SaveAsync(typed, blobName, cancellationToken).ConfigureAwait(false);
    }

    private string ResolveId(string? blobName)
    {
        if (!string.IsNullOrEmpty(blobName))
        {
            // Blob callers pass things like "MyProjection.json"; strip the extension for parity.
            var trimmed = blobName;
            if (trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^5];
            }
            return trimmed;
        }
        return typeof(T).Name;
    }

    private async Task<string?> LoadPayloadAsync(string id, CancellationToken cancellationToken)
    {
        await EnsureTable().ConfigureAwait(false);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand($"SELECT payload::text FROM {qualifiedTable} WHERE id = $1", connection);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = id });
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    private async Task TryLoadExternalCheckpointAsync(T projection, CancellationToken cancellationToken)
    {
        if (!HasExternalCheckpoint || string.IsNullOrEmpty(projection.CheckpointFingerprint))
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            $"SELECT checkpoint::text FROM \"{schemaName}\".\"{CheckpointsTable}\" WHERE projection_type = $1 AND fingerprint = $2",
            connection);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = typeof(T).Name });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = projection.CheckpointFingerprint });
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is string checkpointJson)
        {
            var checkpoint = JsonSerializer.Deserialize(checkpointJson, CheckpointJsonContext.Default.Checkpoint);
            if (checkpoint != null)
            {
                projection.Checkpoint = checkpoint;
            }
        }
    }

    private async Task SaveCheckpointAsync(T projection, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(projection.Checkpoint, CheckpointJsonContext.Default.Checkpoint);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            $"INSERT INTO \"{schemaName}\".\"{CheckpointsTable}\" (projection_type, fingerprint, checkpoint) " +
            "VALUES ($1, $2, $3::jsonb) ON CONFLICT (projection_type, fingerprint) DO NOTHING",
            connection);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = typeof(T).Name });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = projection.CheckpointFingerprint! });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json });
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string RewritePayloadStatus(string json, ProjectionStatus status)
    {
        using var doc = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            var wroteStatus = false;
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Name == StatusPropertyName)
                {
                    writer.WriteNumber(StatusPropertyName, (int)status);
                    wroteStatus = true;
                }
                else
                {
                    property.WriteTo(writer);
                }
            }
            if (!wroteStatus)
            {
                writer.WriteNumber(StatusPropertyName, (int)status);
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}

internal record StatusOnly
{
    [System.Text.Json.Serialization.JsonPropertyName("$status")]
    public ProjectionStatus Status { get; init; }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(StatusOnly))]
internal partial class StatusOnlyJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
