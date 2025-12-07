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

        // Handle positional arguments constructor
        // Example: [EventStreamType("table", "table")] or [EventStreamType("blob", "cosmos", "table")]
        // Order: streamType, documentType, documentTagType, eventStreamTagType, documentRefType
        if (attribute.ConstructorArguments.Length > 1)
        {
            var args = attribute.ConstructorArguments;
            return new EventStreamTypeAttributeData
            {
                StreamType = args.Length > 0 ? args[0].Value as string : null,
                DocumentType = args.Length > 1 ? args[1].Value as string : null,
                DocumentTagType = args.Length > 2 ? args[2].Value as string : null,
                EventStreamTagType = args.Length > 3 ? args[3].Value as string : null,
                DocumentRefType = args.Length > 4 ? args[4].Value as string : null
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

    /// <summary>
    /// Extracts the schema version from [EventVersion] attribute on an event type.
    /// Returns 1 (default) if the attribute is not present.
    /// </summary>
    public static int ExtractEventVersionAttribute(INamedTypeSymbol eventSymbol)
    {
        var attribute = eventSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "EventVersionAttribute");

        if (attribute == null)
            return 1; // Default schema version

        // [EventVersion(2)] - single int constructor argument
        if (attribute.ConstructorArguments.Length == 1 &&
            attribute.ConstructorArguments[0].Value is int version)
        {
            return version;
        }

        return 1; // Default if parsing fails
    }

    /// <summary>
    /// Extracts [UseUpcaster&lt;T&gt;] attributes from an aggregate class.
    /// Returns a list of upcaster definitions to register.
    /// </summary>
    public static List<UpcasterDefinition> ExtractUseUpcasterAttributes(INamedTypeSymbol aggregateSymbol)
    {
        var upcasters = new List<UpcasterDefinition>();

        // Look for generic UseUpcasterAttribute<T> - the name will be "UseUpcasterAttribute" with TypeArguments
        var attributes = aggregateSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.Name == "UseUpcasterAttribute" &&
                       a.AttributeClass.IsGenericType);

        foreach (var attribute in attributes)
        {
            // [UseUpcaster<MyUpcaster>] - generic type argument
            var attributeClass = attribute.AttributeClass;
            if (attributeClass?.TypeArguments.Length == 1 &&
                attributeClass.TypeArguments[0] is INamedTypeSymbol upcasterType)
            {
                upcasters.Add(new UpcasterDefinition
                {
                    TypeName = upcasterType.Name,
                    Namespace = upcasterType.ContainingNamespace.ToDisplayString()
                });
            }
        }

        return upcasters;
    }
}
