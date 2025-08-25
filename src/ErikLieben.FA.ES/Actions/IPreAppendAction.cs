using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Actions;

public interface IPreAppendAction : IAction
{
    Func<T> PreAppend<T>(T data, JsonEvent @event, IObjectDocument objectDocument) where T : class;
}

