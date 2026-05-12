using System.Text.Json;
using ErikLieben.FA.ES.Projections;
using Npgsql;
using NpgsqlTypes;

namespace ErikLieben.FA.ES.Postgres.Projections;

/// <summary>
/// Base factory for routed projections persisted across multiple PostgreSQL tables: the routed
/// projection's main row (checkpoint + destination registry) lives in its own table; each
/// destination type lives in a separate table keyed by destination key.
/// </summary>
/// <typeparam name="TProjection">The routed projection type.</typeparam>
/// <remarks>
/// The destination table for a given destination type is resolved by the source-generated override
/// of <see cref="GetDestinationTable"/>. The destination row id equals the destination key.
/// External checkpoints (for either the main projection or any destination) reuse the shared
/// <c>faes_projection_checkpoints</c> table, keyed by <c>(projection_type, fingerprint)</c>.
/// </remarks>
public abstract class RoutedPostgresJsonbProjectionFactory<TProjection>
    : PostgresJsonbProjectionFactory<TProjection>
    where TProjection : RoutedProjection, new()
{
    private const string CheckpointsTable = "faes_projection_checkpoints";

    /// <summary>Initializes a new instance.</summary>
    protected RoutedPostgresJsonbProjectionFactory(
        NpgsqlDataSource dataSource,
        string table,
        string? schema = null)
        : base(dataSource, table, schema)
    {
    }

    /// <inheritdoc />
    protected override bool HasExternalCheckpoint => true;

    /// <inheritdoc />
    protected override TProjection New() => new TProjection();

    /// <inheritdoc />
    protected override TProjection? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
        => throw new NotSupportedException("Routed projections use LoadMainProjectionFromJson instead.");

    /// <summary>Deserializes the routed projection's main row (checkpoint + registry).</summary>
    protected abstract TProjection? LoadMainProjectionFromJson(
        string json,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory);

    /// <summary>Deserializes one destination row's payload.</summary>
    protected abstract Projection LoadDestinationFromJson(
        string json,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string destinationKey);

    /// <summary>Returns true when the named destination type uses an external checkpoint.</summary>
    protected abstract bool DestinationHasExternalCheckpoint(string destinationTypeName);

    /// <summary>Returns the (schema, table) for the named destination type.</summary>
    protected abstract (string schema, string table) GetDestinationTable(string destinationTypeName);

    /// <summary>Plugs the document/event-stream factories back into a freshly-loaded projection.</summary>
    protected abstract void SetFactories(
        TProjection projection,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory);

    /// <summary>Attaches a freshly-loaded destination to the routed projection.</summary>
    protected abstract void AddDestinationToProjection(
        TProjection projection,
        string destinationKey,
        Projection destination);

    /// <summary>Serializes the routed projection's main row (checkpoint + registry).</summary>
    protected abstract string SerializeMainProjection(TProjection projection);

    /// <inheritdoc />
    public override async Task<TProjection> GetOrCreateAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var mainId = ResolveMainId(blobName);
        var mainJson = await ReadPayloadAsync(QualifiedTable, mainId, ensureTable: true, cancellationToken).ConfigureAwait(false);

        var projection = mainJson is null
            ? New()
            : LoadMainProjectionFromJson(mainJson, documentFactory, eventStreamFactory) ?? New();

        SetFactories(projection, documentFactory, eventStreamFactory);

        foreach (var (destinationKey, destMetadata) in projection.Registry.Destinations)
        {
            var (destSchema, destTable) = GetDestinationTable(destMetadata.DestinationTypeName);
            var destQualified = $"\"{destSchema}\".\"{destTable}\"";
            var destJson = await ReadPayloadAsync(destQualified, destinationKey, ensureTable: true, cancellationToken).ConfigureAwait(false);
            if (destJson is null)
            {
                continue;
            }

            var destination = LoadDestinationFromJson(destJson, documentFactory, eventStreamFactory, destinationKey);
            if (destMetadata.UserMetadata.Count > 0)
            {
                destination.InitializeFromMetadata(destMetadata.UserMetadata);
            }

            if (DestinationHasExternalCheckpoint(destMetadata.DestinationTypeName)
                && !string.IsNullOrEmpty(destination.CheckpointFingerprint))
            {
                var checkpoint = await LoadCheckpointAsync(destMetadata.DestinationTypeName, destination.CheckpointFingerprint, cancellationToken).ConfigureAwait(false);
                if (checkpoint != null)
                {
                    destination.Checkpoint = checkpoint;
                }
            }

            AddDestinationToProjection(projection, destinationKey, destination);
        }

        return projection;
    }

    /// <inheritdoc />
    public override async Task SaveAsync(
        TProjection projection,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        projection.Registry.LastUpdated = DateTimeOffset.UtcNow;

        var mainId = ResolveMainId(blobName);
        var mainJson = SerializeMainProjection(projection);
        await UpsertPayloadAsync(QualifiedTable, mainId, mainJson, cancellationToken).ConfigureAwait(false);

        if (projection.Destinations == null)
        {
            return;
        }

        foreach (var kvp in projection.Destinations)
        {
            var destinationKey = kvp.Key;
            var destination = kvp.Value;
            if (!projection.Registry.Destinations.TryGetValue(destinationKey, out var metadata))
            {
                continue;
            }

            var (destSchema, destTable) = GetDestinationTable(metadata.DestinationTypeName);
            var destQualified = $"\"{destSchema}\".\"{destTable}\"";
            await UpsertPayloadAsync(destQualified, destinationKey, destination.ToJson(), cancellationToken).ConfigureAwait(false);

            if (DestinationHasExternalCheckpoint(metadata.DestinationTypeName)
                && !string.IsNullOrEmpty(destination.CheckpointFingerprint))
            {
                await SaveCheckpointAsync(metadata.DestinationTypeName, destination.CheckpointFingerprint, destination.Checkpoint, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static string ResolveMainId(string? blobName)
    {
        if (!string.IsNullOrEmpty(blobName))
        {
            return blobName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? blobName[..^5] : blobName;
        }
        return typeof(TProjection).Name;
    }

    private async Task<string?> ReadPayloadAsync(string qualifiedTable, string id, bool ensureTable, CancellationToken cancellationToken)
    {
        if (ensureTable)
        {
            await EnsureTableAsync(qualifiedTable, cancellationToken).ConfigureAwait(false);
        }
        await using var conn = await DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand($"SELECT payload::text FROM {qualifiedTable} WHERE id = $1", conn);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = id });
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    private async Task UpsertPayloadAsync(string qualifiedTable, string id, string json, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(qualifiedTable, cancellationToken).ConfigureAwait(false);
        await using var conn = await DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql =
            $"INSERT INTO {qualifiedTable} (id, version, updated_at, payload) " +
            "VALUES ($1, 1, now(), $2::jsonb) " +
            "ON CONFLICT (id) DO UPDATE SET " +
            $"  version = {qualifiedTable}.version + 1, updated_at = now(), payload = EXCLUDED.payload";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = id });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json });
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureTableAsync(string qualifiedTable, CancellationToken cancellationToken)
    {
        var ddl =
            $"CREATE TABLE IF NOT EXISTS {qualifiedTable} (" +
            "id text PRIMARY KEY, " +
            "version bigint NOT NULL, " +
            "updated_at timestamptz NOT NULL, " +
            "payload jsonb NOT NULL)";
        await using var conn = await DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(ddl, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Checkpoint?> LoadCheckpointAsync(string projectionTypeName, string fingerprint, CancellationToken cancellationToken)
    {
        await using var conn = await DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            $"SELECT checkpoint::text FROM \"{Schema}\".\"{CheckpointsTable}\" WHERE projection_type = $1 AND fingerprint = $2",
            conn);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = projectionTypeName });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = fingerprint });
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is string json)
        {
            return JsonSerializer.Deserialize(json, CheckpointJsonContext.Default.Checkpoint);
        }
        return null;
    }

    private async Task SaveCheckpointAsync(string projectionTypeName, string fingerprint, Checkpoint checkpoint, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(checkpoint, CheckpointJsonContext.Default.Checkpoint);
        await using var conn = await DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            $"INSERT INTO \"{Schema}\".\"{CheckpointsTable}\" (projection_type, fingerprint, checkpoint) " +
            "VALUES ($1, $2, $3::jsonb) ON CONFLICT (projection_type, fingerprint) DO NOTHING",
            conn);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = projectionTypeName });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = fingerprint });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json });
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
