#pragma warning disable RS1038 // Workspaces reference - this analyzer intentionally uses Workspaces for cross-file analysis

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

        // Collect When(EventType evt) method patterns
        var whenMethods = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == "When" && m.ParameterList.Parameters.Count >= 1)
            .Select(m => (Method: m, FirstParam: m.ParameterList.Parameters[0]))
            .Where(x => x.FirstParam.Type != null)
            .Select(x => (x.Method, TypeInfo: semanticModel.GetTypeInfo(x.FirstParam.Type!)))
            .Where(x => x.TypeInfo.Type != null);

        result.AddRange(whenMethods.Select(x =>
            (x.TypeInfo.Type!.Name, x.Method.Identifier.GetLocation())));

        // Collect [When<EventType>] attribute patterns using SelectMany
        var whenAttributes = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .SelectMany(m => m.AttributeLists.SelectMany(al => al.Attributes))
            .Where(a => a.Name.ToString().StartsWith("When<") ||
                       a.Name.ToString().StartsWith("WhenAttribute<"))
            .Select(a => (Attribute: a, Symbol: semanticModel.GetSymbolInfo(a).Symbol))
            .Where(x => x.Symbol?.ContainingType is INamedTypeSymbol { TypeArguments.Length: 1 })
            .Select(x => (x.Attribute, EventType: (x.Symbol!.ContainingType as INamedTypeSymbol)!.TypeArguments[0]));

        result.AddRange(whenAttributes.Select(x =>
            (x.EventType.Name, x.Attribute.GetLocation())));

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

        return compilation.SyntaxTrees
            .Where(syntaxTree => syntaxTree.FilePath.EndsWith(expectedFileName))
            .FirstOrDefault(syntaxTree => sourceLocations
                .Where(sourcePath => sourcePath != null)
                .Any(sourcePath => AreInSameDirectory(sourcePath!, syntaxTree.FilePath)));
    }

    private static bool AreInSameDirectory(string path1, string path2)
    {
        var dir1 = System.IO.Path.GetDirectoryName(path1);
        var dir2 = System.IO.Path.GetDirectoryName(path2);
        return dir1 == dir2;
    }

    private static HashSet<string> CollectGeneratedEventTypes(SyntaxTree generatedTree, string className)
    {
        var root = generatedTree.GetRoot();

        // Find the Fold method in the generated partial class and collect event types
        var eventTypes = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.Identifier.ValueText == className)
            .Select(c => c.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == "Fold" ||
                                     m.Identifier.ValueText == "DispatchToWhen"))
            .Where(m => m?.Body != null)
            .SelectMany(m => m!.Body!.DescendantNodes().OfType<SwitchStatementSyntax>())
            .SelectMany(s => s.Sections)
            .SelectMany(section => section.DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Select(ExtractEventTypeFromInvocation)
            .Where(name => name != null);

        return new HashSet<string>(eventTypes!);
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
        return classDecl.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => p.Modifiers.Any(SyntaxKind.PublicKeyword))
            .Where(p => !p.Modifiers.Any(SyntaxKind.StaticKeyword))
            .Where(p => p.ExpressionBody != null ||
                       (p.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) ?? false))
            .Select(p => (p.Identifier.ValueText, p.Identifier.GetLocation()))
            .ToList();
    }

    /// <summary>
    /// Collects property names from the generated interface (I{ClassName}).
    /// </summary>
    private static HashSet<string> CollectGeneratedInterfaceProperties(SyntaxTree generatedTree, string className)
    {
        var root = generatedTree.GetRoot();
        var interfaceName = $"I{className}";

        var properties = root.DescendantNodes()
            .OfType<InterfaceDeclarationSyntax>()
            .Where(i => i.Identifier.ValueText == interfaceName)
            .SelectMany(i => i.Members.OfType<PropertyDeclarationSyntax>())
            .Select(p => p.Identifier.ValueText);

        return new HashSet<string>(properties);
    }
}
