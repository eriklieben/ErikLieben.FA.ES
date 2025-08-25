using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Actions;

public interface IPostReadAction : IAction
{
    Func<T> PostRead<T>(T data, IEvent @event, IObjectDocument objectDocument) where T : class;
}
