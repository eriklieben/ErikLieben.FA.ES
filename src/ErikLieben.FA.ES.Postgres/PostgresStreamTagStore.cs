using ErikLieben.FA.ES.Documents;
using Npgsql;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Postgres-backed stream tag store. Tags live in the <c>stream_tags jsonb</c> column on
/// <c>faes_documents</c>, keyed by stream identifier so each stream carries its own tags and
/// a newly-created stream starts untagged (no inheritance from the previous stream).
/// </summary>
internal sealed class PostgresStreamTagStore : IDocumentTagStore
{
    private readonly NpgsqlDataSource dataSource;

    public PostgresStreamTagStore(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    public async Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);

        // Append the tag to the jsonb array at the stream-id key, deduping by skipping when present.
        // Equivalent of: stream_tags[streamId] = coalesce(stream_tags[streamId], []) || [tag]
        const string sql = """
            UPDATE faes_documents
               SET stream_tags = jsonb_set(
                       stream_tags,
                       ARRAY[$3],
                       COALESCE(stream_tags -> $3, '[]'::jsonb) || to_jsonb($4::text),
                       true),
                   updated_at = now()
             WHERE object_name = $1
               AND object_id   = $2
               AND NOT (COALESCE(stream_tags -> $3, '[]'::jsonb) @> to_jsonb(ARRAY[$4::text]))
            """;

        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectId });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.Active.StreamIdentifier });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = tag });
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        // Match aggregates where any stream's tag-array contains the requested tag.
        // jsonpath form lets the GIN(jsonb_path_ops) index serve this query.
        const string sql = """
            SELECT object_id
              FROM faes_documents
             WHERE object_name = $1
               AND stream_tags @? format('$.* ? (@[*] == %s)', to_jsonb($2::text))::jsonpath
            """;

        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = tag });

        var result = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    public async Task RemoveAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);

        const string sql = """
            UPDATE faes_documents
               SET stream_tags = jsonb_set(
                       stream_tags,
                       ARRAY[$3],
                       COALESCE(stream_tags -> $3, '[]'::jsonb) - $4,
                       true),
                   updated_at = now()
             WHERE object_name = $1
               AND object_id   = $2
            """;

        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectId });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.Active.StreamIdentifier });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = tag });
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
