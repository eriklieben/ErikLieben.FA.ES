using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;

namespace ErikLieben.FA.ES.CLI.Analyze.Helpers;
internal class ConstructorHelper
{
    internal static IEnumerable<ConstructorDefinition> GetConstructors(INamedTypeSymbol symbol)
    {
        return symbol.Constructors.Select(c =>
            new ConstructorDefinition
            {
                Parameters = c.Parameters.Select(p => new ConstructorParameter
                {
                    Name = p.Name,
                    Type = RoslynHelper.GetFullTypeName(p.Type),
                    Namespace = RoslynHelper.GetFullNamespace(p.Type),
                    IsNullable = RoslynHelper.IsExplicitlyNullableType(p.Type) || RoslynHelper.IsSystemNullable(p.Type)
                }).ToList()
            });
    }
}
