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
        if (ShouldSkipType(typeSymbol, collectedTypes))
        {
            return;
        }

        // Stop recursion for primitive and system types, but allow concrete types like Guid, String, Int32, etc.
        if (typeSymbol!.SpecialType != SpecialType.None || IsSystemType(typeSymbol) || IsExcludedBaseType(typeSymbol))
        {
            collectedTypes.Add(typeSymbol);
            return;
        }

        // Add this type to the collection (custom class, enum, struct, etc.)
        collectedTypes.Add(typeSymbol);
        CollectGenericTypeArguments(typeSymbol, collectedTypes);
        ProcessTypeMembers(typeSymbol, collectedTypes);
        ProcessBaseType(typeSymbol, collectedTypes);
    }

    private static bool ShouldSkipType(INamedTypeSymbol? typeSymbol, HashSet<ITypeSymbol> collectedTypes)
    {
        if (typeSymbol == null)
        {
            return true;
        }

        if (IsSystemNoiseType(typeSymbol))
        {
            return true;
        }

        if (collectedTypes.Contains(typeSymbol) ||
            typeSymbol.SpecialType == SpecialType.System_Object)
        {
            return true;
        }

        // Avoid collecting interfaces
        if (typeSymbol.TypeKind == TypeKind.Interface)
        {
            return true;
        }

        // Avoid types from Reflection namespace or other unrelated types
        if (IsFromUnwantedNamespace(typeSymbol))
        {
            return true;
        }

        return false;
    }

    private static void CollectGenericTypeArguments(INamedTypeSymbol typeSymbol, HashSet<ITypeSymbol> collectedTypes)
    {
        if (typeSymbol.IsGenericType)
        {
            foreach (var typeSymbolTypeArgument in typeSymbol.TypeArguments)
            {
                collectedTypes.Add(typeSymbolTypeArgument);
            }
        }
    }

    private static void ProcessTypeMembers(INamedTypeSymbol typeSymbol, HashSet<ITypeSymbol> collectedTypes)
    {
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IPropertySymbol propertySymbol)
            {
                ProcessPropertyType(propertySymbol.Type, collectedTypes);
            }
        }
    }

    private static void ProcessPropertyType(ITypeSymbol propertyType, HashSet<ITypeSymbol> collectedTypes)
    {
        if (propertyType is INamedTypeSymbol namedType)
        {
            GetAllTypesInClass(namedType, collectedTypes);
            ProcessGenericTypeArguments(namedType, collectedTypes);
        }
        else if (propertyType is IArrayTypeSymbol arrayType)
        {
            GetAllTypesInClass(arrayType.ElementType as INamedTypeSymbol, collectedTypes);
        }
    }

    private static void ProcessGenericTypeArguments(INamedTypeSymbol namedType, HashSet<ITypeSymbol> collectedTypes)
    {
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

    private static void ProcessBaseType(INamedTypeSymbol typeSymbol, HashSet<ITypeSymbol> collectedTypes)
    {
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
}
