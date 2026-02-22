using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Testing.InMemory;

/// <summary>
/// Provides an in-memory implementation of <see cref="IObjectDocumentFactory"/> for testing scenarios.
/// </summary>
public class InMemoryObjectDocumentFactory : IObjectDocumentFactory
{
    private readonly InMemoryDocumentStore blobDocumentStore;
    private readonly IDocumentTagStore documentTagStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryObjectDocumentFactory"/> class.
    /// </summary>
    /// <param name="documentTagStore">The in-memory document tag store used for tag queries.</param>
    public InMemoryObjectDocumentFactory(IDocumentTagStore documentTagStore)
    {
        blobDocumentStore = new();
        this.documentTagStore = documentTagStore;
    }

    /// <summary>
    /// Retrieves an object document or creates a new one when it does not exist in the in-memory store.
    /// </summary>
    /// <param name="objectName">The object type/name used as logical scope.</param>
    /// <param name="objectId">The identifier of the object to retrieve or create.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <param name="documentType">Unused in this implementation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The existing or newly created <see cref="IObjectDocument"/>.</returns>
    public Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null, string? documentType = null, CancellationToken cancellationToken = default)
    {
        return blobDocumentStore.CreateAsync(objectName.ToLowerInvariant(), objectId);
    }

    /// <summary>
    /// Gets the first object document that has the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Unused in this implementation.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The first matching document or null when none is found.</returns>
    public async Task<IObjectDocument?> GetFirstByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null, CancellationToken cancellationToken = default)
    {
        var documentId = (await this.documentTagStore.GetAsync(objectName, objectDocumentTag)).ToList().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return null!;
        }

        return await GetAsync(objectName, documentId, store, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets all object documents that have the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Unused in this implementation.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    public async Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null, CancellationToken cancellationToken = default)
    {
        var documentIds = (await this.documentTagStore.GetAsync(objectName, objectDocumentTag)).ToList();
        var documents = new List<IObjectDocument>();
        foreach (var documentId in documentIds)
        {
            documents.Add(await GetAsync(objectName, documentId, store, cancellationToken: cancellationToken));
        }
        return documents;
    }

    /// <summary>
    /// Retrieves an existing object document from the in-memory store.
    /// </summary>
    /// <param name="objectName">The object type/name used as logical scope.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <param name="documentType">Unused in this implementation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loaded <see cref="IObjectDocument"/>.</returns>
    public Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null, string? documentType = null, CancellationToken cancellationToken = default)
    {
        return blobDocumentStore.GetAsync(objectName.ToLowerInvariant(), objectId);
    }

    /// <summary>
    /// Persists the provided object document to the in-memory store.
    /// </summary>
    /// <param name="document">The object document to persist.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <param name="documentType">Unused in this implementation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task SetAsync(IObjectDocument document, string? store = null!, string? documentType = null, CancellationToken cancellationToken = default)
    {
        return blobDocumentStore.SetAsync(document);
    }
}
