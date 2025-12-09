#pragma warning disable RS1038 // Workspaces reference - this analyzer intentionally uses Workspaces for code analysis

using System.Collections.Immutable;
using ErikLieben.FA.ES.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErikLieben.FA.ES.Analyzers;

/// <summary>
/// Roslyn analyzer that warns when a class inherits from Aggregate but is not declared partial, which is required for CLI code generation support.
/// </summary>
/// <remarks>
/// Classes deriving from ErikLieben.FA.ES.Processors.Aggregate should be declared <c>partial</c> so that tooling can augment them.
/// This analyzer reports a warning for non-partial Aggregate-derived classes to encourage correct setup.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NonPartialAggregateAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the diagnostic identifier used by this analyzer.
    /// </summary>
    public const string DiagnosticId = "FAES0003";

    private static readonly LocalizableString Title = "Aggregate-derived class should be partial";
    private static readonly LocalizableString MessageFormat = "Class '{0}' inherits from Aggregate and should be declared partial to allow CLI code generation";
    private static readonly LocalizableString Description = "Classes that inherit from ErikLieben.FA.ES.Processors.Aggregate must be declared partial so that the CLI tool can extend them.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning,
        isEnabledByDefault: true, description: Description);

    /// <summary>
    /// Gets the diagnostics descriptors produced by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>
    /// Registers analysis actions to detect Aggregate-derived classes that are not declared as partial.
    /// </summary>
    /// <param name="context">The analysis context used to register actions.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDecl)
            return;

        // Only consider non-partial classes
        if (SymbolHelpers.IsPartialClass(classDecl))
            return;

        // Resolve the symbol and check base types
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
        if (symbol is null)
            return;

        if (SymbolHelpers.IsAggregateType(symbol))
        {
            var diagnostic = Diagnostic.Create(Rule, classDecl.Identifier.GetLocation(), symbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
