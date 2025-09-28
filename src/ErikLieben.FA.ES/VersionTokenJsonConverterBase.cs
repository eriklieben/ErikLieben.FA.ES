using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.JsonConverters;

namespace ErikLieben.FA.ES;

/// <summary>
/// Provides a base JSON converter for <see cref="VersionToken"/>-derived types, delegating to <see cref="VersionTokenJsonConverter"/>.
/// </summary>
/// <typeparam name="T">The concrete <see cref="VersionToken"/> type handled by this converter.</typeparam>
public abstract class VersionTokenJsonConverterBase<T> : JsonConverter<T> where T : VersionToken, new()
{
    /// <summary>
    /// Gets the shared converter that performs the actual serialization.
    /// </summary>
    protected readonly VersionTokenJsonConverter Converter = new();

    /// <summary>
    /// Writes the specified <see cref="VersionToken"/> value to JSON using the shared converter.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">The serializer options.</param>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        Converter.Write(writer, value, options);
    }
}
