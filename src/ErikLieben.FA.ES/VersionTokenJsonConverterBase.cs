using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.JsonConverters;

namespace ErikLieben.FA.ES;
public abstract class VersionTokenJsonConverterBase<T> : JsonConverter<T> where T : VersionToken, new()
{
    protected readonly VersionTokenJsonConverter Converter = new();

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        Converter.Write(writer, value, options);
    }
}
