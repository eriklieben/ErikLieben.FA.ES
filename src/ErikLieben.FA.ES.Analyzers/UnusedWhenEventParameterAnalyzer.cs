using System.Collections.Immutable;
using ErikLieben.FA.ES.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErikLieben.FA.ES.Analyzers;

/// <summary>
/// Roslyn analyzer that detects When methods where the event parameter is unused,
/// suggesting conversion to the [When&lt;TEvent&gt;] attribute pattern.
/// </summary>
/// <remarks>
/// When an event handler method named "When" has an event parameter that is never referenced
/// in the method body, it indicates the handler only needs to know the event occurred, not its data.
/// In such cases, the [When&lt;TEvent&gt;] attribute provides a cleaner syntax without requiring
/// an unused parameter.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnusedWhenEventParameterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the diagnostic identifier used by this analyzer.
    /// </summary>
    public const string DiagnosticId = "FAES0004";

    private static readonly LocalizableString Title = "When method has unused event parameter";
    private static readonly LocalizableString MessageFormat = "Event parameter '{0}' is not used in When method; consider using [When<{1}>] attribute instead";
    private static readonly LocalizableString Description = "When methods that don't use their event parameter can be simplified using the [When<TEvent>] attribute, which eliminates the need for an unused parameter.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info,
        isEnabledByDefault: true, description: Description);

    /// <summary>
    /// Gets the diagnostics descriptors produced by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>
    /// Registers analysis actions to detect When methods with unused event parameters.
    /// </summary>
    /// <param name="context">The analysis context used to register actions.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodDecl)
            return;

        // Only analyze methods named "When"
        if (methodDecl.Identifier.ValueText != "When")
            return;

        // Must have at least one parameter (the event)
        if (methodDecl.ParameterList.Parameters.Count == 0)
            return;

        // Check if the containing type is an Aggregate or Projection
        if (!SymbolHelpers.IsInsideAggregateOrProjection(context))
            return;

        // Already has [When<TEvent>] attribute - skip
        if (HasWhenAttribute(methodDecl))
            return;

        // Get the first parameter (event parameter)
        var eventParameter = methodDecl.ParameterList.Parameters[0];
        var parameterName = eventParameter.Identifier.ValueText;

        // Check if parameter is a discard (_) - this explicitly indicates unused
        var isDiscard = parameterName == "_";

        // If not a discard, check if the parameter is actually used in the method body
        if (!isDiscard)
        {
            if (methodDecl.Body != null && IsParameterUsedInBody(methodDecl.Body, parameterName, context.SemanticModel))
                return;

            if (methodDecl.ExpressionBody != null && IsParameterUsedInExpression(methodDecl.ExpressionBody.Expression, parameterName, context.SemanticModel))
                return;
        }

        // Get the event type name for the diagnostic message
        var eventTypeName = GetEventTypeName(eventParameter, context.SemanticModel);
        if (eventTypeName == null)
            return;

        // Report diagnostic with event parameter name and type
        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add("EventTypeName", eventTypeName);
        properties.Add("EventTypeNamespace", GetEventTypeNamespace(eventParameter, context.SemanticModel));
        properties.Add("ParameterName", parameterName);

        var diagnostic = Diagnostic.Create(
            Rule,
            eventParameter.GetLocation(),
            properties.ToImmutable(),
            parameterName,
            eventTypeName);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool HasWhenAttribute(MethodDeclarationSyntax methodDecl)
    {
        foreach (var attributeList in methodDecl.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = attribute.Name.ToString();
                if (name.StartsWith("When<") || name.StartsWith("WhenAttribute<") ||
                    name.StartsWith("ErikLieben.FA.ES.Attributes.When<") ||
                    name.StartsWith("ErikLieben.FA.ES.Attributes.WhenAttribute<"))
                    return true;
            }
        }
        return false;
    }

    private static bool IsParameterUsedInBody(BlockSyntax body, string parameterName, SemanticModel semanticModel)
    {
        foreach (var identifier in body.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (identifier.Identifier.ValueText == parameterName)
            {
                // Verify it's actually referencing the parameter, not a different symbol
                var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                if (symbol is IParameterSymbol)
                    return true;
            }
        }
        return false;
    }

    private static bool IsParameterUsedInExpression(ExpressionSyntax expression, string parameterName, SemanticModel semanticModel)
    {
        foreach (var identifier in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (identifier.Identifier.ValueText == parameterName)
            {
                var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                if (symbol is IParameterSymbol)
                    return true;
            }
        }
        return false;
    }

    private static string? GetEventTypeName(ParameterSyntax parameter, SemanticModel semanticModel)
    {
        if (parameter.Type == null)
            return null;

        var typeInfo = semanticModel.GetTypeInfo(parameter.Type);
        return typeInfo.Type?.Name;
    }

    private static string? GetEventTypeNamespace(ParameterSyntax parameter, SemanticModel semanticModel)
    {
        if (parameter.Type == null)
            return null;

        var typeInfo = semanticModel.GetTypeInfo(parameter.Type);
        return typeInfo.Type?.ContainingNamespace?.ToDisplayString();
    }
}
