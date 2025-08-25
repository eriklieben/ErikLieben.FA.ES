using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Aggregates;

public interface IAggregateCovarianceFactory<out T> where T : IBase
{
    string GetObjectName();

    T Create(IEventStream eventStream);

    T Create(IObjectDocument document);
}

public interface IAggregateFactory<T> : IAggregateCovarianceFactory<T> where T : IBase
{
    Task<T> CreateAsync(string id);

    Task<T> GetAsync(string id);

    Task<T> GetFirstByDocumentTag(string tag);

    Task<IEnumerable<T>> GetAllByDocumentTag(string tag);

    Task<(T, IObjectDocument)> GetWithDocumentAsync(string id);
}

public interface IAggregateFactory<T,TId> : IAggregateCovarianceFactory<T> where T : IBase
{
    Task<T> CreateAsync(TId id);

    Task<T> GetAsync(TId id);
    
    Task<T> GetFirstByDocumentTag(string tag);

    Task<IEnumerable<T>> GetAllByDocumentTag(string tag);

    Task<(T, IObjectDocument)> GetWithDocumentAsync(TId id);
}
