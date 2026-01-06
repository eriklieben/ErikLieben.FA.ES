using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory.Model;

namespace ErikLieben.FA.ES.Testing.InMemory;

/// <summary>
/// In-memory implementation of IDocumentStore for testing purposes.
/// </summary>
public class InMemoryDocumentStore : IDocumentStore
{
    private const string InMemoryConnectionName = "inMemory";

    private readonly Dictionary<string, IObjectDocument> documents = new();

    /// <inheritdoc />
    public Task<IObjectDocument> CreateAsync(string name, string objectId, string? store = null)
    {
        var created = new InMemoryEventStreamDocument(
            objectId,
            name.ToLowerInvariant(),
            new StreamInformation
            {
                DataStore = InMemoryConnectionName,
                SnapShotStore = InMemoryConnectionName,
                DocumentTagStore = InMemoryConnectionName,
                StreamTagStore = InMemoryConnectionName,
                StreamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000",
                StreamType = InMemoryConnectionName,
                DocumentTagType = InMemoryConnectionName,
                CurrentStreamVersion = -1
            },
            [],
            "1.0.0");

        if (documents.ContainsKey($"{name}/{objectId}"))
        {
           throw new InvalidOperationException($"Document '{name}/{objectId}' already exists in the in-memory store.");
        }
        else
        {
            documents.Add($"{name}/{objectId}", created);
            return Task.FromResult(documents[$"{name}/{objectId}"]);
        }
    }

    public Task SetAsync(IObjectDocument document)
    {
        if (documents.ContainsKey($"{document.ObjectName.ToLowerInvariant()}/{document.ObjectId}"))
        {
            documents[$"{document.ObjectName.ToLowerInvariant()}/{document.ObjectId}"] = document;
        }
        else
        {
            documents.Add($"{document.ObjectName.ToLowerInvariant()}/{document.ObjectId}", document);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IObjectDocument> GetAsync(string name, string objectId, string? store = null)
    {
        return Task.FromResult(documents[$"{name.ToLowerInvariant()}/{objectId}"]);
    }
}
