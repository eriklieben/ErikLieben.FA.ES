using ErikLieben.FA.ES.Documents;
using Npgsql;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Creates Postgres-backed document and stream tag stores.
/// </summary>
internal sealed class PostgresTagFactory : IDocumentTagDocumentFactory
{
    private readonly NpgsqlDataSource dataSource;

    public PostgresTagFactory(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    public IDocumentTagStore CreateDocumentTagStore() => new PostgresDocumentTagStore(dataSource);

    public IDocumentTagStore CreateDocumentTagStore(IObjectDocument document) => new PostgresDocumentTagStore(dataSource);

    public IDocumentTagStore CreateDocumentTagStore(string type) => new PostgresDocumentTagStore(dataSource);

    public IDocumentTagStore CreateStreamTagStore() => new PostgresStreamTagStore(dataSource);

    public IDocumentTagStore CreateStreamTagStore(IObjectDocument document) => new PostgresStreamTagStore(dataSource);
}
