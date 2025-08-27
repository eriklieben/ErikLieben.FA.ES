using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErikLieben.FA.ES.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NonPartialAggregateAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FAES0003";

    private static readonly LocalizableString Title = "Aggregate-derived class should be partial";
    private static readonly LocalizableString MessageFormat = "Class '{0}' inherits from Aggregate and should be declared partial to allow CLI code generation";
    private static readonly LocalizableString Description = "Classes that inherit from ErikLieben.FA.ES.Processors.Aggregate must be declared partial so that the CLI tool can extend them.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning,
        isEnabledByDefault: true, description: Description);

    private const string AggregateFullName = "ErikLieben.FA.ES.Processors.Aggregate";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
        if (classDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            return;

        // Resolve the symbol and check base types
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
        if (symbol is null)
            return;

        for (var type = symbol.BaseType; type != null; type = type.BaseType)
        {
            if (type.ToDisplayString() == AggregateFullName)
            {
                var diagnostic = Diagnostic.Create(Rule, classDecl.Identifier.GetLocation(), symbol.Name);
                context.ReportDiagnostic(diagnostic);
                break;
            }
        }
    }
}
