using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.Analyze;

public class AnalyzeVersionTokenOfTJsonConverter(INamedTypeSymbol classSymbol, string solutionRootPath)
{
    public void Run(List<VersionTokenJsonConverterDefinition> vtJsonConverters)
    {
        if (classSymbol.BaseType is not { IsGenericType: true, Name: "VersionTokenJsonConverterBase", TypeArguments.Length: 1 })
        {
            return;
        }

        AnsiConsole.MarkupLine("Analyzing VersionTokenJsonConverter<T>: [yellow]" + classSymbol!.Name + "[/]");
        vtJsonConverters.Add(new VersionTokenJsonConverterDefinition
        {
            IsPartialClass =classSymbol.DeclaringSyntaxReferences
                .Any(reference => reference.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax typeDecl
                                  && typeDecl.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))),
            Namespace = RoslynHelper.GetFullNamespace(classSymbol),
            Name = RoslynHelper.GetFullTypeName(classSymbol),
            FileLocations = classSymbol.Locations
                .Select(l => l.SourceTree?.FilePath?.Replace(solutionRootPath, string.Empty) ?? string.Empty)
                .ToList()
        });
    }
}
