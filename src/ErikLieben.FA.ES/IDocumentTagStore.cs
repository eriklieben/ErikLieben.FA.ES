using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES;

public interface IDocumentTagStore
{
    public Task SetAsync(IObjectDocument document, string tag);
    
    public Task<IEnumerable<string>> GetAsync(string objectName, string tag);
}