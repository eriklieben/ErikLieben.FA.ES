using System.Globalization;
using Npgsql;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Postgres-backed <see cref="IObjectIdProvider"/>. Pagination uses an opaque continuation token
/// holding the last object_id seen; queries use keyset pagination for stable ordering.
/// </summary>
internal sealed class PostgresObjectIdProvider : IObjectIdProvider
{
    private readonly NpgsqlDataSource dataSource;

    public PostgresObjectIdProvider(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    public async Task<PagedResult<string>> GetObjectIdsAsync(
        string objectName,
        string? continuationToken,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = continuationToken is null
            ? new NpgsqlCommand(
                "SELECT object_id FROM faes_documents WHERE object_name = $1 ORDER BY object_id LIMIT $2",
                connection)
            : new NpgsqlCommand(
                "SELECT object_id FROM faes_documents WHERE object_name = $1 AND object_id > $2 ORDER BY object_id LIMIT $3",
                connection);

        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectName });
        if (continuationToken is not null)
        {
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = continuationToken });
        }
        cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = pageSize });

        var items = new List<string>(pageSize);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(reader.GetString(0));
        }

        string? next = items.Count == pageSize ? items[^1] : null;
        return new PagedResult<string>
        {
            Items = items,
            PageSize = pageSize,
            ContinuationToken = next,
        };
    }

    public async Task<bool> ExistsAsync(string objectName, string objectId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM faes_documents WHERE object_name = $1 AND object_id = $2 LIMIT 1", connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectId });
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    public async Task<long> CountAsync(string objectName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM faes_documents WHERE object_name = $1", connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectName });
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is long l ? l : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }
}
