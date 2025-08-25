using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.Testing.InMemory;

public class InMemoryDataStore : IDataStore
{
    public Dictionary<string, Dictionary<int, IEvent>> Store = new();

    public Task AppendAsync(IObjectDocument document, params IEvent[] events)
    {
        var identifier = GetStoreKey(document.ObjectName, document.ObjectId);
        foreach (var @event in events)
        {
            if (!Store.ContainsKey(identifier))
            {
                Store[identifier] = new();
            }

            var count = Store[identifier].Count;
            Store[identifier][count] = @event;
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null, int? chunk = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);

        var identifier = GetStoreKey(document.ObjectName, document.ObjectId);

        if (!Store.ContainsKey(identifier))
        {
            return Task.FromResult<IEnumerable<IEvent>?>(new List<IEvent>());
        }

        var storedEvents = Store[identifier].Values.ToList();
        return Task.FromResult<IEnumerable<IEvent>?>(storedEvents);
    }

    public Dictionary<int, IEvent> GetDataStoreFor(string storeId)
    {
        return Store[storeId];
    }

    public Task RemoveAsync(IObjectDocument document, params IEvent[] events)
    {
        var identifier = GetStoreKey(document.ObjectName, document.ObjectId);
        foreach (var @event in events)
        {
            if (Store.ContainsKey(identifier) && Store[identifier].ContainsKey(@event.EventVersion))
            {
                Store[identifier].Remove(@event.EventVersion);
            }
        }
        return Task.CompletedTask;
    }

    public static string GetStoreKey(string objectName, string objectId)
    {
        return $"{objectName.ToLowerInvariant()}__{objectId}";
    }
}

