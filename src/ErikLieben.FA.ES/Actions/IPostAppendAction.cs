using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Actions;

public interface IPostAppendAction : IAction
{
    Func<T> PostAppend<T>(T data, JsonEvent @event, IObjectDocument document) where T : class;
}
