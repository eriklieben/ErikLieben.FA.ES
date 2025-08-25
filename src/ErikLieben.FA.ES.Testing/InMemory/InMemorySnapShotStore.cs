using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Testing.InMemory;

public class InMemorySnapShotStore : ISnapShotStore
{
    private readonly Dictionary<string, IBase> snapshots = new();
    
    public Task<T?> GetAsync<T>(JsonTypeInfo<T> jsonTypeInfo, IObjectDocument document, int version, string? name = null) where T : class, IBase
    {
       var documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
       return Task.FromResult<T?>(snapshots[documentPath] as T);

    }

    public Task<object?> GetAsync(JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null)
    {
       var documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
       return Task.FromResult<object?>(snapshots[documentPath]);
    }

    public Task SetAsync(IBase @object, JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null)
    {
        var documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
        snapshots[documentPath] = @object;
        return Task.CompletedTask;
    }
}
