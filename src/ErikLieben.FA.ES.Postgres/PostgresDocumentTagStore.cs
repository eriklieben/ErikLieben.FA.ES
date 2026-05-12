using ErikLieben.FA.ES.Documents;
using Npgsql;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Postgres-backed document tag store. Tags live in the <c>document_tags text[]</c> column on
/// <c>faes_documents</c>; lookup uses the GIN index.
/// </summary>
internal sealed class PostgresDocumentTagStore : IDocumentTagStore
{
    private readonly NpgsqlDataSource dataSource;

    public PostgresDocumentTagStore(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    public async Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "UPDATE faes_documents " +
            "SET document_tags = array_append(document_tags, $3), updated_at = now() " +
            "WHERE object_name = $1 AND object_id = $2 AND NOT (document_tags @> ARRAY[$3])",
            connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectId });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = tag });
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "SELECT object_id FROM faes_documents " +
            "WHERE object_name = $1 AND document_tags @> ARRAY[$2]",
            connection);
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

        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "UPDATE faes_documents " +
            "SET document_tags = array_remove(document_tags, $3), updated_at = now() " +
            "WHERE object_name = $1 AND object_id = $2",
            connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectId });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = tag });
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
