using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.Actions;

public class StreamActionDataStore(IDataStore datastore) : IDataStore
{
    public Task AppendAsync(IObjectDocument document, params IEvent[] events)
    {
        return datastore.AppendAsync(document, events);
    }

    public Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null, int? chunk = null)
    {
        return datastore.ReadAsync(document, startVersion, untilVersion, chunk);
    }
}