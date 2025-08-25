using Microsoft.CodeAnalysis;

namespace ErikLieben.FA.ES.CLI.Analyze.Helpers;

internal static class TypeCollector
{
    private static bool IsSystemNoiseType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.Name.Contains("Nullable") || typeSymbol.Name.Contains("Int32") ||
            typeSymbol.Name.Contains("Int64"))
        {
            return true;
        }

        // Exclude common system-level noise (attributes, enum helpers, layouts, etc.)
        return typeSymbol.ToDisplayString() switch
        {
            "System.RuntimeTypeHandle" => true,
            "System.Char" => true,
            "System.Nullable" => true,
            "System.Object" => true,
            "System.Attribute" => true,

            "System.Type" => true,
            "System.Runtime.InteropServices.StructLayoutAttribute" => true,
            "System.Runtime.InteropServices.LayoutKind" => true,
            _ => false
        };
    }

    private static bool IsFromUnwantedNamespace(ITypeSymbol typeSymbol)
    {
        var namespaceName = typeSymbol.ContainingNamespace.ToDisplayString();
        return namespaceName.StartsWith("System.Reflection") // Skip reflection-related types
               || namespaceName.StartsWith("System.Collections.Generic") // Skip generic collections
               || namespaceName.StartsWith("System.Linq") // Skip LINQ-related types
               || namespaceName.StartsWith("System.Runtime.InteropServices"); // Skip interop types
    }


    internal static void GetAllTypesInClass(INamedTypeSymbol? typeSymbol, HashSet<ITypeSymbol> collectedTypes)
    {
        if (IsSystemNoiseType(typeSymbol))
        {
            return;
        }

        if (typeSymbol == null || collectedTypes.Contains(typeSymbol) ||
            typeSymbol.SpecialType == SpecialType.System_Object)
            return;

        // Stop recursion for primitive and system types, but allow concrete types like Guid, String, Int32, etc.
        if (typeSymbol.SpecialType != SpecialType.None || IsSystemType(typeSymbol) || IsExcludedBaseType(typeSymbol))
        {
            collectedTypes.Add(typeSymbol);
            return;
        }

        // Avoid collecting interfaces
        if (typeSymbol.TypeKind == TypeKind.Interface)
            return;

        // Avoid types from Reflection namespace or other unrelated types
        if (IsFromUnwantedNamespace(typeSymbol))
            return;

        // Add this type to the collection (custom class, enum, struct, etc.)
        collectedTypes.Add(typeSymbol);

        if (typeSymbol.IsGenericType)
        {
            foreach (var typeSymbolTypeArgument in typeSymbol.TypeArguments)
            {
                collectedTypes.Add(typeSymbolTypeArgument);
            }
        }

        // Process members of the type (properties, fields, etc.)
        foreach (var member in typeSymbol.GetMembers())
        {
            // Check if the member is a property
            if (member is IPropertySymbol propertySymbol)
            {
                var propertyType = propertySymbol.Type;

                // If propertyType is another named type, analyze it
                if (propertyType is INamedTypeSymbol namedType)
                {
                    GetAllTypesInClass(namedType, collectedTypes);

                    // Handle generics (e.g., IList<FeatureFlag>, List<FeatureFlag>)
                    if (namedType.IsGenericType)
                    {
                        foreach (var typeArgument in namedType.TypeArguments)
                        {
                            if (typeArgument is INamedTypeSymbol genericTypeArgument)
                            {
                                GetAllTypesInClass(genericTypeArgument, collectedTypes);
                            }
                        }
                    }
                }
                // If the propertyType is an array, analyze its element type
                else if (propertyType is IArrayTypeSymbol arrayType)
                {
                    GetAllTypesInClass(arrayType.ElementType as INamedTypeSymbol, collectedTypes);
                }
            }
        }

        // Recursively analyze the base type, but not for excluded types (e.g., System.ValueType)
        if (typeSymbol.BaseType != null && !IsExcludedBaseType(typeSymbol.BaseType))
        {
            GetAllTypesInClass(typeSymbol.BaseType, collectedTypes);
        }
    }



    private static bool IsExcludedBaseType(INamedTypeSymbol typeSymbol)
    {
        // Exclude types like System.ValueType
        return typeSymbol.ToDisplayString() == "System.ValueType";
    }


    private static bool IsSystemType(ITypeSymbol typeSymbol)
    {
        // Stop for common system-defined types like string, int, etc.
        return typeSymbol.SpecialType switch
        {
            SpecialType.System_String => true,
            SpecialType.System_Int32 => true,
            SpecialType.System_Boolean => true,
            SpecialType.System_Object => false,
            SpecialType.System_Char => true,
            SpecialType.System_Byte => true,
            SpecialType.System_Single => true,
            SpecialType.System_Double => true,
            SpecialType.System_Decimal => true,
            SpecialType.System_UInt16 => true,
            SpecialType.System_UInt32 => true,
            SpecialType.System_UInt64 => true,
            SpecialType.System_Int16 => true,
            SpecialType.System_Int64 => true,
            SpecialType.System_DateTime => true,
            _ => false
        };
    }

    private static bool IsPrimitive(ITypeSymbol typeSymbol)
    {
        return typeSymbol.SpecialType switch
        {
            SpecialType.System_Char => true,
            SpecialType.System_Boolean => true,
            SpecialType.System_Byte => true,
            SpecialType.System_SByte => true,
            SpecialType.System_Int16 => true,
            SpecialType.System_UInt16 => true,
            SpecialType.System_Int32 => true,
            SpecialType.System_UInt32 => true,
            SpecialType.System_Int64 => true,
            SpecialType.System_UInt64 => true,
            SpecialType.System_Single => true,
            SpecialType.System_Double => true,
            SpecialType.System_Decimal => true,
            _ => false
        };
    }
}
