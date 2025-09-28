using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory.Model;

namespace ErikLieben.FA.ES.Testing.InMemory;

public class InMemoryDocumentStore
{
    private readonly Dictionary<string, IObjectDocument> documents = new();

    public Task<IObjectDocument> CreateAsync(string name, string objectId)
    {
        var created = new InMemoryEventStreamDocument(
            objectId,
            name.ToLowerInvariant(),
            new StreamInformation
            {
                StreamConnectionName = "inMemory",
                SnapShotConnectionName = "inMemory",
                DocumentTagConnectionName = "inMemory",
                StreamTagConnectionName = "inMemory",
                StreamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000",
                StreamType = "inMemory",
                DocumentTagType = "inMemory",
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

    public Task<IObjectDocument> GetAsync(string name, string objectId)
    {
        return Task.FromResult(documents[$"{name.ToLowerInvariant()}/{objectId}"]);
    }
}
