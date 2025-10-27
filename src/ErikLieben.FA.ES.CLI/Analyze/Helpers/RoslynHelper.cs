using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.CLI.Analyze.Helpers;

internal class RoslynHelper(
    SemanticModel semanticModel,
    string solutionRootPath)
{
    private const string FrameworkNamespace = "ErikLieben.FA.ES";
    private const string FrameworkAttributesNamespace = "ErikLieben.FA.ES.Attributes";

    internal bool IsProcessableAggregate(INamedTypeSymbol? classSymbol)
    {
        if (!InheritsFromAggregate(classSymbol))
        {
            return false;
        }

        if (!IsInSolutionRootFolder(classSymbol))
        {
            return false;
        }

        if (IgnoreAggregate(classSymbol))
        {
            return false;
        }

        return !(classSymbol?.ContainingAssembly.Identity.Name.StartsWith(FrameworkNamespace) ?? false);
    }

    internal bool IsInheritedAggregate(INamedTypeSymbol? classSymbol)
    {
        if (!IsInSolutionRootFolder(classSymbol))
        {
            return false;
        }

        if (IgnoreAggregate(classSymbol))
        {
            return false;
        }

        if ((classSymbol?.ContainingAssembly.Identity.Name.StartsWith(FrameworkNamespace) ?? false))
        {
            return false;
        }

        if (classSymbol?.BaseType != null &&
            classSymbol.BaseType is { Name: "Aggregate" } &&
            classSymbol.BaseType.ContainingNamespace.ToDisplayString() == "ErikLieben.FA.ES.Processors")
        {
            return false;
        }

        while (classSymbol != null)
        {
            if (classSymbol.BaseType is { Name: "Aggregate" } &&
                classSymbol.BaseType.ContainingNamespace.ToDisplayString() == "ErikLieben.FA.ES.Processors")
            {
                return true;
            }
            classSymbol = classSymbol.BaseType;
        }

        return false;
    }

    internal bool IsInSolutionRootFolder(ISymbol? classSymbol)
    {
        if (classSymbol == null)
        {
            return false;
        }

        var filePaths = GetFilePaths(classSymbol);
        var filePath = filePaths.FirstOrDefault();
        if (string.IsNullOrEmpty(filePath))
        {
            return false;
        }

        return !filePath.StartsWith("..");
    }


    internal static bool IgnoreAggregate(INamedTypeSymbol? namedTypeSymbol)
    {
        if (namedTypeSymbol == null)
        {
            return false;
        }

        return namedTypeSymbol.GetAttributes()
            .Any(attribute =>
            {
                var typeSymbol = attribute.AttributeClass;
                if (typeSymbol == null) return true;
                return GetFullNamespace(typeSymbol) == FrameworkAttributesNamespace &&
                       typeSymbol.Name == "IgnoreAttribute";
            });
    }

    internal static string GetObjectName(INamedTypeSymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        var attributes = symbol.GetAttributes();
        var objectNameAttribute = attributes.FirstOrDefault(a =>
            a.AttributeClass?.Name == "ObjectNameAttribute" &&
            a.AttributeClass.ContainingNamespace.ToDisplayString()
                .Equals(FrameworkAttributesNamespace, StringComparison.Ordinal));

        if (objectNameAttribute == null)
        {
            return char.ToLower(symbol.Name[0]) + symbol.Name[1..];
        }

        var retrievedObjectName = objectNameAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
        return string.IsNullOrWhiteSpace(retrievedObjectName)
            ? char.ToLower(symbol.Name[0]) + symbol.Name[1..]
            : retrievedObjectName;
    }


    internal static bool InheritsFromAggregate(INamedTypeSymbol? type)
    {
        if (type?.BaseType == null)
        {
            return false;
        }

        return type.BaseType.Name == "Aggregate" &&
               type.BaseType.ContainingNamespace.ToDisplayString() == "ErikLieben.FA.ES.Processors";
    }


    internal static string GetIdentifierTypeFromMetadata(List<PropertyDefinition> properties)
    {
        var metadataProperty = properties
            .FirstOrDefault(p => p.Namespace == FrameworkNamespace &&
                                 p.Type.StartsWith("ObjectMetadata<"));

        return metadataProperty != null
            ? metadataProperty.Type.Replace("ObjectMetadata<", "").Replace(">", "")
            : "string";
    }


    internal static string GetEventName(ISymbol symbol)
    {
        if (symbol.Name == "IEvent")
        {
            var namedTypedSymbol = symbol as INamedTypeSymbol;
            if (namedTypedSymbol == null)
            {
                return symbol.Name;
            }
            symbol = namedTypedSymbol.TypeArguments[0];
        }

        var attribute = symbol
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "EventNameAttribute" &&
                                 GetFullNamespace(a.AttributeClass.ContainingNamespace) == FrameworkNamespace);

        if (attribute == null)
        {
            return AddPeriodsToUppercase(symbol.Name);
        }

        var argument = attribute.ConstructorArguments.FirstOrDefault();
        return argument.Value?.ToString() ?? AddPeriodsToUppercase(symbol.Name);
    }

    private static string AddPeriodsToUppercase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        Span<char> buffer = stackalloc char[input.Length * 2];
        var bufferIndex = 0;
        buffer[bufferIndex++] = input[0];
        for (var i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]) && char.IsLower(input[i - 1]))
            {
                buffer[bufferIndex++] = '.';
            }

            buffer[bufferIndex++] = input[i];
        }

        return new string(buffer[..bufferIndex]);
    }

    internal List<CommandEventDefinition>
        GetStreamContextUsagesInCommand(MethodDeclarationSyntax commandMethod)
    {
        var list = new List<CommandEventDefinition>();
        var invocationExpressions = commandMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocationExpressions)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                memberAccess.Name.Identifier.Text != "Session")
            {
                continue;
            }

            if (!IsStreamOfTypeIEventStream(memberAccess.Expression))
            {
                continue;
            }

            var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
            switch (argument?.Expression)
            {
                case SimpleLambdaExpressionSyntax lambda:
                    list.AddRange(GetCommandEventDefinitions(lambda));
                    break;
                case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                    list.AddRange(GetCommandEventDefinitions(parenthesizedLambda));
                    break;
            }
        }

        return list;
    }


    internal List<CommandEventDefinition> GetStreamContextUsagesInCommand(ISymbol symbol)
    {
        var list = new List<CommandEventDefinition>();
        foreach (var location in symbol.Locations.Where(l => l.IsInSource))
        {
            var syntaxNode = location.SourceTree?.GetRoot().FindNode(location.SourceSpan);
            if (syntaxNode == null) continue;

            var invocationExpressions = syntaxNode.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocationExpressions)
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                    memberAccess.Name.Identifier.Text != "Session")
                {
                    continue;
                }

                if (!IsStreamOfTypeIEventStream(memberAccess.Expression))
                {
                    continue;
                }

                var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
                switch (argument?.Expression)
                {
                    case SimpleLambdaExpressionSyntax lambda:
                        list.AddRange(GetCommandEventDefinitions(lambda));
                        break;
                    case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                        list.AddRange(GetCommandEventDefinitions(parenthesizedLambda));
                        break;
                }
            }
        }

        return list;
    }


    private List<CommandEventDefinition> GetCommandEventDefinitions(CSharpSyntaxNode body)
    {
        var list = new List<CommandEventDefinition>();
        var methodInvocations = body.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var methodInvocation in methodInvocations)
        {
            if (!IsAppendInvocationOnLeasedSession(methodInvocation, out var firstArgument))
            {
                continue;
            }

            if (!TryGetEventTypeInfo(firstArgument, out var symbolInfo, out var typeInfo))
            {
                continue;
            }

            list.Add(new CommandEventDefinition
            {
                EventName = RoslynHelper.GetEventName(symbolInfo.Symbol!),
                Namespace = RoslynHelper.GetFullNamespace(symbolInfo.Symbol!),
                File = GetFilePaths(symbolInfo.Symbol!).FirstOrDefault() ?? string.Empty,
                TypeName = RoslynHelper.GetFullTypeName(typeInfo.Type!),
            });
        }

        return list;
    }

    private bool IsAppendInvocationOnLeasedSession(
        InvocationExpressionSyntax methodInvocation,
        out ArgumentSyntax? firstArgument)
    {
        firstArgument = null!;

        if (methodInvocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Name.Identifier.Text != "Append")
        {
            return false;
        }

        if (!IsContextOfTypeILeasedSession(memberAccess.Expression))
        {
            return false;
        }

        var argumentList = methodInvocation.ArgumentList;
        if (argumentList == null || argumentList.Arguments.Count == 0)
        {
            return false;
        }

        firstArgument = argumentList.Arguments.First();
        return firstArgument.Expression is ObjectCreationExpressionSyntax;
    }

    private bool TryGetEventTypeInfo(
        ArgumentSyntax argument,
        out SymbolInfo symbolInfo,
        out TypeInfo typeInfo)
    {
        symbolInfo = default;
        typeInfo = default;

        if (argument.Expression is not ObjectCreationExpressionSyntax objectCreationExpression)
        {
            return false;
        }

        var typeSyntax = objectCreationExpression.Type;
        if (typeSyntax == null)
        {
            return false;
        }

        symbolInfo = semanticModel.GetSymbolInfo(typeSyntax);
        typeInfo = semanticModel.GetTypeInfo(argument.Expression);

        return symbolInfo.Symbol != null && typeInfo.Type != null;
    }

    private bool IsStreamOfTypeIEventStream(ExpressionSyntax expression)
    {
            if (expression.SyntaxTree != semanticModel.SyntaxTree)
            {
                return false;
            }

            var symbol = semanticModel.GetSymbolInfo(expression).Symbol;

            switch (symbol)
            {
                case ILocalSymbol localSymbol:
                {
                    var type = localSymbol.Type;
                    return IsSymbolOfType(type, FrameworkNamespace, "IEventStream");
                }
                case IPropertySymbol propertySymbol:
                {
                    var type = propertySymbol.Type;
                    return IsSymbolOfType(type, FrameworkNamespace, "IEventStream");
                }
                default:
                    return false;
            }
    }

    private bool IsContextOfTypeILeasedSession(ExpressionSyntax expression)
    {
        var symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is IParameterSymbol parameterSymbol)
        {
            return IsSymbolOfType(parameterSymbol.Type, FrameworkNamespace, "ILeasedSession");
        }

        return false;
    }

    private static bool IsSymbolOfType(ITypeSymbol? symbol, string @namespace, string typeName)
    {
        if (symbol == null)
        {
            return false;
        }

        return symbol.Name == typeName && symbol.ContainingNamespace.ToDisplayString() == @namespace;
    }





    internal static string GetFullTypeNameIncludingGenerics(ITypeSymbol typeSymbol)
    {
        var containingType = typeSymbol.ContainingType;

        // Resolve the containing type if the symbol is nested
        var fullName = containingType != null
            ? GetFullTypeNameIncludingGenerics(containingType) + "." + typeSymbol.Name
            : typeSymbol.Name;

        // Handle generics
        if (typeSymbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeArguments.Length > 0)
        {
            var genericArguments =
                string.Join(", ", namedTypeSymbol.TypeArguments.Select(GetFullTypeNameIncludingGenerics));
            return $"{fullName}<{genericArguments}>";
        }

        return fullName;
    }

    internal bool IsReturnTypeAwaitable(MethodDeclarationSyntax methodDeclaration)
    {
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
        var returnType = methodSymbol?.ReturnType;
        return returnType?.Name is "Task" or "ValueTask";
    }

    internal static bool IsReturnTypeAwaitable(ISymbol symbol)
    {
        var methodSymbol = symbol as IMethodSymbol;
        var returnType = methodSymbol?.ReturnType;
        return returnType?.Name is "Task" or "ValueTask";
    }


    internal static bool IsSystemNullable(ITypeSymbol? typeSymbol)
    {
        return typeSymbol?.NullableAnnotation == NullableAnnotation.Annotated &&
               typeSymbol?.OriginalDefinition.ToDisplayString() != "System.Nullable<T>";
    }

    internal static bool IsExplicitlyNullableType(ITypeSymbol? typeSymbol)
    {
        return typeSymbol?.IsReferenceType ?? false;
    }

    internal static bool IsPartial(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    internal static string GetFullNamespace(ISymbol symbol)
    {
        var currentNamespace = symbol.ContainingNamespace;
        var namespaceParts = new List<string>();
        while (currentNamespace != null && !string.IsNullOrEmpty(currentNamespace.Name))
        {
            namespaceParts.Insert(0, currentNamespace.Name);
            currentNamespace = currentNamespace.ContainingNamespace;
        }

        return string.Join(".", namespaceParts);
    }

    internal static string GetFullTypeName(ITypeSymbol typeSymbol)
    {
        var containingType = typeSymbol.ContainingType;
        if (containingType != null)
        {
            return GetFullTypeName(containingType) + "." + typeSymbol.Name;
        }

        return typeSymbol.Name;
    }

    internal List<string> GetFilePaths(ISymbol symbol)
    {
        var filePaths = new List<string>();

        var locations = symbol.Locations;
        foreach (var location in locations)
        {
            // Try to get a reasonable root path. If solutionRootPath isn't rooted on this OS (e.g., Windows-style on Linux),
            // we will fall back to substring-based relative paths below.
            var rootPathFull = SafeGetFullPath(solutionRootPath);
            var rootIsRooted = Path.IsPathRooted(rootPathFull);

            if (location.IsInSource)
            {
                var filePath = SafeGetFullPath(location.SourceTree?.FilePath ?? string.Empty);
                string rel;
                if (rootIsRooted)
                {
                    rel = Path.GetRelativePath(rootPathFull, filePath);
                }
                else
                {
                    rel = GetRelativeBySubstring(filePath, solutionRootPath);
                }
                // Always return Windows-style separators for test stability across OS
                filePaths.Add(rel.Replace('/', '\\'));
            }

            if (!location.IsInMetadata)
            {
                continue;
            }

            var metadataName = location.MetadataModule?.Name;
            if (string.IsNullOrEmpty(metadataName))
            {
                continue;
            }

            var fullMetadataPath = SafeGetFullPath(metadataName);
            string relMeta;
            if (rootIsRooted)
            {
                relMeta = Path.GetRelativePath(rootPathFull, fullMetadataPath);
            }
            else
            {
                relMeta = GetRelativeBySubstring(fullMetadataPath, solutionRootPath);
            }
            // Always return Windows-style separators for test stability across OS
            filePaths.Add(relMeta.Replace('/', '\\'));
        }

        return filePaths;
    }

    private static string SafeGetFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            // If the path is not valid for this platform, return the original normalized to current separator
            return NormalizeSeparators(path);
        }
    }

    private static string NormalizeSeparators(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var sep = Path.DirectorySeparatorChar;
        return path.Replace('\\', sep).Replace('/', sep);
    }

    private static string GetRelativeBySubstring(string filePath, string rootCandidate)
    {
        var fp = NormalizeSeparators(filePath);
        var root1 = NormalizeSeparators(rootCandidate);
        var root2 = NormalizeSeparators(rootCandidate.Replace("\\", "/"));
        var root3 = NormalizeSeparators(rootCandidate.Replace("/", "\\"));

        foreach (var root in new[] { root1, root2, root3 })
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var idx = fp.IndexOf(root, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var rel = fp[(idx + root.Length)..];
                return rel.TrimStart(Path.DirectorySeparatorChar);
            }
        }

        // As a last resort, return the file name relative part (best effort)
        return Path.GetFileName(fp);
    }

    internal static List<GenericArgument> GetGenericArguments(INamedTypeSymbol symbol)
    {
        var genericArguments = new List<GenericArgument>();
        if (symbol.TypeArguments.Length == 0)
        {
            return genericArguments;
        }

        foreach (var typeArgument in symbol.TypeArguments)
        {
            genericArguments.Add(new GenericArgument
            {
                Namespace = GetFullNamespace(typeArgument),
                Type = GetFullTypeName(typeArgument),
            });
        }

        return genericArguments;
    }
}
