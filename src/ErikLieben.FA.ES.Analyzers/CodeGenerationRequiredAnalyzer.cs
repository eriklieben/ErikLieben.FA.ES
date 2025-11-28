using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErikLieben.FA.ES.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErikLieben.FA.ES.Analyzers;

/// <summary>
/// Roslyn analyzer that detects when code generation needs to be re-run via 'dotnet faes'.
/// Compares When methods in source files against the generated Fold switch cases.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CodeGenerationRequiredAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Diagnostic ID for missing generated file.
    /// </summary>
    public const string MissingGeneratedFileDiagnosticId = "FAES0005";

    /// <summary>
    /// Diagnostic ID for stale generated code (When method not in Fold).
    /// </summary>
    public const string StaleGeneratedCodeDiagnosticId = "FAES0006";

    /// <summary>
    /// Diagnostic ID for property not in generated interface.
    /// </summary>
    public const string PropertyNotInGeneratedInterfaceDiagnosticId = "FAES0007";

    private static readonly LocalizableString MissingFileTitle = "Generated file missing";
    private static readonly LocalizableString MissingFileMessageFormat = "Class '{0}' requires code generation. Run 'dotnet faes' to generate supporting code.";
    private static readonly LocalizableString MissingFileDescription = "Classes inheriting from Aggregate or Projection require generated code. Run 'dotnet faes' to generate the supporting code.";

    private static readonly LocalizableString StaleCodeTitle = "Generated code is out of date";
    private static readonly LocalizableString StaleCodeMessageFormat = "Event handler '{0}' for '{1}' is not in generated code. Run 'dotnet faes' to update.";
    private static readonly LocalizableString StaleCodeDescription = "A When method or [When<T>] attribute was added but the generated Fold method doesn't include it. Run 'dotnet faes' to regenerate.";

    private static readonly LocalizableString PropertyNotInInterfaceTitle = "Property not in generated interface";
    private static readonly LocalizableString PropertyNotInInterfaceMessageFormat = "Property '{0}' is not in generated interface 'I{1}'. Run 'dotnet faes' to update.";
    private static readonly LocalizableString PropertyNotInInterfaceDescription = "A public property was added but the generated interface doesn't include it. Run 'dotnet faes' to regenerate.";

    private const string Category = "CodeGeneration";

    private static readonly DiagnosticDescriptor MissingFileRule = new(
        MissingGeneratedFileDiagnosticId,
        MissingFileTitle,
        MissingFileMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: MissingFileDescription);

    private static readonly DiagnosticDescriptor StaleCodeRule = new(
        StaleGeneratedCodeDiagnosticId,
        StaleCodeTitle,
        StaleCodeMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: StaleCodeDescription);

    private static readonly DiagnosticDescriptor PropertyNotInInterfaceRule = new(
        PropertyNotInGeneratedInterfaceDiagnosticId,
        PropertyNotInInterfaceTitle,
        PropertyNotInInterfaceMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: PropertyNotInInterfaceDescription);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MissingFileRule, StaleCodeRule, PropertyNotInInterfaceRule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDecl)
            return;

        // Skip generated files
        var filePath = classDecl.SyntaxTree.FilePath;
        if (SymbolHelpers.IsGeneratedFile(filePath))
            return;

        // Must be partial (non-partial classes are caught by FAES0003)
        if (!SymbolHelpers.IsPartialClass(classDecl))
            return;

        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
        if (symbol is null)
            return;

        var baseTypeName = SymbolHelpers.GetRelevantBaseTypeName(symbol);
        if (baseTypeName is null)
            return;

        // Collect When methods and [When<T>] attributes from the source class
        var sourceEventTypes = CollectSourceEventTypes(classDecl, context.SemanticModel);

        // Collect public properties from the source class
        var sourceProperties = CollectSourceProperties(classDecl);

        // If no When handlers and no properties, nothing to check
        if (sourceEventTypes.Count == 0 && sourceProperties.Count == 0)
            return;

        // Find the corresponding .Generated.cs file in the compilation
        var generatedSyntaxTree = FindGeneratedFile(context.Compilation, symbol);
        if (generatedSyntaxTree is null)
        {
            // Generated file doesn't exist - only report if there are When methods
            // (properties alone don't require generated code)
            if (sourceEventTypes.Count > 0)
            {
                var diagnostic = Diagnostic.Create(
                    MissingFileRule,
                    classDecl.Identifier.GetLocation(),
                    symbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
            return;
        }

        // Check When methods
        if (sourceEventTypes.Count > 0)
        {
            // Parse the Fold method in the generated file to get handled event types
            var generatedEventTypes = CollectGeneratedEventTypes(generatedSyntaxTree, symbol.Name);

            // Find When methods that are NOT in the generated Fold
            foreach (var (eventTypeName, location) in sourceEventTypes)
            {
                if (!generatedEventTypes.Contains(eventTypeName))
                {
                    var diagnostic = Diagnostic.Create(
                        StaleCodeRule,
                        location,
                        "When",
                        eventTypeName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // Check properties (FAES0007)
        if (sourceProperties.Count > 0)
        {
            var generatedInterfaceProperties = CollectGeneratedInterfaceProperties(generatedSyntaxTree, symbol.Name);

            foreach (var (propertyName, location) in sourceProperties)
            {
                if (!generatedInterfaceProperties.Contains(propertyName))
                {
                    var diagnostic = Diagnostic.Create(
                        PropertyNotInInterfaceRule,
                        location,
                        propertyName,
                        symbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static List<(string EventTypeName, Location Location)> CollectSourceEventTypes(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel)
    {
        var result = new List<(string, Location)>();

        foreach (var member in classDecl.Members)
        {
            if (member is not MethodDeclarationSyntax methodDecl)
                continue;

            // Check for When(EventType evt) method pattern
            if (methodDecl.Identifier.ValueText == "When" &&
                methodDecl.ParameterList.Parameters.Count >= 1)
            {
                var firstParam = methodDecl.ParameterList.Parameters[0];
                if (firstParam.Type != null)
                {
                    var typeInfo = semanticModel.GetTypeInfo(firstParam.Type);
                    if (typeInfo.Type != null)
                    {
                        var eventTypeName = typeInfo.Type.Name;
                        result.Add((eventTypeName, methodDecl.Identifier.GetLocation()));
                    }
                }
            }

            // Check for [When<EventType>] attribute pattern
            foreach (var attributeList in methodDecl.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var attrName = attribute.Name.ToString();
                    if (attrName.StartsWith("When<") || attrName.StartsWith("WhenAttribute<"))
                    {
                        // Extract type from generic attribute
                        var attrSymbol = semanticModel.GetSymbolInfo(attribute).Symbol;
                        if (attrSymbol?.ContainingType is INamedTypeSymbol { TypeArguments.Length: 1 } namedType)
                        {
                            var eventType = namedType.TypeArguments[0];
                            result.Add((eventType.Name, attribute.GetLocation()));
                        }
                    }
                }
            }
        }

        return result;
    }

    private static SyntaxTree? FindGeneratedFile(Compilation compilation, INamedTypeSymbol classSymbol)
    {
        var className = classSymbol.Name;
        var expectedFileName = $"{className}.Generated.cs";

        // Get the directory of the source file to look for the generated file nearby
        var sourceLocations = classSymbol.Locations
            .Where(l => l.IsInSource)
            .Select(l => l.SourceTree?.FilePath)
            .Where(p => p != null)
            .ToList();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var treePath = syntaxTree.FilePath;
            if (treePath.EndsWith(expectedFileName))
            {
                // Verify it's in a similar directory (same project)
                foreach (var sourcePath in sourceLocations)
                {
                    if (sourcePath != null && AreInSameDirectory(sourcePath, treePath))
                    {
                        return syntaxTree;
                    }
                }
            }
        }

        return null;
    }

    private static bool AreInSameDirectory(string path1, string path2)
    {
        var dir1 = System.IO.Path.GetDirectoryName(path1);
        var dir2 = System.IO.Path.GetDirectoryName(path2);
        return dir1 == dir2;
    }

    private static HashSet<string> CollectGeneratedEventTypes(SyntaxTree generatedTree, string className)
    {
        var result = new HashSet<string>();
        var root = generatedTree.GetRoot();

        // Find the Fold method in the generated partial class
        var classDeclarations = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.Identifier.ValueText == className);

        foreach (var classDecl in classDeclarations)
        {
            // Look for Fold method (Aggregates, Projections) or DispatchToWhen (RoutedProjections)
            var foldMethod = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == "Fold" ||
                                     m.Identifier.ValueText == "DispatchToWhen");

            if (foldMethod?.Body == null)
                continue;

            // Find all switch sections in the Fold method
            var switchStatements = foldMethod.Body.DescendantNodes()
                .OfType<SwitchStatementSyntax>();

            foreach (var switchStmt in switchStatements)
            {
                foreach (var section in switchStmt.Sections)
                {
                    // Look for When invocations or WhenXxx method calls in the case body
                    foreach (var invocation in section.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        var eventTypeName = ExtractEventTypeFromInvocation(invocation);
                        if (eventTypeName != null)
                        {
                            result.Add(eventTypeName);
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts the event type name from an invocation in the generated Fold method.
    /// Handles patterns like:
    /// - When(JsonEvent.To(@event, ProjectInitiatedJsonSerializerContext.Default.ProjectInitiated))
    /// - When(JsonEvent.ToEvent(@event, Ctx.Default.EventType).Data(), ...)  (for Projections)
    /// - WhenProjectCompleted()
    /// </summary>
    private static string? ExtractEventTypeFromInvocation(InvocationExpressionSyntax invocation)
    {
        string? methodName = null;

        // Get the method name
        if (invocation.Expression is IdentifierNameSyntax identifier)
        {
            methodName = identifier.Identifier.ValueText;
        }
        else if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            methodName = memberAccess.Name.Identifier.ValueText;
        }

        if (methodName == null)
            return null;

        // Pattern 1: WhenEventName() - extract EventName from method name
        if (methodName.StartsWith("When") && methodName.Length > 4)
        {
            return methodName.Substring(4); // Remove "When" prefix
        }

        // Pattern 2: When(JsonEvent.To/@event, XxxJsonSerializerContext.Default.Xxx))
        // Pattern 3: When(JsonEvent.ToEvent(@event, Ctx.Default.Xxx).Data(), ...)
        if (methodName == "When" && invocation.ArgumentList.Arguments.Count >= 1)
        {
            var firstArg = invocation.ArgumentList.Arguments[0].Expression;

            // Handle chained call: JsonEvent.ToEvent(...).Data()
            if (firstArg is InvocationExpressionSyntax dataInvocation &&
                dataInvocation.Expression is MemberAccessExpressionSyntax dataAccess &&
                dataAccess.Name.Identifier.ValueText == "Data")
            {
                // Get the inner JsonEvent.ToEvent(...) call
                firstArg = dataAccess.Expression;
            }

            // The argument could be JsonEvent.To(...) or JsonEvent.ToEvent(...)
            if (firstArg is InvocationExpressionSyntax jsonEventInvocation &&
                jsonEventInvocation.ArgumentList.Arguments.Count >= 2)
            {
                // Get the second argument which is like: XxxJsonSerializerContext.Default.Xxx
                var serializerArg = jsonEventInvocation.ArgumentList.Arguments[1].Expression;
                if (serializerArg is MemberAccessExpressionSyntax serializerAccess)
                {
                    // The event type name is the final member name
                    return serializerAccess.Name.Identifier.ValueText;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Collects public properties from the source class that should be in the generated interface.
    /// Only properties with public getters are included (as the interface only exposes getters).
    /// </summary>
    private static List<(string PropertyName, Location Location)> CollectSourceProperties(ClassDeclarationSyntax classDecl)
    {
        var result = new List<(string, Location)>();

        foreach (var member in classDecl.Members)
        {
            if (member is not PropertyDeclarationSyntax propertyDecl)
                continue;

            // Must have public modifier (explicit or implicit via interface implementation)
            var isPublic = propertyDecl.Modifiers.Any(SyntaxKind.PublicKeyword);
            if (!isPublic)
                continue;

            // Must have a getter
            var hasGetter = propertyDecl.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) ?? false;
            var hasExpressionBody = propertyDecl.ExpressionBody != null;
            if (!hasGetter && !hasExpressionBody)
                continue;

            // Skip static properties
            if (propertyDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
                continue;

            result.Add((propertyDecl.Identifier.ValueText, propertyDecl.Identifier.GetLocation()));
        }

        return result;
    }

    /// <summary>
    /// Collects property names from the generated interface (I{ClassName}).
    /// </summary>
    private static HashSet<string> CollectGeneratedInterfaceProperties(SyntaxTree generatedTree, string className)
    {
        var result = new HashSet<string>();
        var root = generatedTree.GetRoot();

        // Find the generated interface (I{ClassName})
        var interfaceName = $"I{className}";
        var interfaceDeclarations = root.DescendantNodes()
            .OfType<InterfaceDeclarationSyntax>()
            .Where(i => i.Identifier.ValueText == interfaceName);

        foreach (var interfaceDecl in interfaceDeclarations)
        {
            foreach (var member in interfaceDecl.Members)
            {
                if (member is PropertyDeclarationSyntax propertyDecl)
                {
                    result.Add(propertyDecl.Identifier.ValueText);
                }
            }
        }

        return result;
    }
}
