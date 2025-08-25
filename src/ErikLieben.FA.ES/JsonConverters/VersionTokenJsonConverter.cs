using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.JsonConverters;

public class VersionTokenJsonConverter : JsonConverter<VersionToken>
{
    private const string Prefix = "vt[";
    private const char SuffixStart = ']';

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

    public override void Write(Utf8JsonWriter writer, VersionToken value, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(value.SchemaVersion))
        {
            throw new InvalidOperationException("SchemaVersion cannot be null or empty when writing a VersionToken.");
        }

        var formattedValue = $"{Prefix}{value.Value}]{value.SchemaVersion}";
        writer.WriteStringValue(formattedValue);
    }

    // Handling dictionary keys
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

// public class VersionTokenJsonConverter : JsonConverter<VersionToken>
// {
//     private const string Prefix = "versionToken[";
//     private const char SuffixStart = ']';
//
//     public override VersionToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
//     {
//         var versionTokenString = reader.GetString();
//         if (string.IsNullOrEmpty(versionTokenString) || !versionTokenString.StartsWith(Prefix))
//         {
//             throw new JsonException($"Invalid versionToken format: {versionTokenString}");
//         }
//
//         var suffixStartIndex = versionTokenString.IndexOf(SuffixStart);
//         if (suffixStartIndex == -1 || suffixStartIndex < Prefix.Length)
//         {
//             throw new JsonException($"Invalid versionToken format: {versionTokenString}");
//         }
//
//         var value = versionTokenString.Substring(Prefix.Length, suffixStartIndex - Prefix.Length);
//         var schemaVersion = versionTokenString.Substring(suffixStartIndex + 1); // Extract version part after ']'
//
//         if (string.IsNullOrEmpty(schemaVersion))
//         {
//             throw new JsonException($"Schema version missing in versionToken: {versionTokenString}");
//         }
//
//         // You can then use `schemaVersion` to populate SchemaVersion in VersionToken
//         var versionToken = new VersionToken(value) { SchemaVersion = schemaVersion };
//         return versionToken;
//     }
//
//     public override void Write(Utf8JsonWriter writer, VersionToken value, JsonSerializerOptions options)
//     {
//         if (string.IsNullOrEmpty(value.SchemaVersion))
//         {
//             throw new InvalidOperationException("SchemaVersion cannot be null or empty when writing a VersionToken.");
//         }
//
//         var formattedValue = $"{Prefix}{value.Value}]{value.SchemaVersion}";
//         writer.WriteStringValue(formattedValue);
//     }
// }