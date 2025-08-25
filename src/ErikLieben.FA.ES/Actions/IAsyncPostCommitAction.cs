using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Actions;

public interface IAsyncPostCommitAction : IAction
{
    Task PostCommitAsync(IEnumerable<JsonEvent> events, IObjectDocument document);
}
