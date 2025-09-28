using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErikLieben.FA.ES.Analyzers;

/// <summary>
/// Roslyn analyzer that warns when the When(...) API is used inside a stream session in an Aggregate and suggests using Fold(...) instead.
/// </summary>
/// <remarks>
/// Within an Aggregate's Stream.Session(...), composing operations with When(...) is discouraged in favor of Fold(...),
/// which makes state application explicit and consistent. This analyzer detects When invocations in that context and
/// emits a warning with guidance to switch to Fold. The analyzer ignores usages outside Aggregates and outside a stream session.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class WhenUsageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the diagnostic identifier used by this analyzer.
    /// </summary>
    public const string DiagnosticId = "FAES0001";

    private static readonly LocalizableString Title = "Use Fold over When";
    private static readonly LocalizableString MessageFormat = "Use Fold(...) instead of When(...){0}";
    private static readonly LocalizableString Description = "The When(...) API is discouraged. Prefer Fold(...) for composing sessions.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning,
        isEnabledByDefault: true, description: Description);

    private const string AggregateFullName = "ErikLieben.FA.ES.Processors.Aggregate";
    private const string IEventStreamFullName = "ErikLieben.FA.ES.IEventStream";

    /// <summary>
    /// Gets the diagnostics descriptors produced by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <summary>
    /// Registers analysis actions to detect discouraged When(...) usage inside Aggregate stream sessions.
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

        // Find the method name for invocation, handling both simple and member access invocations.
        SimpleNameSyntax? invokedName = null;
        if (invocation.Expression is IdentifierNameSyntax id)
        {
            invokedName = id;
        }
        else if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            invokedName = memberAccess.Name;
        }

        if (invokedName == null)
            return;

        var nameText = invokedName.Identifier.ValueText;
        if (!string.Equals(nameText, "When", System.StringComparison.Ordinal))
            return;

        // Only warn when symbol resolves to a method (avoid false positive on local var named When)
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol)
            return;

        // 1) Only trigger for methods contained in a type that inherits ErikLieben.FA.ES.Processors.Aggregate
        if (!IsInsideAggregateType(context))
            return;

        // 2) Only trigger when used inside a Stream.Session(...) call
        if (!IsInsideStreamSession(context.SemanticModel, invocation))
            return;

        // Tailor message: if someone used When(...).Data() then hint about removing .Data()
        var hasDataCall = IsChainedWithData(invocation);
        var suffix = hasDataCall ? ", and remove trailing .Data() when switching to Fold" : string.Empty;

        var diagnostic = Diagnostic.Create(Rule, invokedName.GetLocation(), suffix);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsInsideAggregateType(SyntaxNodeAnalysisContext context)
    {
        // context.ContainingSymbol is usually the method/prop/etc. Get containing type and walk base types
        var containingSymbol = context.ContainingSymbol;
        var containingType = (containingSymbol as IMethodSymbol)?.ContainingType ?? containingSymbol?.ContainingType;
        if (containingType == null)
            return false;

        for (var type = containingType; type != null; type = type.BaseType)
        {
            if (type.ToDisplayString() == AggregateFullName)
                return true;
        }
        return false;
    }

    private static bool IsInsideStreamSession(SemanticModel model, InvocationExpressionSyntax whenInvocation)
    {
        // Walk up the syntax tree looking for an invocation whose target method is named "Session"
        // and whose containing type implements ErikLieben.FA.ES.IEventStream
        foreach (var ancestor in whenInvocation.Ancestors())
        {
            if (ancestor is InvocationExpressionSyntax inv)
            {
                if (inv.Expression is MemberAccessExpressionSyntax mae)
                {
                    if (mae.Name is IdentifierNameSyntax name && name.Identifier.ValueText == "Session")
                    {
                        var sessionSymbolInfo = model.GetSymbolInfo(inv);
                        if (sessionSymbolInfo.Symbol is IMethodSymbol sessionMethod)
                        {
                            // Direct on interface type
                            if (sessionMethod.ContainingType?.ToDisplayString() == IEventStreamFullName)
                                return true;

                            // Implementations of IEventStream
                            if (sessionMethod.ContainingType != null && sessionMethod.ContainingType.AllInterfaces.Any(i => i.ToDisplayString() == IEventStreamFullName))
                                return true;
                        }
                    }
                }
            }
            // stop at method boundary to avoid walking unrelated scopes
            if (ancestor is MethodDeclarationSyntax or LocalFunctionStatementSyntax)
                break;
        }
        return false;
    }

    private static bool IsChainedWithData(InvocationExpressionSyntax whenInvocation)
    {
        // Two ways .Data() can appear relative to When(...):
        // 1) Chained after When: When(...).Data()
        if (whenInvocation.Parent is MemberAccessExpressionSyntax mae && mae.Name is IdentifierNameSyntax chainedName)
        {
            if (string.Equals(chainedName.Identifier.ValueText, "Data", System.StringComparison.Ordinal)
                && mae.Parent is InvocationExpressionSyntax)
            {
                return true;
            }
        }

        // 2) Inside When argument(s): When(context.Append(...).Data())
        if (whenInvocation.ArgumentList is { Arguments.Count: > 0 } args)
        {
            foreach (var arg in args.Arguments)
            {
                // Find any invocation where the expression is a member access with name Data
                var hasDataCallInside = arg.Expression
                    .DescendantNodesAndSelf()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Any(x => x.Name is IdentifierNameSyntax n && n.Identifier.ValueText == "Data"
                              && x.Parent is InvocationExpressionSyntax);
                if (hasDataCallInside)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
