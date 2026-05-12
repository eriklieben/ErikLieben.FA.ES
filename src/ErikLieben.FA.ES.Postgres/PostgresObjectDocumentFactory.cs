using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Postgres-backed <see cref="IObjectDocumentFactory"/>. Thin wrapper over <see cref="IPostgresDocumentStore"/>.
/// </summary>
internal sealed class PostgresObjectDocumentFactory : IObjectDocumentFactory
{
    private readonly IPostgresDocumentStore documentStore;

    public PostgresObjectDocumentFactory(IPostgresDocumentStore documentStore)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        this.documentStore = documentStore;
    }

    public Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null, string? documentType = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        return documentStore.GetAsync(objectName, objectId, store);
    }

    public Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null, string? documentType = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        return documentStore.CreateAsync(objectName, objectId, store);
    }

    public Task<IObjectDocument?> GetFirstByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null, CancellationToken cancellationToken = default)
        => documentStore.GetFirstByDocumentByTagAsync(objectName, objectDocumentTag, documentTagStore, store);

    public Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null, CancellationToken cancellationToken = default)
        => documentStore.GetByDocumentByTagAsync(objectName, objectDocumentTag, documentTagStore, store);

    public Task SetAsync(IObjectDocument document, string? store = null, string? documentType = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return documentStore.SetAsync(document);
    }
}
