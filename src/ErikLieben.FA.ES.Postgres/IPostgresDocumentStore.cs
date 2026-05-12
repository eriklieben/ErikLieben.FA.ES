using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Internal contract for the Postgres-backed document store.
/// Mirrors <c>IBlobDocumentStore</c> for symmetry with other providers.
/// </summary>
internal interface IPostgresDocumentStore
{
    Task<IObjectDocument> CreateAsync(string objectName, string objectId, string? store = null);
    Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null);
    Task<IObjectDocument?> GetFirstByDocumentByTagAsync(string objectName, string tag, string? documentTagStore = null, string? store = null);
    Task<IEnumerable<IObjectDocument>> GetByDocumentByTagAsync(string objectName, string tag, string? documentTagStore = null, string? store = null);
    Task SetAsync(IObjectDocument document);
}
