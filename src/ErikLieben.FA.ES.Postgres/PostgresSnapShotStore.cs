using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Snapshots;
using Npgsql;
using NpgsqlTypes;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Postgres-backed snapshot store. One row per (stream_id, version, name) in <c>faes_snapshots</c>.
/// </summary>
internal sealed class PostgresSnapShotStore : ISnapShotStore
{
    private readonly NpgsqlDataSource dataSource;

    public PostgresSnapShotStore(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    public async Task SetAsync(IBase @object, JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@object);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        ArgumentNullException.ThrowIfNull(document);

        var json = JsonSerializer.Serialize(@object, jsonTypeInfo);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO faes_snapshots (stream_id, version, name, data, data_type, size_bytes) " +
            "VALUES ($1, $2, $3, $4::jsonb, $5, $6) " +
            "ON CONFLICT (stream_id, version, name) DO UPDATE SET data = EXCLUDED.data, data_type = EXCLUDED.data_type, size_bytes = EXCLUDED.size_bytes, created_at = now()",
            connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.Active.StreamIdentifier });
        cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = version });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = name ?? string.Empty });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = json });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)jsonTypeInfo.Type.FullName ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter<long> { TypedValue = json.Length });

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> GetAsync<T>(JsonTypeInfo<T> jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default) where T : class, IBase
    {
        var raw = await LoadRawAsync(document, version, name, cancellationToken).ConfigureAwait(false);
        return raw is null ? null : JsonSerializer.Deserialize(raw, jsonTypeInfo);
    }

    public async Task<object?> GetAsync(JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        var raw = await LoadRawAsync(document, version, name, cancellationToken).ConfigureAwait(false);
        return raw is null ? null : JsonSerializer.Deserialize(raw, jsonTypeInfo);
    }

    public async Task<IReadOnlyList<SnapshotMetadata>> ListSnapshotsAsync(IObjectDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT version, name, created_at, size_bytes FROM faes_snapshots WHERE stream_id = $1 ORDER BY version DESC",
            connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.Active.StreamIdentifier });

        var list = new List<SnapshotMetadata>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var version = reader.GetInt32(0);
            var name    = reader.GetString(1);
            var created = reader.GetFieldValue<DateTimeOffset>(2);
            long? size  = reader.IsDBNull(3) ? null : reader.GetInt64(3);
            list.Add(new SnapshotMetadata(version, created, string.IsNullOrEmpty(name) ? null : name, size));
        }
        return list;
    }

    public async Task<bool> DeleteAsync(IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM faes_snapshots WHERE stream_id = $1 AND version = $2 AND name = $3",
            connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.Active.StreamIdentifier });
        cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = version });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = name ?? string.Empty });
        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }

    public async Task<int> DeleteManyAsync(IObjectDocument document, IEnumerable<int> versions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var versionList = versions.ToArray();
        if (versionList.Length == 0)
        {
            return 0;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM faes_snapshots WHERE stream_id = $1 AND version = ANY($2)", connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.Active.StreamIdentifier });
        cmd.Parameters.Add(new NpgsqlParameter<int[]> { TypedValue = versionList });
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> LoadRawAsync(IObjectDocument document, int version, string? name, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT data::text FROM faes_snapshots WHERE stream_id = $1 AND version = $2 AND name = $3",
            connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.Active.StreamIdentifier });
        cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = version });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = name ?? string.Empty });
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }
}
