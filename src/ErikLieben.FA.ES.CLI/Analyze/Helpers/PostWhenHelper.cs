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

        var postWhen = postWhenMethods.FirstOrDefault();
        if (postWhen == null)
        {
            return null;
        }

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

    private static bool IsInGeneratedFile(INamedTypeSymbol classSymbol)
    {
        var syntaxReferences = classSymbol.DeclaringSyntaxReferences;

        foreach (var syntaxReference in syntaxReferences)
        {
            var syntaxTree = syntaxReference.SyntaxTree;
            var filePath = syntaxTree?.FilePath;

            if (!string.IsNullOrEmpty(filePath) && filePath.EndsWith(".Generated.cs", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

}
