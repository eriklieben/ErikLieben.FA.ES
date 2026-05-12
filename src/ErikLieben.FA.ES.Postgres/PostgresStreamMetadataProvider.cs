using ErikLieben.FA.ES.Retention;
using Npgsql;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Postgres-backed <see cref="IStreamMetadataProvider"/>. Computes event count and date range
/// directly from <c>faes_events</c> via a single aggregate query. Partition pruning on
/// <c>object_name</c> keeps the scan to the relevant partition.
/// </summary>
public sealed class PostgresStreamMetadataProvider : IStreamMetadataProvider
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>Initializes a new instance.</summary>
    public PostgresStreamMetadataProvider(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<StreamMetadata?> GetStreamMetadataAsync(string objectName, string objectId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*)::int, min(created_at), max(created_at) " +
            "FROM faes_events WHERE object_name = $1 AND object_id = $2", connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectId });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var count = reader.GetInt32(0);
        if (count == 0)
        {
            return null;
        }

        DateTimeOffset? oldest = reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1);
        DateTimeOffset? newest = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2);
        return new StreamMetadata(objectName, objectId, count, oldest, newest);
    }
}
