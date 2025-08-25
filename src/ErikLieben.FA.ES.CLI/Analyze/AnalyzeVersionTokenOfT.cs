using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.Analyze;

public class AnalyzeVersionTokenOfT(INamedTypeSymbol recordSymbol, string solutionRootPath)
{
    public void Run(List<VersionTokenDefinition> versionTokens)
    {
        if (recordSymbol.BaseType is not { IsGenericType: true, Name: "VersionToken", TypeArguments.Length: 1 })
        {
            return;
        }

        AnsiConsole.MarkupLine("Analyzing versionToken<T>: [yellow]" + recordSymbol!.Name + "[/]");
        versionTokens.Add(new VersionTokenDefinition
        {
            IsPartialClass =recordSymbol.DeclaringSyntaxReferences
                .Any(reference => reference.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax typeDecl
                                  && typeDecl.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))),
            Namespace = RoslynHelper.GetFullNamespace(recordSymbol),
            Name = RoslynHelper.GetFullTypeName(recordSymbol),
            GenericType = RoslynHelper.GetFullTypeName(recordSymbol.BaseType.TypeArguments[0]),
            NamespaceOfType = RoslynHelper.GetFullNamespace(recordSymbol.BaseType.TypeArguments[0]),
            FileLocations = recordSymbol.Locations
                .Select(l => l.SourceTree?.FilePath?.Replace(solutionRootPath, string.Empty) ?? string.Empty)
                .ToList()
        });
    }
}
