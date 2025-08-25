using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Testing.InMemory;

public class InMemoryDocumentTagDocumentFactory : IDocumentTagDocumentFactory
{
    public IDocumentTagStore CreateDocumentTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.DocumentTagType);
        return new InMemoryDocumentTagStore();
    }

    public IDocumentTagStore CreateDocumentTagStore()
    {
        return new InMemoryDocumentTagStore();
    }

    public IDocumentTagStore CreateDocumentTagStore(string type)
    {
        return new InMemoryDocumentTagStore();
    }
}