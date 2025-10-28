using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.Analyze;

public class AnalyzeInheritedAggregates
{
    private readonly INamedTypeSymbol? typeSymbol;
    private readonly string solutionRootPath;
    private readonly RoslynHelper roslyn;

    public AnalyzeInheritedAggregates(
        INamedTypeSymbol typeSymbol,
        SemanticModel semanticModel,
        string solutionRootPath)
    {
        ArgumentNullException.ThrowIfNull(typeSymbol);
        ArgumentNullException.ThrowIfNull(semanticModel);
        ArgumentNullException.ThrowIfNull(solutionRootPath);

        this.typeSymbol = typeSymbol;
        this.solutionRootPath = solutionRootPath;

        roslyn = new RoslynHelper(semanticModel, solutionRootPath);
    }

    public void Run(List<InheritedAggregateDefinition> aggregates)
    {
        if (!roslyn.IsInheritedAggregate(typeSymbol))
        {
            return;
        }

        AnsiConsole.MarkupLine("Analyzing inherited aggregate: [yellow]" + typeSymbol!.Name + "[/]");
        var declaration = GetOrCreateAggregateDefinition(aggregates);
        declaration.Constructors.AddRange(ConstructorHelper.GetConstructors(typeSymbol));
        declaration.Properties.AddRange(PropertyHelper.GetPublicGetterProperties(typeSymbol));
        declaration.Commands.AddRange(CommandHelper.GetCommandMethods(typeSymbol, roslyn));

        // Extract attribute-based settings
        declaration.EventStreamTypeAttribute = AttributeExtractor.ExtractEventStreamTypeAttribute(typeSymbol);
        declaration.EventStreamBlobSettingsAttribute = AttributeExtractor.ExtractEventStreamBlobSettingsAttribute(typeSymbol);

        AppendIdentifierTypeFromMetadata(declaration);
    }


    private static void AppendIdentifierTypeFromMetadata(InheritedAggregateDefinition aggregate)
    {
        var metadataProperty = aggregate.Properties
            .FirstOrDefault(p => p.Namespace == "ErikLieben.FA.ES" &&
                                 p.Type.StartsWith("ObjectMetadata"));

        var genericType = metadataProperty?.GenericTypes.FirstOrDefault();
        if (genericType == null)
        {
            return;
        }

        aggregate.IdentifierType = genericType.Name;
        aggregate.IdentifierTypeNamespace = genericType.Namespace;
    }

    private InheritedAggregateDefinition GetOrCreateAggregateDefinition(
        List<InheritedAggregateDefinition> aggregates)
    {
        var aggregate = aggregates.FirstOrDefault(a =>
            a.IdentifierName == typeSymbol?.Name && a.Namespace == RoslynHelper.GetFullNamespace(typeSymbol));

        if (aggregate != null)
        {
            return aggregate;
        }

        var parentIdentifierName = string.Empty;
        var parentInterfaceNamespace = string.Empty;
        var interfaceIdentifierName = string.Empty;
        var interfaceIdentifierNamespace = string.Empty;
        var parentType = GetParentType(typeSymbol);
        if (parentType != null)
        {
            parentIdentifierName = RoslynHelper.GetFullTypeName(parentType);
            parentInterfaceNamespace = RoslynHelper.GetFullNamespace(parentType);
            interfaceIdentifierNamespace = RoslynHelper.GetFullNamespace(parentType);
            interfaceIdentifierName = $"I{parentType.Name}";
        }

        aggregate = new InheritedAggregateDefinition
        {
            IdentifierName = typeSymbol!.Name,
            ObjectName = RoslynHelper.GetObjectName(parentType!),
            Namespace = RoslynHelper.GetFullNamespace(typeSymbol),
            FileLocations = typeSymbol.DeclaringSyntaxReferences
                .Select(r => NormalizeRelativeFile(r.SyntaxTree.FilePath))
                .ToList(),
            // Default to string, we will later on replace this with the detected type
            IdentifierType = "String",
            IdentifierTypeNamespace = "System",
            InheritedIdentifierName = parentIdentifierName,
            InheritedNamespace = parentInterfaceNamespace,
            ParentInterface = interfaceIdentifierName,
            ParentInterfaceNamespace = interfaceIdentifierNamespace
        };
        aggregates.Add(aggregate);

        return aggregate;
    }

    private string NormalizeRelativeFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return string.Empty;
        var fp = NormalizeSeparators(filePath);
        var root1 = NormalizeSeparators(solutionRootPath);
        var root2 = NormalizeSeparators(solutionRootPath.Replace("\\", "/"));
        var root3 = NormalizeSeparators(solutionRootPath.Replace("/", "\\"));

        foreach (var root in new[] { root1, root2, root3 })
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var idx = fp.IndexOf(root, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var rel = fp[(idx + root.Length)..];
                return rel.TrimStart(Path.DirectorySeparatorChar, '/','\\');
            }
        }

        // Fallback: return normalized file path
        return fp;
    }

    private static string NormalizeSeparators(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var sep = Path.DirectorySeparatorChar;
        return path.Replace('\\', sep).Replace('/', sep);
    }


    private static INamedTypeSymbol? GetParentType(INamedTypeSymbol? type)
    {
        while (type != null)
        {
            if (type.BaseType is { Name: "Aggregate" } &&
                type.BaseType.ContainingNamespace.ToDisplayString() == "ErikLieben.FA.ES.Processors")
            {
                return type;
            }

            type = type.BaseType;
        }

        return type;
    }
}
