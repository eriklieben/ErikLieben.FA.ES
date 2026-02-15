using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.CLI.Analyze;

/// <summary>
/// Analyzes manual stream.RegisterAction&lt;T&gt;() calls in extension methods.
/// </summary>
public class AnalyzeManualStreamActions(
    ClassDeclarationSyntax classDeclaration,
    SemanticModel semanticModel)
{
    private static readonly string[] StreamInterfaces =
    [
        "IAsyncPostCommitAction",
        "IPostAppendAction",
        "IPostReadAction",
        "IPreAppendAction",
        "IPreReadAction"
    ];

    /// <summary>
    /// Analyzes the class for manual RegisterAction calls and adds them to the appropriate aggregate.
    /// </summary>
    public void Run(List<AggregateDefinition> aggregates)
    {
        // Look for extension methods that might contain RegisterAction calls
        var methods = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(mod => mod.Text == "static"));

        foreach (var method in methods)
        {
            AnalyzeMethodForRegisterActionCalls(method, aggregates);
        }
    }

    private void AnalyzeMethodForRegisterActionCalls(MethodDeclarationSyntax method, List<AggregateDefinition> aggregates)
    {
        var invocations = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                continue;

            if (memberAccess.Name.Identifier.Text != "RegisterAction")
                continue;

            if (memberAccess.Name is not GenericNameSyntax { TypeArgumentList.Arguments.Count: > 0 } genericName)
                continue;

            var actionType = ResolveActionType(genericName);
            if (actionType == null)
                continue;

            var aggregate = FindTargetAggregate(memberAccess, method, aggregates);
            if (aggregate == null)
                continue;

            TryAddStreamAction(actionType, aggregate);
        }
    }

    private INamedTypeSymbol? ResolveActionType(GenericNameSyntax genericName)
    {
        var typeArg = genericName.TypeArgumentList.Arguments[0];
        var typeInfo = semanticModel.GetTypeInfo(typeArg);
        return typeInfo.Type as INamedTypeSymbol;
    }

    private AggregateDefinition? FindTargetAggregate(
        MemberAccessExpressionSyntax memberAccess,
        MethodDeclarationSyntax method,
        List<AggregateDefinition> aggregates)
    {
        var aggregateName = DetermineAggregateFromContext(memberAccess, method);
        if (string.IsNullOrEmpty(aggregateName))
            return null;

        return aggregates.FirstOrDefault(a =>
            a.IdentifierName == aggregateName ||
            a.ObjectName == aggregateName);
    }

    private static void TryAddStreamAction(INamedTypeSymbol actionType, AggregateDefinition aggregate)
    {
        var actionTypeName = RoslynHelper.GetFullTypeName(actionType);
        var actionNamespace = RoslynHelper.GetFullNamespace(actionType);

        var existingAction = aggregate.StreamActions.FirstOrDefault(sa =>
            sa.Type == actionTypeName && sa.Namespace == actionNamespace);

        if (existingAction != null)
            return;

        var interfaces = actionType.AllInterfaces
            .Where(i => StreamInterfaces.Contains(i.Name))
            .Select(i => i.Name)
            .ToList();

        aggregate.StreamActions.Add(new StreamActionDefinition
        {
            Namespace = actionNamespace,
            Type = actionTypeName,
            StreamActionInterfaces = interfaces,
            RegistrationType = "Manual"
        });
    }

    private string? DetermineAggregateFromContext(
        MemberAccessExpressionSyntax memberAccess,
        MethodDeclarationSyntax method)
    {
        return DetermineAggregateFromStreamVariable(memberAccess)
            ?? DetermineAggregateFromMethodName(method)
            ?? DetermineAggregateFromMethodParameters(method);
    }

    /// <summary>
    /// Strategy 1: Look at the variable being called (e.g., stream.RegisterAction)
    /// and trace back to find the aggregate type from the stream's generic parameter.
    /// </summary>
    private string? DetermineAggregateFromStreamVariable(MemberAccessExpressionSyntax memberAccess)
    {
        if (memberAccess.Expression is not IdentifierNameSyntax identifier)
            return null;

        var symbolInfo = semanticModel.GetSymbolInfo(identifier);
        var symbolType = GetSymbolType(symbolInfo.Symbol);

        return ExtractAggregateNameFromEventStreamType(symbolType);
    }

    private static ITypeSymbol? GetSymbolType(ISymbol? symbol)
    {
        return symbol switch
        {
            ILocalSymbol localSymbol => localSymbol.Type,
            IParameterSymbol paramSymbol => paramSymbol.Type,
            _ => null
        };
    }

    private static string? ExtractAggregateNameFromEventStreamType(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol { IsGenericType: true } namedType)
            return null;

        if (!namedType.Name.Contains("EventStream"))
            return null;

        return namedType.TypeArguments.FirstOrDefault()?.Name;
    }

    /// <summary>
    /// Strategy 2: Look at the method name for hints (e.g., AddOrderAggregateActions).
    /// </summary>
    private static string? DetermineAggregateFromMethodName(MethodDeclarationSyntax method)
    {
        var methodName = method.Identifier.Text;
        if (!methodName.StartsWith("Add") || !methodName.EndsWith("Actions"))
            return null;

        return methodName.Substring(3, methodName.Length - 10);
    }

    /// <summary>
    /// Strategy 3: Look for the aggregate type in the method's generic constraints or parameters.
    /// </summary>
    private string? DetermineAggregateFromMethodParameters(MethodDeclarationSyntax method)
    {
        foreach (var param in method.ParameterList.Parameters)
        {
            var paramType = semanticModel.GetTypeInfo(param.Type!).Type;
            var aggregateName = ExtractAggregateNameFromStreamBuilderType(paramType);
            if (aggregateName != null)
                return aggregateName;
        }

        return null;
    }

    private static string? ExtractAggregateNameFromStreamBuilderType(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol { IsGenericType: true } namedParam)
            return null;

        if (!namedParam.Name.Contains("EventStream") && !namedParam.Name.Contains("IEventStreamBuilder"))
            return null;

        return namedParam.TypeArguments.FirstOrDefault()?.Name;
    }
}
