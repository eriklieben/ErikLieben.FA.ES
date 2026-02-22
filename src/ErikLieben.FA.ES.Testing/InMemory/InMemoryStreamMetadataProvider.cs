using ErikLieben.FA.ES.Retention;

namespace ErikLieben.FA.ES.Testing.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IStreamMetadataProvider"/> for testing.
/// Uses the <see cref="InMemoryDataStore"/> to derive stream metadata.
/// </summary>
public class InMemoryStreamMetadataProvider : IStreamMetadataProvider
{
    private readonly InMemoryDataStore _dataStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryStreamMetadataProvider"/> class.
    /// </summary>
    /// <param name="dataStore">The in-memory data store to query.</param>
    public InMemoryStreamMetadataProvider(InMemoryDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    /// <inheritdoc />
    public Task<StreamMetadata?> GetStreamMetadataAsync(
        string objectName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var key = InMemoryDataStore.GetStoreKey(objectName, objectId);

        if (!_dataStore.Store.TryGetValue(key, out var events) || events.Count == 0)
        {
            return Task.FromResult<StreamMetadata?>(null);
        }

        // IEvent does not expose a timestamp property, so dates are unavailable
        // from in-memory storage. Tests can supply metadata directly if needed.
        var metadata = new StreamMetadata(objectName, objectId, events.Count, null, null);
        return Task.FromResult<StreamMetadata?>(metadata);
    }
}
