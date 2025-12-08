#pragma warning disable S3776 // Cognitive Complexity - stream action analysis inherently requires complex control flow

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
        // Find all invocation expressions in the method
        var invocations = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            // Check if this is a RegisterAction call
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                continue;

            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName != "RegisterAction")
                continue;

            // Get the generic type argument
            if (memberAccess.Name is not GenericNameSyntax genericName)
                continue;

            if (genericName.TypeArgumentList.Arguments.Count == 0)
                continue;

            var typeArg = genericName.TypeArgumentList.Arguments[0];
            var typeInfo = semanticModel.GetTypeInfo(typeArg);

            if (typeInfo.Type is not INamedTypeSymbol actionType)
                continue;

            // Try to determine which aggregate this RegisterAction belongs to
            // by looking at the stream variable's type or the method context
            var aggregateName = DetermineAggregateFromContext(memberAccess, method);

            if (string.IsNullOrEmpty(aggregateName))
                continue;

            // Find the aggregate and add the stream action
            var aggregate = aggregates.FirstOrDefault(a =>
                a.IdentifierName == aggregateName ||
                a.ObjectName == aggregateName);

            if (aggregate == null)
                continue;

            // Check if this action already exists (might be registered via attribute)
            var actionTypeName = RoslynHelper.GetFullTypeName(actionType);
            var actionNamespace = RoslynHelper.GetFullNamespace(actionType);

            var existingAction = aggregate.StreamActions.FirstOrDefault(sa =>
                sa.Type == actionTypeName && sa.Namespace == actionNamespace);

            if (existingAction != null)
            {
                // Already registered (probably via attribute), skip
                continue;
            }

            // Get interfaces implemented by the action type
            var interfaces = actionType.AllInterfaces
                .Where(i => StreamInterfaces.Contains(i.Name))
                .Select(i => i.Name)
                .ToList();

            var streamAction = new StreamActionDefinition
            {
                Namespace = actionNamespace,
                Type = actionTypeName,
                StreamActionInterfaces = interfaces,
                RegistrationType = "Manual"
            };

            aggregate.StreamActions.Add(streamAction);
        }
    }

    private string? DetermineAggregateFromContext(
        MemberAccessExpressionSyntax memberAccess,
        MethodDeclarationSyntax method)
    {
        // Strategy 1: Look at the variable being called (e.g., stream.RegisterAction)
        // and trace back to find the aggregate type from the stream's generic parameter
        if (memberAccess.Expression is IdentifierNameSyntax identifier)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(identifier);
            if (symbolInfo.Symbol is ILocalSymbol localSymbol)
            {
                // Check if it's an IEventStream<TAggregate>
                if (localSymbol.Type is INamedTypeSymbol namedType &&
                    namedType.IsGenericType &&
                    namedType.Name.Contains("EventStream"))
                {
                    var typeArg = namedType.TypeArguments.FirstOrDefault();
                    if (typeArg != null)
                    {
                        return typeArg.Name;
                    }
                }
            }
            else if (symbolInfo.Symbol is IParameterSymbol paramSymbol)
            {
                // Check if the parameter type is IEventStream<TAggregate>
                if (paramSymbol.Type is INamedTypeSymbol paramType &&
                    paramType.IsGenericType &&
                    paramType.Name.Contains("EventStream") &&
                    paramType.TypeArguments.FirstOrDefault() is { } typeArg)
                {
                    return typeArg.Name;
                }
            }
        }

        // Strategy 2: Look at the method name for hints (e.g., AddOrderAggregateActions)
        var methodName = method.Identifier.Text;
        if (methodName.StartsWith("Add") && methodName.EndsWith("Actions"))
        {
            var aggregateName = methodName.Substring(3, methodName.Length - 10); // Remove "Add" and "Actions"
            return aggregateName;
        }

        // Strategy 3: Look for the aggregate type in the method's generic constraints or parameters
        foreach (var param in method.ParameterList.Parameters)
        {
            var paramType = semanticModel.GetTypeInfo(param.Type!).Type;
            if (paramType is INamedTypeSymbol namedParam &&
                namedParam.IsGenericType &&
                (namedParam.Name.Contains("EventStream") || namedParam.Name.Contains("IEventStreamBuilder")))
            {
                var typeArg = namedParam.TypeArguments.FirstOrDefault();
                if (typeArg != null)
                {
                    return typeArg.Name;
                }
            }
        }

        return null;
    }
}
