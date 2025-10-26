using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.JsonConverters;

/// <summary>
/// Provides JSON serialization and deserialization for <see cref="VersionIdentifier"/> using the custom format "vid[&lt;value&gt;]&lt;schemaVersion&gt;".
/// </summary>
public class VersionIdentifierJsonConverter : JsonConverter<VersionIdentifier>
{
    private const string Prefix = "vid[";
    private const char SuffixStart = ']';

    /// <summary>
    /// Reads and converts JSON to a <see cref="VersionIdentifier"/>.
    /// </summary>
    /// <param name="reader">The reader to read JSON from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>The deserialized version identifier.</returns>
    /// <exception cref="JsonException">Thrown when the JSON format is invalid.</exception>
    public override VersionIdentifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var versionIdentifierString = reader.GetString();

        if (string.IsNullOrEmpty(versionIdentifierString) || !versionIdentifierString.StartsWith(Prefix))
        {
            throw new JsonException($"Invalid versionIdentifier format: {versionIdentifierString}");
        }

        var suffixStartIndex = versionIdentifierString.IndexOf(SuffixStart);
        if (suffixStartIndex == -1 || suffixStartIndex < Prefix.Length)
        {
            throw new JsonException($"Invalid versionIdentifier format: {versionIdentifierString}");
        }

        var value = versionIdentifierString.Substring(Prefix.Length, suffixStartIndex - Prefix.Length);
        var schemaVersion = versionIdentifierString.Substring(suffixStartIndex + 1); // Extract version part after ']'

        if (string.IsNullOrEmpty(schemaVersion))
        {
            throw new JsonException($"Schema version missing in versionIdentifier: {versionIdentifierString}");
        }

        var versionIdentifier = new VersionIdentifier(value)
        {
            SchemaVersion = schemaVersion
        };

        return versionIdentifier;
    }

    /// <summary>
    /// Writes a <see cref="VersionIdentifier"/> as JSON.
    /// </summary>
    /// <param name="writer">The writer to write JSON to.</param>
    /// <param name="value">The version identifier to serialize.</param>
    /// <param name="options">Serialization options.</param>
    /// <exception cref="InvalidOperationException">Thrown when the schema version is null or empty.</exception>
    public override void Write(Utf8JsonWriter writer, VersionIdentifier value, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(value.SchemaVersion))
        {
            throw new InvalidOperationException("SchemaVersion cannot be null or empty when writing a VersionIdentifier.");
        }

        var formattedValue = $"{Prefix}{value.Value}]{value.SchemaVersion}";
        writer.WriteStringValue(formattedValue);
    }

    /// <summary>
    /// Reads and converts a JSON property name to a <see cref="VersionIdentifier"/> for use as a dictionary key.
    /// </summary>
    /// <param name="reader">The reader to read JSON from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>The deserialized version identifier.</returns>
    /// <exception cref="JsonException">Thrown when the JSON format is invalid.</exception>
    public override VersionIdentifier ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var versionIdentifierString = reader.GetString();
        if (string.IsNullOrEmpty(versionIdentifierString) || !versionIdentifierString.StartsWith(Prefix))
        {
            throw new JsonException($"Invalid versionIdentifier format as property name: {versionIdentifierString}");
        }

        var suffixStartIndex = versionIdentifierString.IndexOf(SuffixStart);
        if (suffixStartIndex == -1 || suffixStartIndex < Prefix.Length)
        {
            throw new JsonException($"Invalid versionIdentifier format as property name: {versionIdentifierString}");
        }

        var value = versionIdentifierString.Substring(Prefix.Length, suffixStartIndex - Prefix.Length);
        var schemaVersion = versionIdentifierString.Substring(suffixStartIndex + 1);

        if (string.IsNullOrEmpty(schemaVersion))
        {
            throw new JsonException($"Schema version missing in versionIdentifier: {versionIdentifierString}");
        }

        return new VersionIdentifier(value)
        {
            SchemaVersion = schemaVersion
        };
    }

    /// <summary>
    /// Writes a <see cref="VersionIdentifier"/> as a JSON property name for use as a dictionary key.
    /// </summary>
    /// <param name="writer">The writer to write JSON to.</param>
    /// <param name="value">The version identifier to serialize.</param>
    /// <param name="options">Serialization options.</param>
    /// <exception cref="InvalidOperationException">Thrown when the schema version is null or empty.</exception>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, VersionIdentifier value, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(value.SchemaVersion))
        {
            throw new InvalidOperationException("SchemaVersion cannot be null or empty when writing a VersionIdentifier as a dictionary key.");
        }

        var formattedValue = $"{Prefix}{value.Value}]{value.SchemaVersion}";
        writer.WritePropertyName(formattedValue);
    }
}

// public class VersionIdentifierJsonConverter : JsonConverter<VersionIdentifier>
// {
//     private const string Prefix = "vid[";
//     private const char SuffixStart = ']';
//
//     public override VersionIdentifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
//     {
//         var versionIdentifierString = reader.GetString();
//
//         if (string.IsNullOrEmpty(versionIdentifierString) || !versionIdentifierString.StartsWith(Prefix))
//         {
//             throw new JsonException($"Invalid versionIdentifier format: {versionIdentifierString}");
//         }
//
//         var suffixStartIndex = versionIdentifierString.IndexOf(SuffixStart);
//         if (suffixStartIndex == -1 || suffixStartIndex < Prefix.Length)
//         {
//             throw new JsonException($"Invalid versionIdentifier format: {versionIdentifierString}");
//         }
//
//         var value = versionIdentifierString.Substring(Prefix.Length, suffixStartIndex - Prefix.Length);
//         var schemaVersion = versionIdentifierString.Substring(suffixStartIndex + 1); // Extract version part after ']'
//
//         if (string.IsNullOrEmpty(schemaVersion))
//         {
//             throw new JsonException($"Schema version missing in versionIdentifier: {versionIdentifierString}");
//         }
//
//         var versionIdentifier = new VersionIdentifier(value)
//         {
//             SchemaVersion = schemaVersion
//         };
//
//         return versionIdentifier;
//     }
//
//     public override void Write(Utf8JsonWriter writer, VersionIdentifier value, JsonSerializerOptions options)
//     {
//         if (string.IsNullOrEmpty(value.SchemaVersion))
//         {
//             throw new InvalidOperationException("SchemaVersion cannot be null or empty when writing a VersionIdentifier.");
//         }
//
//         var formattedValue = $"{Prefix}{value.Value}]{value.SchemaVersion}";
//         writer.WriteStringValue(formattedValue);
//     }
// }
