using System.Text.Json;
using ErikLieben.FA.ES.Postgres.Model;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Postgres-backed <see cref="IProjectionStatusCoordinator"/>. Status, rebuild info, and the
/// active rebuild token live in <c>faes_projection_status</c> with one row per
/// (projection_name, object_id). Optimistic concurrency uses each row's <c>xmin</c>.
/// </summary>
public sealed class PostgresProjectionStatusCoordinator : IProjectionStatusCoordinator
{
    private readonly NpgsqlDataSource dataSource;
    private readonly ILogger<PostgresProjectionStatusCoordinator>? logger;

    /// <summary>Initializes a new instance.</summary>
    public PostgresProjectionStatusCoordinator(
        NpgsqlDataSource dataSource,
        ILogger<PostgresProjectionStatusCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<RebuildToken> StartRebuildAsync(
        string projectionName, string objectId, RebuildStrategy strategy, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        var token = RebuildToken.Create(projectionName, objectId, strategy, timeout);
        var rebuildInfo = RebuildInfo.Start(strategy);

        const string sql = """
            INSERT INTO faes_projection_status
                (projection_name, object_id, status, status_changed_at, schema_version, rebuild_info, active_rebuild_token)
            VALUES ($1, $2, $3, now(), 0, $4::jsonb, $5::jsonb)
            ON CONFLICT (projection_name, object_id) DO UPDATE
            SET status               = EXCLUDED.status,
                status_changed_at    = EXCLUDED.status_changed_at,
                rebuild_info         = EXCLUDED.rebuild_info,
                active_rebuild_token = EXCLUDED.active_rebuild_token
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = projectionName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectId });
        cmd.Parameters.Add(new NpgsqlParameter<short> { TypedValue = (short)ProjectionStatus.Rebuilding });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = SerializeRebuildInfo(rebuildInfo) });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = SerializeRebuildToken(token) });
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        logger?.LogInformation(
            "Started rebuild for {ProjectionName}:{ObjectId} with strategy {Strategy}, expires at {ExpiresAt}",
            projectionName, objectId, strategy, token.ExpiresAt);

        return token;
    }

    /// <inheritdoc />
    public Task StartCatchUpAsync(RebuildToken token, CancellationToken cancellationToken = default)
        => TransitionAsync(token, ProjectionStatus.CatchingUp, withProgress: true, keepActiveToken: true, cancellationToken);

    /// <inheritdoc />
    public Task MarkReadyAsync(RebuildToken token, CancellationToken cancellationToken = default)
        => TransitionAsync(token, ProjectionStatus.Ready, withProgress: false, keepActiveToken: true, cancellationToken, completion: true);

    /// <inheritdoc />
    public Task CompleteRebuildAsync(RebuildToken token, CancellationToken cancellationToken = default)
        => TransitionAsync(token, ProjectionStatus.Active, withProgress: false, keepActiveToken: false, cancellationToken, completion: true);

    /// <inheritdoc />
    public async Task CancelRebuildAsync(RebuildToken token, string? error = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await ReadAsync(connection, token.ProjectionName, token.ObjectId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        var newStatus = error != null ? ProjectionStatus.Failed : ProjectionStatus.Active;
        var rebuildInfo = error != null
            ? row.RebuildInfo?.WithError(error)
            : row.RebuildInfo?.WithCompletion();

        const string sql = """
            UPDATE faes_projection_status
               SET status               = $3,
                   status_changed_at    = now(),
                   rebuild_info         = $4::jsonb,
                   active_rebuild_token = NULL
             WHERE projection_name = $1 AND object_id = $2 AND xmin::text = $5
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = token.ProjectionName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = token.ObjectId });
        cmd.Parameters.Add(new NpgsqlParameter<short> { TypedValue = (short)newStatus });
        cmd.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Text,
            Value = rebuildInfo is null ? DBNull.Value : SerializeRebuildInfo(rebuildInfo)
        });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = row.Xmin });
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        logger?.LogWarning(
            "Cancelled rebuild for {ProjectionName}:{ObjectId}. Error: {Error}",
            token.ProjectionName, token.ObjectId, error ?? "none");
    }

    /// <inheritdoc />
    public async Task<ProjectionStatusInfo?> GetStatusAsync(string projectionName, string objectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await ReadAsync(connection, projectionName, objectId, cancellationToken).ConfigureAwait(false);
        return row?.ToStatusInfo();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProjectionStatusInfo>> GetByStatusAsync(ProjectionStatus status, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT projection_name, object_id, status, status_changed_at, schema_version, " +
            "       rebuild_info::text, active_rebuild_token::text, xmin::text " +
            "FROM faes_projection_status WHERE status = $1", connection);
        cmd.Parameters.Add(new NpgsqlParameter<short> { TypedValue = (short)status });

        var results = new List<ProjectionStatusInfo>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadRow(reader).ToStatusInfo());
        }
        return results;
    }

    /// <inheritdoc />
    public async Task<int> RecoverStuckRebuildsAsync(CancellationToken cancellationToken = default)
    {
        // Find rows where the active_rebuild_token has expired AND status is in a rebuilding state.
        var rebuildingStatuses = new short[]
        {
            (short)ProjectionStatus.Rebuilding,
            (short)ProjectionStatus.CatchingUp,
            (short)ProjectionStatus.Ready,
        };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var selectCmd = new NpgsqlCommand(
            "SELECT projection_name, object_id, status, status_changed_at, schema_version, " +
            "       rebuild_info::text, active_rebuild_token::text, xmin::text " +
            "FROM faes_projection_status " +
            "WHERE status = ANY($1) AND active_rebuild_token IS NOT NULL " +
            "  AND (active_rebuild_token->>'expiresAt')::timestamptz < now()",
            connection);
        selectCmd.Parameters.Add(new NpgsqlParameter<short[]> { TypedValue = rebuildingStatuses });

        var stuck = new List<ProjectionStatusRow>();
        await using (var reader = await selectCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                stuck.Add(ReadRow(reader));
            }
        }

        var recovered = 0;
        foreach (var row in stuck)
        {
            var rebuildInfo = row.RebuildInfo?.WithError("Rebuild timed out");
            const string updateSql = """
                UPDATE faes_projection_status
                   SET status               = $3,
                       status_changed_at    = now(),
                       rebuild_info         = $4::jsonb,
                       active_rebuild_token = NULL
                 WHERE projection_name = $1 AND object_id = $2 AND xmin::text = $5
                """;
            await using var updateCmd = new NpgsqlCommand(updateSql, connection);
            updateCmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = row.ProjectionName });
            updateCmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = row.ObjectId });
            updateCmd.Parameters.Add(new NpgsqlParameter<short> { TypedValue = (short)ProjectionStatus.Failed });
            updateCmd.Parameters.Add(new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.Text,
                Value = rebuildInfo is null ? DBNull.Value : SerializeRebuildInfo(rebuildInfo)
            });
            updateCmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = row.Xmin });
            var affected = await updateCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (affected > 0)
            {
                recovered++;
                logger?.LogWarning(
                    "Recovered stuck rebuild for {ProjectionName}:{ObjectId}", row.ProjectionName, row.ObjectId);
            }
        }
        return recovered;
    }

    /// <inheritdoc />
    public async Task DisableAsync(string projectionName, string objectId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        const string sql = """
            INSERT INTO faes_projection_status
                (projection_name, object_id, status, status_changed_at)
            VALUES ($1, $2, $3, now())
            ON CONFLICT (projection_name, object_id) DO UPDATE
            SET status            = EXCLUDED.status,
                status_changed_at = EXCLUDED.status_changed_at
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = projectionName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectId });
        cmd.Parameters.Add(new NpgsqlParameter<short> { TypedValue = (short)ProjectionStatus.Disabled });
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        logger?.LogInformation("Disabled projection {ProjectionName}:{ObjectId}", projectionName, objectId);
    }

    /// <inheritdoc />
    public async Task EnableAsync(string projectionName, string objectId, CancellationToken cancellationToken = default)
    {
        // Only enable if the row exists; matches the Blob coordinator's behaviour.
        const string sql = """
            UPDATE faes_projection_status
               SET status = $3, status_changed_at = now()
             WHERE projection_name = $1 AND object_id = $2
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = projectionName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectId });
        cmd.Parameters.Add(new NpgsqlParameter<short> { TypedValue = (short)ProjectionStatus.Active });
        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (rows > 0)
        {
            logger?.LogInformation("Enabled projection {ProjectionName}:{ObjectId}", projectionName, objectId);
        }
    }

    private async Task TransitionAsync(
        RebuildToken token,
        ProjectionStatus newStatus,
        bool withProgress,
        bool keepActiveToken,
        CancellationToken cancellationToken,
        bool completion = false)
    {
        ArgumentNullException.ThrowIfNull(token);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await ReadAsync(connection, token.ProjectionName, token.ObjectId, cancellationToken).ConfigureAwait(false);
        ValidateToken(token, row);
        if (row is null)
        {
            return;
        }

        RebuildInfo? rebuildInfo = row.RebuildInfo;
        if (completion)
        {
            rebuildInfo = rebuildInfo?.WithCompletion();
        }
        else if (withProgress)
        {
            rebuildInfo = rebuildInfo?.WithProgress();
        }

        const string sql = """
            UPDATE faes_projection_status
               SET status               = $3,
                   status_changed_at    = now(),
                   rebuild_info         = $4::jsonb,
                   active_rebuild_token = CASE WHEN $5 THEN active_rebuild_token ELSE NULL END
             WHERE projection_name = $1 AND object_id = $2 AND xmin::text = $6
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = token.ProjectionName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = token.ObjectId });
        cmd.Parameters.Add(new NpgsqlParameter<short> { TypedValue = (short)newStatus });
        cmd.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Text,
            Value = rebuildInfo is null ? DBNull.Value : SerializeRebuildInfo(rebuildInfo)
        });
        cmd.Parameters.Add(new NpgsqlParameter<bool> { TypedValue = keepActiveToken });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = row.Xmin });
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ProjectionStatusRow?> ReadAsync(
        NpgsqlConnection connection, string projectionName, string objectId, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT projection_name, object_id, status, status_changed_at, schema_version, " +
            "       rebuild_info::text, active_rebuild_token::text, xmin::text " +
            "FROM faes_projection_status WHERE projection_name = $1 AND object_id = $2", connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = projectionName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectId });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRow(reader) : null;
    }

    private static ProjectionStatusRow ReadRow(NpgsqlDataReader reader) => new(
        ProjectionName: reader.GetString(0),
        ObjectId:       reader.GetString(1),
        Status:         (ProjectionStatus)reader.GetInt16(2),
        StatusChangedAt: reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
        SchemaVersion:  reader.GetInt32(4),
        RebuildInfo:    reader.IsDBNull(5) ? null
            : JsonSerializer.Deserialize(reader.GetString(5), ProjectionStatusJsonContext.Default.RebuildInfo),
        ActiveRebuildToken: reader.IsDBNull(6) ? null
            : JsonSerializer.Deserialize(reader.GetString(6), ProjectionStatusJsonContext.Default.RebuildToken),
        Xmin:           reader.GetString(7));

    private static void ValidateToken(RebuildToken token, ProjectionStatusRow? row)
    {
        if (row?.ActiveRebuildToken is null || row.ActiveRebuildToken.Token != token.Token)
        {
            throw new InvalidOperationException(
                $"Invalid or expired rebuild token for {token.ProjectionName}:{token.ObjectId}");
        }
        if (token.IsExpired)
        {
            throw new InvalidOperationException(
                $"Rebuild token for {token.ProjectionName}:{token.ObjectId} has expired");
        }
    }

    private static string SerializeRebuildInfo(RebuildInfo info)
        => JsonSerializer.Serialize(info, ProjectionStatusJsonContext.Default.RebuildInfo);

    private static string SerializeRebuildToken(RebuildToken token)
        => JsonSerializer.Serialize(token, ProjectionStatusJsonContext.Default.RebuildToken);

    private sealed record ProjectionStatusRow(
        string ProjectionName,
        string ObjectId,
        ProjectionStatus Status,
        DateTimeOffset? StatusChangedAt,
        int SchemaVersion,
        RebuildInfo? RebuildInfo,
        RebuildToken? ActiveRebuildToken,
        string Xmin)
    {
        public ProjectionStatusInfo ToStatusInfo() => new(
            ProjectionName, ObjectId, Status, StatusChangedAt, SchemaVersion, RebuildInfo);
    }
}
