using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES;

public interface ISnapShotStore
{
    Task<T?> GetAsync<T>(JsonTypeInfo<T> jsonTypeInfo, IObjectDocument document, int version, string? name = null) where T : class, IBase;

    Task<object?> GetAsync(JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null);

    Task SetAsync(IBase @object, JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null);
}