using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

[JsonSerializable(typeof(TestEntity))]
internal partial class TestJsonContext : JsonSerializerContext
{
    // public static TestJsonContext Default { get; } = new(new JsonSerializerOptions
    // {
    //     TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    // });
    //
    // public TestJsonContext(JsonSerializerOptions options) : base(options) { }
    //
    // public override JsonTypeInfo? GetTypeInfo(Type type) => Options.GetTypeInfo(type);
    //
    // protected override JsonSerializerOptions? GeneratedSerializerOptions => Options;
    //
    // public JsonTypeInfo<TestEntity> TestEntity => (JsonTypeInfo<TestEntity>)Options.GetTypeInfo(typeof(TestEntity))!;
}
