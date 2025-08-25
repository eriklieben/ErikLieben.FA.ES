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
                    GenericArguments = RoslynHelper.GetGenericArguments(p.Type as INamedTypeSymbol)
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
        var streamActions = new List<StreamActionDefinition>();

        var attributes = parameterTypeSymbol.GetAttributes();
        foreach (var attribute in attributes)
        {
            var attributeClassType = attribute.AttributeClass;
            if (attributeClassType is { TypeArguments.Length: > 0 })
            {
                foreach (var typeArgument in attributeClassType.TypeArguments)
                {
                    var typeArgumentType = typeArgument.TypeKind;
                    if (typeArgumentType == TypeKind.Error)
                    {
                        continue;
                    }

                    streamActions.Add(new StreamActionDefinition
                    {
                        Namespace = RoslynHelper.GetFullNamespace(typeArgument),
                        Type = RoslynHelper.GetFullTypeName(typeArgument),
                        StreamActionInterfaces = typeArgument.AllInterfaces
                            .Where(i => StreamInterfaces.Contains(i.Name))
                            .Select(i => i.Name)
                            .ToList()
                    });
                }
            }
        }

        return streamActions;
    }


    private static List<WhenParameterValueFactory> GetWhenParameterValueFactories(IMethodSymbol methodSymbol)
     {
         var whenParameterValueFactories = new List<WhenParameterValueFactory>();

         foreach (var attributeData in methodSymbol.GetAttributes())
         {
             var attributeType = attributeData.AttributeClass;
             if (attributeType == null)
             {
                 continue;
             }

             if (attributeType.OriginalDefinition.ToDisplayString() !=
                 "ErikLieben.FA.ES.Attributes.WhenParameterValueFactoryAttribute<T>")
             {
                 continue;
             }

             if (attributeType is not INamedTypeSymbol { TypeArguments.Length: 1 } namedSymbol)
             {
                 continue;
             }

             var genericArgument = namedSymbol.TypeArguments[0];
             var typeName = genericArgument.ToDisplayString();
             var typeSymbol = methodSymbol.ContainingAssembly.GetTypeByMetadataName(typeName);

             if (typeSymbol == null)
             {
                 continue;
             }

             foreach (var implementedInterface in typeSymbol.AllInterfaces)
             {
                 if (implementedInterface.OriginalDefinition.ToDisplayString() ==
                     "ErikLieben.FA.ES.Projections.IProjectionWhenParameterValueFactory<TValue, TEventType>")
                 {
                     whenParameterValueFactories.Add(new WhenParameterValueFactory
                     {
                         Type = new WhenParameterValueItem()
                         {
                             Type  = typeName,
                             Namespace = string.Empty,
                         },
                         ForType = new WhenParameterValueItem
                         {
                             Type = implementedInterface.TypeArguments[0].ToDisplayString(),
                             Namespace = RoslynHelper.GetFullNamespace(implementedInterface.TypeArguments[0])
                         },

                         EventType = implementedInterface.TypeArguments[1].ToDisplayString()
                     });
                 }
                 else if (implementedInterface.OriginalDefinition.ToDisplayString() ==
                          "ErikLieben.FA.ES.Projections.IProjectionWhenParameterValueFactory<TValue>")
                 {
                     whenParameterValueFactories.Add(new WhenParameterValueFactory
                     {
                         Type = new WhenParameterValueItem()
                         {
                             Type = typeName,
                             Namespace = string.Empty,
                         },
                         ForType = new WhenParameterValueItem
                         {
                             Type = implementedInterface.TypeArguments[0].ToDisplayString(),
                             Namespace = RoslynHelper.GetFullNamespace(implementedInterface.TypeArguments[0])
                         },
                     });
                 }
             }
         }

         return whenParameterValueFactories;
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
