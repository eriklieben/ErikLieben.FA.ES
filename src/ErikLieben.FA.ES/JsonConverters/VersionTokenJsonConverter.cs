using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.JsonConverters;

/// <summary>
/// Provides JSON conversion for <see cref="VersionToken"/> values, supporting both regular values and dictionary keys with format "vt[value]schemaVersion".
/// </summary>
public class VersionTokenJsonConverter : JsonConverter<VersionToken>
{
    private const string Prefix = "vt[";
    private const char SuffixStart = ']';

    /// <summary>
    /// Reads and converts JSON to a <see cref="VersionToken"/>.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The type being converted.</param>
    /// <param name="options">Serializer options.</param>
    /// <returns>A <see cref="VersionToken"/> parsed from the JSON.</returns>
    /// <exception cref="JsonException">Thrown when the JSON format is invalid.</exception>
    public override VersionToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var versionTokenString = reader.GetString();
        
        // TODO: temp hack
        if (versionTokenString != null && versionTokenString.StartsWith("versionToken["))
        {
            versionTokenString = "vt[" + versionTokenString[13..];
        }
        
        if (string.IsNullOrEmpty(versionTokenString) || !versionTokenString.StartsWith(Prefix))
        {
            throw new JsonException($"Invalid versionToken format: {versionTokenString}");
        }

        var suffixStartIndex = versionTokenString.IndexOf(SuffixStart);
        if (suffixStartIndex == -1 || suffixStartIndex < Prefix.Length)
        {
            throw new JsonException($"Invalid versionToken format: {versionTokenString}");
        }

        var value = versionTokenString.Substring(Prefix.Length, suffixStartIndex - Prefix.Length);
        var schemaVersion = versionTokenString.Substring(suffixStartIndex + 1); // Extract version part after ']'

        if (string.IsNullOrEmpty(schemaVersion))
        {
            throw new JsonException($"Schema version missing in versionToken: {versionTokenString}");
        }

        var versionToken = new VersionToken(value) { SchemaVersion = schemaVersion };
        return versionToken;
    }

    /// <summary>
    /// Writes a <see cref="VersionToken"/> as JSON.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The <see cref="VersionToken"/> to write.</param>
    /// <param name="options">Serializer options.</param>
    /// <exception cref="InvalidOperationException">Thrown when the SchemaVersion is null or empty.</exception>
    public override void Write(Utf8JsonWriter writer, VersionToken value, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(value.SchemaVersion))
        {
            throw new InvalidOperationException("SchemaVersion cannot be null or empty when writing a VersionToken.");
        }

        var formattedValue = $"{Prefix}{value.Value}]{value.SchemaVersion}";
        writer.WriteStringValue(formattedValue);
    }

    /// <summary>
    /// Reads and converts a JSON property name to a <see cref="VersionToken"/> for use as a dictionary key.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The type being converted.</param>
    /// <param name="options">Serializer options.</param>
    /// <returns>A <see cref="VersionToken"/> parsed from the JSON property name.</returns>
    /// <exception cref="JsonException">Thrown when the JSON format is invalid.</exception>
    public override VersionToken ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var versionTokenString = reader.GetString();
        
        // TODO: temp hack
        if (versionTokenString != null && versionTokenString.StartsWith("versionToken["))
        {
            versionTokenString = "vt[" + versionTokenString[13..];
        }
        
        if (string.IsNullOrEmpty(versionTokenString) || !versionTokenString.StartsWith(Prefix))
        {
            throw new JsonException($"Invalid versionToken format as property name: {versionTokenString}");
        }

        var suffixStartIndex = versionTokenString.IndexOf(SuffixStart);
        if (suffixStartIndex == -1 || suffixStartIndex < Prefix.Length)
        {
            throw new JsonException($"Invalid versionToken format as property name: {versionTokenString}");
        }

        var value = versionTokenString.Substring(Prefix.Length, suffixStartIndex - Prefix.Length);
        var schemaVersion = versionTokenString.Substring(suffixStartIndex + 1);

        if (string.IsNullOrEmpty(schemaVersion))
        {
            throw new JsonException($"Schema version missing in versionToken: {versionTokenString}");
        }

        return new VersionToken(value) { SchemaVersion = schemaVersion };
    }

    /// <summary>
    /// Writes a <see cref="VersionToken"/> as a JSON property name for use as a dictionary key.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The <see cref="VersionToken"/> to write as a property name.</param>
    /// <param name="options">Serializer options.</param>
    /// <exception cref="InvalidOperationException">Thrown when the SchemaVersion is null or empty.</exception>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, VersionToken value, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(value.SchemaVersion))
        {
            throw new InvalidOperationException("SchemaVersion cannot be null or empty when writing a VersionToken as a dictionary key.");
        }

        var formattedValue = $"{Prefix}{value.Value}]{value.SchemaVersion}";
        writer.WritePropertyName(formattedValue);
    }
}