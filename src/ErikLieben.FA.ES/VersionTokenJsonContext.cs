using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.JsonConverters;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES;

/// <summary>
/// Provides JSON serialization context for <see cref="VersionToken"/> and related types with support for custom converters.
/// </summary>
[JsonSerializable(typeof(VersionToken))]
[JsonSerializable(typeof(ObjectIdentifier))]
[JsonSerializable(typeof(VersionIdentifier))]
public partial class VersionTokenJsonContext : JsonSerializerContext
{
    /// <summary>
    /// Gets the JSON serializer options configured with custom converters for <see cref="VersionToken"/>, <see cref="ObjectIdentifier"/>, and <see cref="VersionIdentifier"/>.
    /// </summary>
    public static JsonSerializerOptions OptionsWithCustomConverters => new()
    {
        Converters =
        {
            new VersionTokenJsonConverter(),
            new ObjectIdentifierJsonConverter(),
            new VersionIdentifierJsonConverter(),
        }
    };

    /// <summary>
    /// Gets a <see cref="VersionTokenJsonContext"/> instance configured with custom converters.
    /// </summary>
    public static VersionTokenJsonContext WithConverters => new VersionTokenJsonContext(OptionsWithCustomConverters);

}