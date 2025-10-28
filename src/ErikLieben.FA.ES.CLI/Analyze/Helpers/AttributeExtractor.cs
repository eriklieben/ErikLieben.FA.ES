using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;

namespace ErikLieben.FA.ES.CLI.Analyze.Helpers;

/// <summary>
/// Helper class for extracting attribute data from type symbols.
/// </summary>
public static class AttributeExtractor
{
    /// <summary>
    /// Extracts [EventStreamType] attribute data from an aggregate class.
    /// </summary>
    public static EventStreamTypeAttributeData? ExtractEventStreamTypeAttribute(
        INamedTypeSymbol aggregateSymbol)
    {
        var attribute = aggregateSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "EventStreamTypeAttribute");

        if (attribute == null)
            return null;

        // Handle constructor with single "all" parameter
        // Example: [EventStreamType("blob")]
        if (attribute.ConstructorArguments.Length == 1 &&
            attribute.ConstructorArguments[0].Value is string allValue)
        {
            return new EventStreamTypeAttributeData
            {
                StreamType = allValue,
                DocumentType = allValue,
                DocumentTagType = allValue,
                EventStreamTagType = allValue,
                DocumentRefType = allValue
            };
        }

        // Handle named arguments constructor
        // Example: [EventStreamType(streamType: "blob", documentType: "cosmos")]
        var data = new EventStreamTypeAttributeData();

        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Value.Value is not string value)
                continue;

            data = namedArg.Key switch
            {
                "StreamType" => data with { StreamType = value },
                "DocumentType" => data with { DocumentType = value },
                "DocumentTagType" => data with { DocumentTagType = value },
                "EventStreamTagType" => data with { EventStreamTagType = value },
                "DocumentRefType" => data with { DocumentRefType = value },
                _ => data
            };
        }

        return data;
    }

    /// <summary>
    /// Extracts [EventStreamBlobSettings] attribute data from an aggregate class.
    /// </summary>
    public static EventStreamBlobSettingsAttributeData? ExtractEventStreamBlobSettingsAttribute(
        INamedTypeSymbol aggregateSymbol)
    {
        var attribute = aggregateSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "EventStreamBlobSettingsAttribute");

        if (attribute == null)
            return null;

        // Handle constructor with single "all" parameter
        // Example: [EventStreamBlobSettings("Store2")]
        if (attribute.ConstructorArguments.Length == 1 &&
            attribute.ConstructorArguments[0].Value is string allValue)
        {
            return new EventStreamBlobSettingsAttributeData
            {
                DataStore = allValue,
                DocumentStore = allValue,
                DocumentTagStore = allValue,
                StreamTagStore = allValue,
                SnapShotStore = allValue
            };
        }

        // Handle named arguments
        var data = new EventStreamBlobSettingsAttributeData();

        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Value.Value is not string value)
                continue;

            data = namedArg.Key switch
            {
                "DataStore" => data with { DataStore = value },
                "DocumentStore" => data with { DocumentStore = value },
                "DocumentTagStore" => data with { DocumentTagStore = value },
                "StreamTagStore" => data with { StreamTagStore = value },
                "SnapShotStore" => data with { SnapShotStore = value },
                _ => data
            };
        }

        return data;
    }
}
