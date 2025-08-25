using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES;

public interface IObjectDocumentFactory
{
    public Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null!);

    public Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null!);

    Task<IObjectDocument?> GetFirstByObjectDocumentTag(string objectName, string objectDocumentTag);

    Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(string objectName, string objectDocumentTag);


    public Task SetAsync(IObjectDocument document, string? store = null!);
}
