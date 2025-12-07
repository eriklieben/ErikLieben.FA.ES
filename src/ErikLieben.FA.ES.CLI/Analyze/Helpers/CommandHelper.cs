using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;

namespace ErikLieben.FA.ES.CLI.Analyze.Helpers;

internal static class CommandHelper
{
    internal static List<CommandDefinition> GetCommandMethods(ITypeSymbol typeSymbol, RoslynHelper roslyn)
    {
        var commandDefinitions = new List<CommandDefinition>();

        var commandMethods = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(md => md.DeclaredAccessibility == Accessibility.Public && md.Name != "When" && md.Name != ".ctor");

        foreach (var commandMethod in commandMethods)
        {
            var commandDefinition = new CommandDefinition
            {
                CommandName = commandMethod.Name,
                Parameters = GetMethodParameters(commandMethod),
                ReturnType = GetMethodReturnType(commandMethod),
                RequiresAwait = RoslynHelper.IsReturnTypeAwaitable(commandMethod)
            };

            var list = roslyn.GetStreamContextUsagesInCommand(commandMethod);
            if (list.Count == 0) {
                continue;
            }

            commandDefinition.ProducesEvents.AddRange(list);
            commandDefinitions.Add(commandDefinition);
        }

        return commandDefinitions;
    }

    private static List<CommandParameter> GetMethodParameters(IMethodSymbol symbol)
    {
        var parameters = new List<CommandParameter>();
        foreach (var parameterSymbol in symbol.Parameters)
        {
            var name = parameterSymbol.Name;
            var typeSymbol = parameterSymbol.Type;
            var typeName = typeSymbol != null ? RoslynHelper.GetFullTypeNameIncludingGenerics(typeSymbol) : string.Empty;
            var fullNamespace = RoslynHelper.GetFullNamespace(typeSymbol!);
            var genericTypes = typeSymbol is INamedTypeSymbol { IsGenericType: true } namedType
                ? namedType.TypeArguments
                    .Where(t => t.TypeKind != TypeKind.Error)
                    .Select(t => new PropertyGenericTypeDefinition(
                        RoslynHelper.GetFullTypeName(t),
                        RoslynHelper.GetFullNamespace(t), [], []))
                    .ToList()
                : null;

            parameters.Add(new CommandParameter
            {
                Name = name,
                Type = typeName,
                Namespace = fullNamespace,
                GenericTypes = genericTypes!,
                IsGeneric = genericTypes?.Count != 0
            });
        }

        return parameters;
    }

    private static CommandReturnType GetMethodReturnType(IMethodSymbol methodSymbol)
    {
        // Get the return type
        var returnType = methodSymbol.ReturnType;

        // Create the CommandReturnType
        return new CommandReturnType
        {
            Namespace = RoslynHelper.GetFullNamespace(returnType),
            Type = returnType.Name,
        };
    }
}
