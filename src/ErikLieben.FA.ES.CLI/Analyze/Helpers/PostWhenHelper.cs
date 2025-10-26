using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;

namespace ErikLieben.FA.ES.CLI.Analyze.Helpers;

internal static class PostWhenHelper
{
    internal static PostWhenDeclaration? GetPostWhenMethod(INamedTypeSymbol symbol)
    {
        var postWhenMethods = symbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.Name == "PostWhen")
            .ToList();

        if (postWhenMethods.Count == 0)
        {
            return null;
        }

        var postWhen = postWhenMethods[0];

        var postWhenDeclaration = new PostWhenDeclaration();
        foreach (var parameter in postWhen.Parameters)
        {
            postWhenDeclaration.Parameters.Add(new PostWhenParameterDeclaration
            {
                Name = parameter.Name,
                Type = RoslynHelper.GetFullTypeName(parameter.Type),
                Namespace = RoslynHelper.GetFullNamespace(parameter.Type)
            });
        }

        return postWhenDeclaration;
    }

    public static bool HasPostWhenAllMethod(INamedTypeSymbol classSymbol)
    {
        var postAllMethod = classSymbol.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(m => m.Name == "PostWhenAll");
        if (postAllMethod == null)
        {
            return false;
        }

        var attributes = postAllMethod.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "GeneratedCodeAttribute");
        return attributes == null;
    }

}
