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
        // Get methods named "When"
        var whenMethods = symbol.GetMembers("When")
            .OfType<IMethodSymbol>()
            .ToList();

        // Get methods with [When<TEvent>] attribute
        var attributeBasedMethods = symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => HasWhenAttribute(m))
            .ToList();

        // Combine both approaches
        var allWhenMethods = whenMethods.Concat(attributeBasedMethods)
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<IMethodSymbol>()
            .ToList();

        var items = new List<ProjectionEventDefinition>();
        foreach (var whenMethod in allWhenMethods)
        {
            // Try to get event type from attribute first
            var eventTypeFromAttribute = GetEventTypeFromWhenAttribute(whenMethod);

            INamedTypeSymbol? parameterTypeSymbol = null;
            if (eventTypeFromAttribute != null)
            {
                // Use event type from attribute
                parameterTypeSymbol = eventTypeFromAttribute;
            }
            else
            {
                // Fall back to parameter-based detection
                var parameter = whenMethod.Parameters.FirstOrDefault();
                if (parameter?.Type is not INamedTypeSymbol typeSymbol)
                {
                    continue;
                }
                parameterTypeSymbol = typeSymbol;
            }

            if (parameterTypeSymbol.Name == "IExecutionContextWithEvent")
            {
                continue;
            }

            var eventName = RoslynHelper.GetEventName(parameterTypeSymbol);

            // Determine if first parameter is the event or if we're using attribute-based detection
            var hasEventParameter = eventTypeFromAttribute == null;
            var skipCount = hasEventParameter ? 1 : 0;
            var extra = whenMethod.Parameters.Skip(skipCount).ToList();

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
                SchemaVersion = AttributeExtractor.ExtractEventVersionAttribute(parameterTypeSymbol),
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
                        : [],
                    IsExecutionContext = IsExecutionContextType(p.Type)
                }).ToList(),
            });
        }
        return items;
    }

    /// <summary>
    /// Checks if a method has the [When&lt;TEvent&gt;] attribute.
    /// </summary>
    private static bool HasWhenAttribute(IMethodSymbol methodSymbol)
    {
        return methodSymbol.GetAttributes()
            .Any(a => a.AttributeClass != null &&
                     a.AttributeClass.OriginalDefinition.ToDisplayString() == "ErikLieben.FA.ES.Attributes.WhenAttribute<TEvent>");
    }

    /// <summary>
    /// Extracts the event type from the [When&lt;TEvent&gt;] attribute if present.
    /// </summary>
    private static INamedTypeSymbol? GetEventTypeFromWhenAttribute(IMethodSymbol methodSymbol)
    {
        var whenAttribute = methodSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass != null &&
                                a.AttributeClass.OriginalDefinition.ToDisplayString() == "ErikLieben.FA.ES.Attributes.WhenAttribute<TEvent>");

        if (whenAttribute?.AttributeClass is INamedTypeSymbol { TypeArguments.Length: 1 } namedSymbol)
        {
            return namedSymbol.TypeArguments[0] as INamedTypeSymbol;
        }

        return null;
    }



    private static readonly List<string> StreamInterfaces =
    [
        "IAsyncPostCommitAction",
        "IPostAppendAction",
        "IPostReadAction",
        "IPreAppendAction",
        "IPreReadAction"
    ];

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

    /// <summary>
    /// Checks if a type symbol is or implements IExecutionContext.
    /// </summary>
    private static bool IsExecutionContextType(ITypeSymbol typeSymbol)
    {
        // Check if it's exactly IExecutionContext or starts with IExecutionContext (covers IExecutionContextWithData etc.)
        var typeName = typeSymbol.Name;
        if (typeName.StartsWith("IExecutionContext"))
        {
            return true;
        }

        // Check if it implements IExecutionContext
        if (typeSymbol is INamedTypeSymbol namedType)
        {
            return namedType.AllInterfaces.Any(i =>
                i.Name == "IExecutionContext" ||
                i.OriginalDefinition.ToDisplayString() == "ErikLieben.FA.ES.IExecutionContext");
        }

        return false;
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
