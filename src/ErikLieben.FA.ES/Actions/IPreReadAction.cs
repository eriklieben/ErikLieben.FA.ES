using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Actions;

public interface IPreReadAction : IAction
{
    Func<T> PreRead<T>(T data, JsonEvent @event, IObjectDocument objectDocument) where T : class;
}