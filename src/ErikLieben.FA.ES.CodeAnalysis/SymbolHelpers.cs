using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErikLieben.FA.ES.CodeAnalysis;

/// <summary>
/// Shared helper methods for symbol analysis used across code analysis tools.
/// </summary>
public static class SymbolHelpers
{
    /// <summary>
    /// Checks if the given type symbol inherits from Aggregate.
    /// </summary>
    public static bool IsAggregateType(INamedTypeSymbol? symbol)
    {
        if (symbol == null)
            return false;

        for (var type = symbol.BaseType; type != null; type = type.BaseType)
        {
            if (type.ToDisplayString() == TypeConstants.AggregateFullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the given type symbol inherits from Aggregate (including the type itself).
    /// </summary>
    public static bool IsOrInheritsFromAggregate(INamedTypeSymbol? symbol)
    {
        if (symbol == null)
            return false;

        for (var type = symbol; type != null; type = type.BaseType)
        {
            if (type.ToDisplayString() == TypeConstants.AggregateFullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the given type symbol inherits from Projection or RoutedProjection.
    /// </summary>
    public static bool IsProjectionType(INamedTypeSymbol? symbol)
    {
        if (symbol == null)
            return false;

        for (var type = symbol.BaseType; type != null; type = type.BaseType)
        {
            var typeName = type.ToDisplayString();
            if (typeName == TypeConstants.ProjectionFullName ||
                typeName == TypeConstants.RoutedProjectionFullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the given type symbol inherits from Aggregate, Projection, or RoutedProjection.
    /// </summary>
    public static bool IsAggregateOrProjectionType(INamedTypeSymbol? symbol)
    {
        if (symbol == null)
            return false;

        for (var type = symbol.BaseType; type != null; type = type.BaseType)
        {
            var typeName = type.ToDisplayString();
            if (typeName == TypeConstants.AggregateFullName ||
                typeName == TypeConstants.ProjectionFullName ||
                typeName == TypeConstants.RoutedProjectionFullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the containing type of the current analysis context is an Aggregate.
    /// </summary>
    public static bool IsInsideAggregateType(SyntaxNodeAnalysisContext context)
    {
        var containingType = GetContainingType(context);
        return IsOrInheritsFromAggregate(containingType);
    }

    /// <summary>
    /// Checks if the containing type of the current analysis context is an Aggregate or Projection.
    /// </summary>
    public static bool IsInsideAggregateOrProjection(SyntaxNodeAnalysisContext context)
    {
        var containingType = GetContainingType(context);
        return IsAggregateOrProjectionType(containingType);
    }

    /// <summary>
    /// Gets the containing type from an analysis context.
    /// </summary>
    public static INamedTypeSymbol? GetContainingType(SyntaxNodeAnalysisContext context)
    {
        var containingSymbol = context.ContainingSymbol;
        return (containingSymbol as IMethodSymbol)?.ContainingType ?? containingSymbol?.ContainingType;
    }

    /// <summary>
    /// Checks if a class declaration inherits from Aggregate.
    /// </summary>
    public static bool IsAggregateClass(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(classDecl);
        return IsAggregateType(symbol);
    }

    /// <summary>
    /// Checks if a type implements IEventStream.
    /// </summary>
    public static bool IsEventStreamType(ITypeSymbol? type)
    {
        if (type == null)
            return false;

        if (type.ToDisplayString() == TypeConstants.IEventStreamFullName)
            return true;

        if (type is INamedTypeSymbol namedType)
        {
            return namedType.AllInterfaces.Any(i => i.ToDisplayString() == TypeConstants.IEventStreamFullName);
        }

        return false;
    }

    /// <summary>
    /// Checks if a method symbol belongs to an IEventStream Session method.
    /// </summary>
    public static bool IsEventStreamSessionMethod(IMethodSymbol? method)
    {
        var type = method?.ContainingType;
        if (type == null)
            return false;

        if (type.ToDisplayString() == TypeConstants.IEventStreamFullName)
            return true;

        return type.AllInterfaces.Any(i => i.ToDisplayString() == TypeConstants.IEventStreamFullName);
    }

    /// <summary>
    /// Gets the relevant base type name if the symbol inherits from Aggregate, Projection, or RoutedProjection.
    /// </summary>
    public static string? GetRelevantBaseTypeName(INamedTypeSymbol? symbol)
    {
        if (symbol == null)
            return null;

        for (var type = symbol.BaseType; type != null; type = type.BaseType)
        {
            var typeName = type.ToDisplayString();
            if (typeName == TypeConstants.AggregateFullName ||
                typeName == TypeConstants.ProjectionFullName ||
                typeName == TypeConstants.RoutedProjectionFullName)
            {
                return typeName;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if the invocation is inside a Stream.Session(...) call.
    /// </summary>
    public static bool IsInsideStreamSession(SemanticModel semanticModel, SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is MethodDeclarationSyntax or LocalFunctionStatementSyntax)
                break;

            if (ancestor is InvocationExpressionSyntax invocation &&
                GetInvocationMethodName(invocation) == "Session")
            {
                var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (IsEventStreamSessionMethod(symbol))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the method name from an invocation expression.
    /// </summary>
    public static string? GetInvocationMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => name.Identifier.ValueText,
            _ => null
        };
    }

    /// <summary>
    /// Checks if a class declaration has the partial modifier.
    /// </summary>
    public static bool IsPartialClass(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    /// <summary>
    /// Checks if a file path is a generated file.
    /// </summary>
    public static bool IsGeneratedFile(string? filePath)
    {
        return filePath?.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    /// <summary>
    /// Gets the full namespace of a symbol.
    /// </summary>
    public static string GetFullNamespace(ISymbol symbol)
    {
        var ns = symbol.ContainingNamespace;
        if (ns == null || ns.IsGlobalNamespace)
            return string.Empty;

        return ns.ToDisplayString();
    }

    /// <summary>
    /// Gets the full type name including containing types (for nested types).
    /// </summary>
    public static string GetFullTypeName(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.ContainingType != null)
        {
            return $"{GetFullTypeName(typeSymbol.ContainingType)}.{typeSymbol.Name}";
        }

        return typeSymbol.Name;
    }

    /// <summary>
    /// Gets the full type name including generic type arguments.
    /// </summary>
    public static string GetFullTypeNameWithGenerics(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var baseName = GetFullTypeName(typeSymbol);
            var typeArgs = string.Join(", ", namedType.TypeArguments.Select(GetFullTypeNameWithGenerics));
            return $"{baseName}<{typeArgs}>";
        }

        return GetFullTypeName(typeSymbol);
    }
}
