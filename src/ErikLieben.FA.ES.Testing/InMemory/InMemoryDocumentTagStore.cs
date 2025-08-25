using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Testing.InMemory;

public class InMemoryDocumentTagStore : IDocumentTagStore
{
    private readonly Dictionary<string, List<string>> Tags = new();

    public Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        if (Tags.ContainsKey(document.ObjectId) && !Tags[document.ObjectId].Contains(tag))
        {
            Tags[document.ObjectId].Add(tag);
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        if (!this.Tags.ContainsKey(objectName))
        {
            return Task.FromResult<IEnumerable<string>>(new List<string>());
        }
        return Task.FromResult<IEnumerable<string>>(this.Tags[objectName]);
    }
}
