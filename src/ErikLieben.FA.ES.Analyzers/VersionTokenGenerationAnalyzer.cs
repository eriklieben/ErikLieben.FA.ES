#pragma warning disable RS1038 // Workspaces reference - this analyzer intentionally uses Workspaces for code analysis

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErikLieben.FA.ES.Analyzers;

/// <summary>
/// Roslyn analyzer that detects when VersionToken classes are missing their generated code.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class VersionTokenGenerationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Diagnostic ID for VersionToken missing generated file.
    /// </summary>
    public const string VersionTokenMissingGeneratedFileDiagnosticId = "FAES0015";

    private static readonly LocalizableString Title = "VersionToken generated file missing";
    private static readonly LocalizableString MessageFormat = "VersionToken '{0}' requires code generation. Run 'dotnet faes' to generate supporting code.";
    private static readonly LocalizableString Description = "Classes inheriting from VersionToken<T> require generated code for constructors and extension methods. Run 'dotnet faes' to generate the supporting code.";

    private const string Category = "CodeGeneration";

    private static readonly DiagnosticDescriptor Rule = new(
        VersionTokenMissingGeneratedFileDiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    private const string VersionTokenGenericFullName = "ErikLieben.FA.ES.VersionToken<";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeRecordDeclaration, SyntaxKind.RecordDeclaration);
    }

    private static void AnalyzeRecordDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not RecordDeclarationSyntax recordDecl)
            return;

        // Skip generated files
        var filePath = recordDecl.SyntaxTree.FilePath;
        if (filePath.EndsWith(".Generated.cs"))
            return;

        // Must be partial
        if (!recordDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            return;

        var symbol = context.SemanticModel.GetDeclaredSymbol(recordDecl);
        if (symbol is null)
            return;

        // Check if it inherits from VersionToken<T>
        if (!IsVersionTokenClass(symbol))
            return;

        // Check if generated file exists
        var generatedSyntaxTree = FindGeneratedFile(context.Compilation, symbol);
        if (generatedSyntaxTree is null)
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                recordDecl.Identifier.GetLocation(),
                symbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsVersionTokenClass(INamedTypeSymbol symbol)
    {
        for (var type = symbol.BaseType; type != null; type = type.BaseType)
        {
            var typeName = type.ToDisplayString();
            if (typeName.StartsWith(VersionTokenGenericFullName))
            {
                return true;
            }
        }
        return false;
    }

    private static SyntaxTree? FindGeneratedFile(Compilation compilation, INamedTypeSymbol classSymbol)
    {
        var className = classSymbol.Name;
        var expectedFileName = $"{className}.Generated.cs";

        // Get the directory of the source file to look for the generated file nearby
        var sourceLocations = classSymbol.Locations
            .Where(l => l.IsInSource)
            .Select(l => l.SourceTree?.FilePath)
            .Where(p => p != null)
            .ToList();

        // Find matching generated file in the same directory as source
        return compilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(expectedFileName))
            .FirstOrDefault(t => sourceLocations.Any(s => s != null && AreInSameDirectory(s, t.FilePath)));
    }

    private static bool AreInSameDirectory(string path1, string path2)
    {
        var dir1 = System.IO.Path.GetDirectoryName(path1);
        var dir2 = System.IO.Path.GetDirectoryName(path2);
        return dir1 == dir2;
    }
}
