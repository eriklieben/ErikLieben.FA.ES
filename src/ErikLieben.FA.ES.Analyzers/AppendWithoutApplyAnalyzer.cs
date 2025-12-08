#pragma warning disable RS1038 // Workspaces reference - this analyzer intentionally uses Workspaces for code analysis

using System.Collections.Immutable;
using ErikLieben.FA.ES.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErikLieben.FA.ES.Analyzers;

/// <summary>
/// Roslyn analyzer that warns when an event is appended within a stream session but not applied to the aggregate's active state.
/// </summary>
/// <remarks>
/// In an Aggregate's <c>Stream.Session(...)</c> block, calling <c>Append(...)</c> should be wrapped by a call that applies the
/// change to the active state (for example, <c>Fold(context.Append(...))</c> or <c>When(context.Append(...))</c>). This analyzer
/// detects bare <c>Append</c> invocations in that context and reports a warning to encourage correct usage.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AppendWithoutApplyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the diagnostic identifier used by this analyzer.
    /// </summary>
    public const string DiagnosticId = "FAES0002";

    private static readonly LocalizableString Title = "Appended event is not applied to active state";
    private static readonly LocalizableString MessageFormat = "Event is appended but not applied to the active state; wrap with Fold(context.Append(...))";
    private static readonly LocalizableString Description = "Within a Stream.Session in an Aggregate, appending an event should be applied to the aggregate's active state using Fold(context.Append(...)).";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning,
        isEnabledByDefault: true, description: Description);

    /// <summary>
    /// Gets the diagnostics supported by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>
    /// Registers actions that analyze invocation expressions to detect incorrect Append usage inside stream sessions.
    /// </summary>
    /// <param name="context">The analysis context used to register actions.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return;

        // Identify Append(...) calls
        SimpleNameSyntax? invokedName = null;
        if (invocation.Expression is IdentifierNameSyntax id)
        {
            invokedName = id;
        }
        else if (invocation.Expression is MemberAccessExpressionSyntax member)
        {
            invokedName = member.Name;
        }

        if (invokedName == null)
            return;

        if (!string.Equals(invokedName.Identifier.ValueText, "Append", StringComparison.Ordinal))
            return;

        // Ensure it's a method invocation
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol)
            return;

        // Only inside an Aggregate-derived type
        if (!SymbolHelpers.IsInsideAggregateType(context))
            return;

        // Only inside a Stream.Session(...)
        if (!SymbolHelpers.IsInsideStreamSession(context.SemanticModel, invocation))
            return;

        // Check whether this Append(...) is applied to state via Fold(...) or When(...)
        if (IsAppliedByAncestor(invocation))
            return; // good usage, no diagnostic

        // Otherwise, report
        var diagnostic = Diagnostic.Create(Rule, invokedName.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsAppliedByAncestor(InvocationExpressionSyntax appendInvocation)
    {
        // If any ancestor invocation is Fold(...) or When(...), and our append call is within its arguments, consider it applied
        foreach (var ancestor in appendInvocation.Ancestors())
        {
            if (ancestor is InvocationExpressionSyntax possibleWrapper)
            {
                var name = SymbolHelpers.GetInvocationMethodName(possibleWrapper);
                // Merge nested conditions to reduce complexity and satisfy S1066
                if (name is { } n && (n == "Fold" || n == "When") && possibleWrapper.ArgumentList != null)
                {
                    // Ensure our append invocation is somewhere inside the argument expressions of this wrapper call
                    var contains = possibleWrapper.ArgumentList.Arguments
                        .SelectMany(a => a.Expression.DescendantNodesAndSelf())
                        .Any(n2 => ReferenceEquals(n2, appendInvocation));

                    if (contains)
                        return true;
                }
            }

            // Stop at method or local function boundary
            if (ancestor is MethodDeclarationSyntax or LocalFunctionStatementSyntax)
                break;
        }
        return false;
    }
}
