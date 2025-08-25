using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.JsonConverters;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES;

[JsonSerializable(typeof(VersionToken))]
[JsonSerializable(typeof(ObjectIdentifier))]
[JsonSerializable(typeof(VersionIdentifier))]
public partial class VersionTokenJsonContext : JsonSerializerContext
{
    public static JsonSerializerOptions OptionsWithCustomConverters => new()
    {
        Converters =
        {
            new VersionTokenJsonConverter(),
            new ObjectIdentifierJsonConverter(),
            new VersionIdentifierJsonConverter(),
        } 
    };

    public static VersionTokenJsonContext WithConverters => new VersionTokenJsonContext(OptionsWithCustomConverters);

}