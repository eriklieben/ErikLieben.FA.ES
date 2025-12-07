using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErikLieben.FA.ES.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErikLieben.FA.ES.Analyzers;

/// <summary>
/// Roslyn analyzer that detects when Aggregates are not properly registered in the Extensions.Generated.cs file.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ExtensionsRegistrationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Diagnostic ID for aggregate not registered in Extensions.
    /// </summary>
    public const string AggregateNotRegisteredDiagnosticId = "FAES0012";

    /// <summary>
    /// Diagnostic ID for missing Extensions.Generated.cs file.
    /// </summary>
    public const string MissingExtensionsFileDiagnosticId = "FAES0014";

    private static readonly LocalizableString AggregateNotRegisteredTitle = "Aggregate not registered in Extensions";
    private static readonly LocalizableString AggregateNotRegisteredMessageFormat = "Aggregate '{0}' is not registered in Extensions. Run 'dotnet faes' to update.";
    private static readonly LocalizableString AggregateNotRegisteredDescription = "An Aggregate class exists but is not registered in the generated Extensions file. Run 'dotnet faes' to regenerate.";

    private static readonly LocalizableString MissingExtensionsTitle = "Extensions file missing";
    private static readonly LocalizableString MissingExtensionsMessageFormat = "Project contains Aggregates but no Extensions.Generated.cs file. Run 'dotnet faes' to generate.";
    private static readonly LocalizableString MissingExtensionsDescription = "A project with Aggregate classes requires an Extensions.Generated.cs file for DI registration. Run 'dotnet faes' to generate.";

    private const string Category = "CodeGeneration";

    private static readonly DiagnosticDescriptor AggregateNotRegisteredRule = new(
        AggregateNotRegisteredDiagnosticId,
        AggregateNotRegisteredTitle,
        AggregateNotRegisteredMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: AggregateNotRegisteredDescription,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    private static readonly DiagnosticDescriptor MissingExtensionsRule = new(
        MissingExtensionsFileDiagnosticId,
        MissingExtensionsTitle,
        MissingExtensionsMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: MissingExtensionsDescription,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [AggregateNotRegisteredRule, MissingExtensionsRule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        // Collect all Aggregate classes and Extensions files during compilation
        var aggregateClasses = new List<(INamedTypeSymbol Symbol, Location Location)>();
        var extensionsFiles = new List<SyntaxTree>();
        var registeredAggregates = new HashSet<string>();

        context.RegisterSyntaxNodeAction(ctx =>
        {
            if (ctx.Node is not ClassDeclarationSyntax classDecl)
                return;

            var filePath = classDecl.SyntaxTree.FilePath;

            // Check if this is an Extensions.Generated.cs file
            if (filePath.EndsWith("Extensions.Generated.cs"))
            {
                lock (extensionsFiles)
                {
                    if (!extensionsFiles.Contains(classDecl.SyntaxTree))
                    {
                        extensionsFiles.Add(classDecl.SyntaxTree);
                    }
                }
                return;
            }

            // Skip other generated files
            if (filePath.EndsWith(".Generated.cs"))
                return;

            // Check if this is an Aggregate class
            var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl);
            if (symbol is null)
                return;

            if (!SymbolHelpers.IsAggregateType(symbol))
                return;

            // Must be partial
            if (!SymbolHelpers.IsPartialClass(classDecl))
                return;

            lock (aggregateClasses)
            {
                aggregateClasses.Add((symbol, classDecl.Identifier.GetLocation()));
            }
        }, SyntaxKind.ClassDeclaration);

        context.RegisterCompilationEndAction(ctx =>
        {
            if (aggregateClasses.Count == 0)
                return; // No aggregates, nothing to check

            // Find the Extensions.Generated.cs file for this project
            var extensionsTree = FindExtensionsFile(ctx.Compilation, extensionsFiles);

            if (extensionsTree is null)
            {
                // Report FAES0014 on the first aggregate
                var firstAggregate = aggregateClasses.First();
                var diagnostic = Diagnostic.Create(
                    MissingExtensionsRule,
                    firstAggregate.Location);
                ctx.ReportDiagnostic(diagnostic);
                return;
            }

            // Parse the Extensions file to find registered aggregates
            var registeredTypes = CollectRegisteredAggregates(extensionsTree);

            // Check each aggregate to see if it's registered
            foreach (var (symbol, location) in aggregateClasses)
            {
                if (!registeredTypes.Contains(symbol.Name))
                {
                    var diagnostic = Diagnostic.Create(
                        AggregateNotRegisteredRule,
                        location,
                        symbol.Name);
                    ctx.ReportDiagnostic(diagnostic);
                }
            }
        });
    }

    private static SyntaxTree? FindExtensionsFile(Compilation compilation, List<SyntaxTree> extensionsTrees)
    {
        // Return the first Extensions.Generated.cs file found
        // In most cases there should be only one per project
        return extensionsTrees.FirstOrDefault();
    }

    /// <summary>
    /// Parses the Extensions.Generated.cs file to find which aggregates are registered.
    /// Looks for patterns like: IAggregateFactory&lt;Project, ProjectId&gt;
    /// </summary>
    private static HashSet<string> CollectRegisteredAggregates(SyntaxTree extensionsTree)
    {
        var result = new HashSet<string>();
        var root = extensionsTree.GetRoot();

        // Find the Register method
        var registerMethods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == "Register");

        foreach (var registerMethod in registerMethods)
        {
            // Find all generic type arguments that look like IAggregateFactory<AggregateType, IdType>
            var genericNames = registerMethod.DescendantNodes()
                .OfType<GenericNameSyntax>()
                .Where(g => g.Identifier.ValueText == "IAggregateFactory" &&
                           g.TypeArgumentList.Arguments.Count == 2);

            foreach (var genericName in genericNames)
            {
                // The first type argument is the aggregate type
                var aggregateType = genericName.TypeArgumentList.Arguments[0];
                if (aggregateType is IdentifierNameSyntax identifier)
                {
                    result.Add(identifier.Identifier.ValueText);
                }
                else if (aggregateType is QualifiedNameSyntax qualified)
                {
                    result.Add(qualified.Right.Identifier.ValueText);
                }
            }
        }

        // Also check the Get method switch expression for registered types
        var getMethods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == "Get");

        foreach (var getMethod in getMethods)
        {
            // Find typeof(AggregateType) patterns
            var typeofExpressions = getMethod.DescendantNodes()
                .OfType<TypeOfExpressionSyntax>();

            foreach (var typeofExpr in typeofExpressions)
            {
                if (typeofExpr.Type is IdentifierNameSyntax identifier)
                {
                    result.Add(identifier.Identifier.ValueText);
                }
                else if (typeofExpr.Type is QualifiedNameSyntax qualified)
                {
                    result.Add(qualified.Right.Identifier.ValueText);
                }
            }
        }

        return result;
    }
}
