using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.EventStream;

public interface IDataStore
{
    Task AppendAsync(IObjectDocument document, params IEvent[] events);

    Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null,
        int? chunk = null);
}