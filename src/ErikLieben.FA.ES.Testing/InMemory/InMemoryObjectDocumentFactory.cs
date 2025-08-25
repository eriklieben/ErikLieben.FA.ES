using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Testing.InMemory;

public class InMemoryObjectDocumentFactory : IObjectDocumentFactory
{
    private readonly InMemoryDocumentStore blobDocumentStore;
    private readonly IDocumentTagStore documentTagStore;

    public InMemoryObjectDocumentFactory(IDocumentTagStore documentTagStore)
    {
        blobDocumentStore = new();
        this.documentTagStore = documentTagStore;
    }

    public Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null)
    {
        return blobDocumentStore.CreateAsync(objectName.ToLowerInvariant(), objectId);
    }

    public async Task<IObjectDocument?> GetFirstByObjectDocumentTag(string objectName, string objectDocumentTag)
    {
        var documentId = (await this.documentTagStore.GetAsync(objectName, objectDocumentTag)).ToList().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return null!;
        }

        return await GetAsync(objectName, documentId);
    }

    public async Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(string objectName, string objectDocumentTag)
    {
        var documentIds = (await this.documentTagStore.GetAsync(objectName, objectDocumentTag)).ToList();
        var documents = new List<IObjectDocument>();
        foreach (var documentId in documentIds)
        {
            documents.Add(await GetAsync(objectName, documentId));
        }
        return documents;
    }

    public Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null)
    {
        return blobDocumentStore.GetAsync(objectName.ToLowerInvariant(), objectId);
    }

    public Task SetAsync(IObjectDocument document, string? store = null!)
    {
        return blobDocumentStore.SetAsync(document);
    }
}
