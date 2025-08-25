using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES;

public interface IEventStreamFactory
{
    IEventStream Create(IObjectDocument document);

}


public interface IEventStreamFactory<T> : IEventStreamFactory where T : Aggregate
{
    IEventStream Create(IEventStream eventStream);
}
