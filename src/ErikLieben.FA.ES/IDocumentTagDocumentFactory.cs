using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES;

public interface IDocumentTagDocumentFactory
{
    IDocumentTagStore CreateDocumentTagStore(IObjectDocument document);
    
    IDocumentTagStore CreateDocumentTagStore(string type);
}
