using System.Collections.Immutable;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;

namespace ErikLieben.FA.ES.CLI.Analyze.Helpers;

internal static class PropertyHelper
{
    internal static List<PropertyDefinition> GetPublicGetterProperties(ITypeSymbol typeSymbol)
    {
        return typeSymbol
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p =>
                // Check that property is not private or internal
                p.DeclaredAccessibility != Accessibility.Private &&
                p.DeclaredAccessibility != Accessibility.Internal &&
                // Check that the getter exists and is not private or internal
                p.GetMethod != null &&
                p.GetMethod.DeclaredAccessibility != Accessibility.Private &&
                p.GetMethod.DeclaredAccessibility != Accessibility.Internal &&
                // Exclude EqualityContract
                p.Name != "EqualityContract")
            .Select(p =>
            {
                if (p.Type is not INamedTypeSymbol namedTypeSymbol)
                {
                    return null;
                }

                // PropertyGenericTypeDefinition
                var genericTypes = GetGenericTypes(namedTypeSymbol.TypeArguments);
                var namespaceString = RoslynHelper.GetFullNamespace(namedTypeSymbol);

                HashSet<ITypeSymbol> collectedTypes = [];
                TypeCollector.GetAllTypesInClass(namedTypeSymbol, collectedTypes);

                return new PropertyDefinition
                {
                    Name = p.Name,
                    Type = RoslynHelper.GetFullTypeName(namedTypeSymbol),
                    Namespace = namespaceString,
                    IsNullable = RoslynHelper.IsSystemNullable(namedTypeSymbol) ||
                                 RoslynHelper.IsExplicitlyNullableType(namedTypeSymbol),
                    GenericTypes = genericTypes,
                    SubTypes = collectedTypes
                        .Where(t => t.Name != namedTypeSymbol.Name)
                        .Select(t =>
                            new PropertyGenericTypeDefinition(
                                RoslynHelper.GetFullTypeNameIncludingGenerics(t),
                                RoslynHelper.GetFullNamespace(t),
                                [],
                                []))
                        .ToList() ?? [],
                };
            })
            .Where(p => p != null)
            .Select(p => p!)
            .ToList();

    }

    internal static List<PropertyDefinition> GetPublicGetterPropertiesIncludeParentDefinitions(ITypeSymbol typeSymbol)
    {
        var result = new List<PropertyDefinition>();
        var currentType = typeSymbol;

        // Collect properties from the type and all its base types
        while (currentType != null)
        {
            var properties = currentType
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p =>
                    // Check that property is not private or internal
                    p.DeclaredAccessibility != Accessibility.Private &&
                    p.DeclaredAccessibility != Accessibility.Internal &&
                    // Check that the getter exists and is not private or internal
                    p.GetMethod != null &&
                    p.GetMethod.DeclaredAccessibility != Accessibility.Private &&
                    p.GetMethod.DeclaredAccessibility != Accessibility.Internal)
                .Select(p =>
                {
                    if (p.Type is not INamedTypeSymbol namedTypeSymbol)
                    {
                        return null;
                    }

                    // PropertyGenericTypeDefinition
                    var genericTypes = GetGenericTypes(namedTypeSymbol.TypeArguments);
                    var namespaceString = RoslynHelper.GetFullNamespace(namedTypeSymbol);
                    HashSet<ITypeSymbol> collectedTypes = [];
                    TypeCollector.GetAllTypesInClass(namedTypeSymbol, collectedTypes);
                    return new PropertyDefinition
                    {
                        Name = p.Name,
                        Type = RoslynHelper.GetFullTypeName(namedTypeSymbol),
                        Namespace = namespaceString,
                        IsNullable = RoslynHelper.IsSystemNullable(namedTypeSymbol) ||
                                     RoslynHelper.IsExplicitlyNullableType(namedTypeSymbol),
                        GenericTypes = genericTypes,
                        SubTypes = collectedTypes
                            .Where(t => t.Name != namedTypeSymbol.Name)
                            .Select(t =>
                                new PropertyGenericTypeDefinition(
                                    RoslynHelper.GetFullTypeNameIncludingGenerics(t),
                                    RoslynHelper.GetFullNamespace(t),
                                    [],
                                    []))
                            .ToList() ?? [],
                    };
                })
                .Where(p => p != null)
                .Select(p => p!);

            result.AddRange(properties);

            // Move to the base type
            currentType = currentType.BaseType;
        }

        // Remove duplicate properties (if a property is overridden in a derived class)
        return result
            .GroupBy(p => p.Name)
            .Select(g => g.First()) // Take the most derived implementation
            .ToList();
    }

    private static List<PropertyGenericTypeDefinition> GetGenericTypes(ImmutableArray<ITypeSymbol>? typeArguments)
    {
        if (typeArguments == null || typeArguments.Value.Length == 0)
        {
            return [];
        }


        return typeArguments
            .Where<ITypeSymbol>(t => t.TypeKind != TypeKind.Error)
            .Select(t =>
            {
                return new PropertyGenericTypeDefinition(
                    RoslynHelper.GetFullTypeName(t),
                    RoslynHelper.GetFullNamespace(t),
                    GetGenericTypes((t as INamedTypeSymbol)?.TypeArguments),
                    []
                );
            }).ToList();
    }
}
