using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.CLI.Analyze.Helpers;

internal static class WhenMethodHelper
{
    internal static List<ProjectionEventDefinition> GetEventDefinitions(
        INamedTypeSymbol symbol,
        Compilation compilation,
        RoslynHelper roslyn)
    {
        var whenMethods = symbol.GetMembers("When")
            .OfType<IMethodSymbol>()
            .ToList();

        var items = new List<ProjectionEventDefinition>();
        foreach (var whenMethod in whenMethods)
        {
            var parameter = whenMethod.Parameters.FirstOrDefault();
            if (parameter?.Type is not INamedTypeSymbol parameterTypeSymbol)
            {
                continue;
            }

            if (parameterTypeSymbol.Name == "IExecutionContextWithEvent")
            {
                continue;
            }

            var eventName = RoslynHelper.GetEventName(parameterTypeSymbol);
            var extra = whenMethod.Parameters.Skip(1).ToList();

            var typeName = parameterTypeSymbol.Name;
            if (parameterTypeSymbol.TypeArguments.Length is > 0 and 1)
            {
                typeName = RoslynHelper.GetFullTypeName(parameterTypeSymbol.TypeArguments[0]);
            }

            items.Add(new ProjectionEventDefinition
            {
                ActivationType = whenMethod.Name,
                ActivationAwaitRequired = IsAwaitable(whenMethod, compilation),
                EventName = eventName,
                Namespace = RoslynHelper.GetFullNamespace(parameterTypeSymbol),
                File = roslyn.GetFilePaths(parameterTypeSymbol).FirstOrDefault() ?? string.Empty,
                TypeName = typeName,
                Properties = PropertyHelper.GetPublicGetterProperties(parameterTypeSymbol),
                Parameters = ParameterHelper.GetParameters(whenMethod),
                // StreamActions = GetStreamActions(parameterTypeSymbol),
                WhenParameterValueFactories = GetWhenParameterValueFactories(whenMethod),
                WhenParameterDeclarations = extra.Select(p => new WhenParameterDeclaration
                {
                    Name = p.Name,
                    Type = RoslynHelper.GetFullTypeName(p.Type),
                    Namespace = RoslynHelper.GetFullNamespace(p.Type),
                    GenericArguments = p.Type is INamedTypeSymbol namedType
                        ? RoslynHelper.GetGenericArguments(namedType)
                        : []
                }).ToList(),
            });
        }
        return items;
    }



    private static readonly List<string> StreamInterfaces =
    [
        "IAsyncPostCommitAction",
        "IPostAppendAction",
        "IPostReadAction",
        "IPreAppendAction",
        "IPreReadAction"
    ];

    private static List<StreamActionDefinition> GetStreamActions(INamedTypeSymbol parameterTypeSymbol)
    {
        return parameterTypeSymbol.GetAttributes()
            .Where(a => a.AttributeClass is { TypeArguments.Length: > 0 })
            .SelectMany(attribute => attribute.AttributeClass!.TypeArguments)
            .Where(typeArgument => typeArgument.TypeKind != TypeKind.Error)
            .Select(typeArgument => new StreamActionDefinition
            {
                Namespace = RoslynHelper.GetFullNamespace(typeArgument),
                Type = RoslynHelper.GetFullTypeName(typeArgument),
                StreamActionInterfaces = typeArgument.AllInterfaces
                    .Where(i => StreamInterfaces.Contains(i.Name))
                    .Select(i => i.Name)
                    .ToList()
            })
            .ToList();
    }


    private static List<WhenParameterValueFactory> GetWhenParameterValueFactories(IMethodSymbol methodSymbol)
     {
         return methodSymbol.GetAttributes()
             .Where(a => a.AttributeClass != null &&
                         a.AttributeClass.OriginalDefinition.ToDisplayString() == "ErikLieben.FA.ES.Attributes.WhenParameterValueFactoryAttribute<T>" &&
                         a.AttributeClass is INamedTypeSymbol { TypeArguments.Length: 1 })
             .Select(attributeData =>
             {
                 var namedSymbol = attributeData.AttributeClass!;
                 var genericArgument = namedSymbol.TypeArguments[0];
                 var typeName = genericArgument.ToDisplayString();
                 var typeSymbol = methodSymbol.ContainingAssembly.GetTypeByMetadataName(typeName);
                 return (typeName, typeSymbol);
             })
             .Where(x => x.typeSymbol != null)
             .SelectMany(x => x.typeSymbol!.AllInterfaces
                 .Where(i =>
                     i.OriginalDefinition.ToDisplayString() == "ErikLieben.FA.ES.Projections.IProjectionWhenParameterValueFactory<TValue, TEventType>" ||
                     i.OriginalDefinition.ToDisplayString() == "ErikLieben.FA.ES.Projections.IProjectionWhenParameterValueFactory<TValue>")
                 .Select(i => new WhenParameterValueFactory
                 {
                     Type = new WhenParameterValueItem
                     {
                         Type = x.typeName,
                         Namespace = string.Empty,
                     },
                     ForType = new WhenParameterValueItem
                     {
                         Type = i.TypeArguments[0].ToDisplayString(),
                         Namespace = RoslynHelper.GetFullNamespace(i.TypeArguments[0])
                     },
                     EventType = i.OriginalDefinition.ToDisplayString() ==
                         "ErikLieben.FA.ES.Projections.IProjectionWhenParameterValueFactory<TValue, TEventType>"
                         ? i.TypeArguments[1].ToDisplayString()
                         : null
                 }))
             .ToList();
     }

    private static bool IsAwaitable(IMethodSymbol methodSymbol, Compilation compilation)
    {
        // Get well-known types for Task and ValueTask
        var taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        var taskOfTType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        var valueTaskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
        var valueTaskOfTType = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");

        // Check if the return type matches Task/ValueTask or generic variants
        if (methodSymbol.ReturnType.Equals(taskType, SymbolEqualityComparer.Default) ||
            methodSymbol.ReturnType.OriginalDefinition.Equals(taskOfTType, SymbolEqualityComparer.Default) ||
            methodSymbol.ReturnType.Equals(valueTaskType, SymbolEqualityComparer.Default) ||
            methodSymbol.ReturnType.OriginalDefinition.Equals(valueTaskOfTType, SymbolEqualityComparer.Default))
        {
            return true;
        }

        // Check for custom awaitable types
        var getAwaiterMethod = methodSymbol.ReturnType.GetMembers("GetAwaiter").FirstOrDefault();
        if (getAwaiterMethod is not IMethodSymbol awaiterMethod ||
            !awaiterMethod.Parameters.IsEmpty ||
            awaiterMethod.ReturnType is not { } awaiterReturnType)
        {
            return false;
        }

        var inotifyCompletion =
            compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.INotifyCompletion");

        return inotifyCompletion != null && awaiterReturnType.AllInterfaces.Contains(inotifyCompletion, SymbolEqualityComparer.Default);
    }

}
