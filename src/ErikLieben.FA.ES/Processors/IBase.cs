namespace ErikLieben.FA.ES.Processors;

public interface IBase
{
    Task Fold();
    void Fold(IEvent @event);

    void ProcessSnapshot(object snapshot);
}
