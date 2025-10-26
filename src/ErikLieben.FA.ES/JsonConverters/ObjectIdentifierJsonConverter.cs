using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.JsonConverters;

/// <summary>
/// Provides JSON serialization and deserialization for <see cref="ObjectIdentifier"/> using the custom format "oid[&lt;value&gt;]&lt;schemaVersion&gt;".
/// </summary>
public class ObjectIdentifierJsonConverter : JsonConverter<ObjectIdentifier>
{
    private const string Prefix = "oid[";
    private const char SuffixStart = ']';

    /// <summary>
    /// Reads and converts JSON to an <see cref="ObjectIdentifier"/>.
    /// </summary>
    /// <param name="reader">The reader to read JSON from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>The deserialized object identifier.</returns>
    /// <exception cref="JsonException">Thrown when the JSON format is invalid.</exception>
    public override ObjectIdentifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var objectIdentifierString = reader.GetString();

        if (string.IsNullOrEmpty(objectIdentifierString) || !objectIdentifierString.StartsWith(Prefix))
        {
            throw new JsonException($"Invalid objectIdentifier format: {objectIdentifierString}");
        }

        var suffixStartIndex = objectIdentifierString.IndexOf(SuffixStart);
        if (suffixStartIndex == -1 || suffixStartIndex < Prefix.Length)
        {
            throw new JsonException($"Invalid objectIdentifier format: {objectIdentifierString}");
        }

        var value = objectIdentifierString.Substring(Prefix.Length, suffixStartIndex - Prefix.Length);
        var schemaVersion = objectIdentifierString.Substring(suffixStartIndex + 1); // Extract version part after ']'

        if (string.IsNullOrEmpty(schemaVersion))
        {
            throw new JsonException($"Schema version missing in objectIdentifier: {objectIdentifierString}");
        }

        var objectIdentifier = new ObjectIdentifier(value)
        {
            SchemaVersion = schemaVersion
        };

        return objectIdentifier;
    }

    /// <summary>
    /// Writes an <see cref="ObjectIdentifier"/> as JSON.
    /// </summary>
    /// <param name="writer">The writer to write JSON to.</param>
    /// <param name="value">The object identifier to serialize.</param>
    /// <param name="options">Serialization options.</param>
    /// <exception cref="InvalidOperationException">Thrown when the schema version is null or empty.</exception>
    public override void Write(Utf8JsonWriter writer, ObjectIdentifier value, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(value.SchemaVersion))
        {
            throw new InvalidOperationException("SchemaVersion cannot be null or empty when writing an ObjectIdentifier.");
        }

        var formattedValue = $"{Prefix}{value.Value}]{value.SchemaVersion}";
        writer.WriteStringValue(formattedValue);
    }

    /// <summary>
    /// Reads and converts a JSON property name to an <see cref="ObjectIdentifier"/> for use as a dictionary key.
    /// </summary>
    /// <param name="reader">The reader to read JSON from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>The deserialized object identifier.</returns>
    /// <exception cref="JsonException">Thrown when the JSON format is invalid.</exception>
    public override ObjectIdentifier ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var objectIdentifierString = reader.GetString();
        if (string.IsNullOrEmpty(objectIdentifierString) || !objectIdentifierString.StartsWith(Prefix))
        {
            throw new JsonException($"Invalid objectIdentifier format as property name: {objectIdentifierString}");
        }

        var suffixStartIndex = objectIdentifierString.IndexOf(SuffixStart);
        if (suffixStartIndex == -1 || suffixStartIndex < Prefix.Length)
        {
            throw new JsonException($"Invalid objectIdentifier format as property name: {objectIdentifierString}");
        }

        var value = objectIdentifierString.Substring(Prefix.Length, suffixStartIndex - Prefix.Length);
        var schemaVersion = objectIdentifierString.Substring(suffixStartIndex + 1);

        if (string.IsNullOrEmpty(schemaVersion))
        {
            throw new JsonException($"Schema version missing in objectIdentifier: {objectIdentifierString}");
        }

        return new ObjectIdentifier(value)
        {
            SchemaVersion = schemaVersion
        };
    }

    /// <summary>
    /// Writes an <see cref="ObjectIdentifier"/> as a JSON property name for use as a dictionary key.
    /// </summary>
    /// <param name="writer">The writer to write JSON to.</param>
    /// <param name="value">The object identifier to serialize.</param>
    /// <param name="options">Serialization options.</param>
    /// <exception cref="InvalidOperationException">Thrown when the schema version is null or empty.</exception>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, ObjectIdentifier value, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(value.SchemaVersion))
        {
            throw new InvalidOperationException("SchemaVersion cannot be null or empty when writing an ObjectIdentifier as a dictionary key.");
        }

        var formattedValue = $"{Prefix}{value.Value}]{value.SchemaVersion}";
        writer.WritePropertyName(formattedValue);
    }
}


// public class ObjectIdentifierJsonConverter : JsonConverter<ObjectIdentifier>
// {
//     private const string Prefix = "oid[";
//     private const char SuffixStart = ']';
//
//     public override ObjectIdentifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
//     {
//         var objectIdentifierString = reader.GetString();
//
//         if (string.IsNullOrEmpty(objectIdentifierString) || !objectIdentifierString.StartsWith(Prefix))
//         {
//             throw new JsonException($"Invalid objectIdentifier format: {objectIdentifierString}");
//         }
//
//         var suffixStartIndex = objectIdentifierString.IndexOf(SuffixStart);
//         if (suffixStartIndex == -1 || suffixStartIndex < Prefix.Length)
//         {
//             throw new JsonException($"Invalid objectIdentifier format: {objectIdentifierString}");
//         }
//
//         var value = objectIdentifierString.Substring(Prefix.Length, suffixStartIndex - Prefix.Length);
//         var schemaVersion = objectIdentifierString.Substring(suffixStartIndex + 1); // Extract version part after ']'
//
//         if (string.IsNullOrEmpty(schemaVersion))
//         {
//             throw new JsonException($"Schema version missing in objectIdentifier: {objectIdentifierString}");
//         }
//
//         var objectIdentifier = new ObjectIdentifier(value)
//         {
//             SchemaVersion = schemaVersion
//         };
//
//         return objectIdentifier;
//     }
//
//     public override void Write(Utf8JsonWriter writer, ObjectIdentifier value, JsonSerializerOptions options)
//     {
//         if (string.IsNullOrEmpty(value.SchemaVersion))
//         {
//             throw new InvalidOperationException("SchemaVersion cannot be null or empty when writing an ObjectIdentifier.");
//         }
//
//         var formattedValue = $"{Prefix}{value.Value}]{value.SchemaVersion}";
//         writer.WriteStringValue(formattedValue);
//     }
// }
