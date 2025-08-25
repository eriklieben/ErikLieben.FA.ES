using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

public interface IBlobDocumentStore
{
    Task<IObjectDocument> CreateAsync(
        string name,
        string objectId);

    Task<IObjectDocument> GetAsync(
        string name, 
        string objectId);

    Task<IObjectDocument?> GetFirstByDocumentByTagAsync(string objectName, string tag);
    Task<IEnumerable<IObjectDocument>> GetByDocumentByTagAsync(string objectName, string tag);
    Task SetAsync(IObjectDocument document);
}