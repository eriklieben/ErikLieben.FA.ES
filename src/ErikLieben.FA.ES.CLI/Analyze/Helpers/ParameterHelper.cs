using System.Collections.Immutable;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;

namespace ErikLieben.FA.ES.CLI.Analyze.Helpers;

internal static class ParameterHelper
{
    internal static List<ParameterDefinition> GetParameters(IMethodSymbol typeSymbol)
    {
        return typeSymbol
            .Parameters
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

                return new ParameterDefinition
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
                            new ParameterGenericTypeDefinition(
                                RoslynHelper.GetFullTypeNameIncludingGenerics(t),
                                RoslynHelper.GetFullNamespace(t),
                                [],
                                []))
                        .ToList(),
                };
            })
            .Where(p => p != null)
            .Select(p => p!)
            .ToList();

    }

    private static List<ParameterGenericTypeDefinition> GetGenericTypes(ImmutableArray<ITypeSymbol>? typeArguments)
    {
        if (typeArguments == null || typeArguments.Value.Length == 0)
        {
            return [];
        }


        return typeArguments
            .Where<ITypeSymbol>(t => t.TypeKind != TypeKind.Error)
            .Select(t => new ParameterGenericTypeDefinition(
                RoslynHelper.GetFullTypeName(t),
                RoslynHelper.GetFullNamespace(t),
                GetGenericTypes((t as INamedTypeSymbol)?.TypeArguments),
                []
            )).ToList();
    }
}
