using System.IO;
using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.Analyze;

public class AnalyzeProjections
{
    private readonly INamedTypeSymbol? classSymbol;
    private readonly string solutionRootPath;
    private readonly RoslynHelper roslyn;
    private readonly Compilation compilation;

    public AnalyzeProjections(
        INamedTypeSymbol? classSymbol,
        SemanticModel semanticModel,
        Compilation compilation,
        string solutionRootPath)
    {
        ArgumentNullException.ThrowIfNull(classSymbol);
        ArgumentNullException.ThrowIfNull(semanticModel);
        ArgumentNullException.ThrowIfNull(solutionRootPath);
        ArgumentNullException.ThrowIfNull(compilation);

        this.classSymbol = classSymbol;
        this.solutionRootPath = solutionRootPath;
        this.compilation = compilation;
        roslyn = new RoslynHelper(semanticModel, solutionRootPath);
    }
    public void Run(List<ProjectionDefinition> projections)
    {
        if (classSymbol == null)
        {
            return;
        }

        if (classSymbol.ContainingAssembly.Identity.Name.StartsWith("ErikLieben.FA.ES"))
        {
            return;
        }

        if (!InheritsFromProjection(classSymbol))
        {
            return;
        }

        AnsiConsole.MarkupLine("Analyzing projection: [yellow]" + classSymbol!.Name + "[/]");

        var projectionDefinition = FindOrCreateProjection(projections);
        projectionDefinition.Constructors.AddRange(ConstructorHelper.GetConstructors(classSymbol));
        projectionDefinition.Properties.AddRange(PropertyHelper.GetPublicGetterPropertiesIncludeParentDefinitions(classSymbol));
        projectionDefinition.Events.AddRange(WhenMethodHelper.GetEventDefinitions(classSymbol, compilation, roslyn));
        projectionDefinition.PostWhen = PostWhenHelper.GetPostWhenMethod(classSymbol);
        projectionDefinition.HasPostWhenAllMethod = PostWhenHelper.HasPostWhenAllMethod(classSymbol);
        var (isBlobJsonProjection, containerValue, connectionValue) = CheckBlobJsonProjection(classSymbol);
        if (isBlobJsonProjection)
        {
            projectionDefinition.BlobProjection = new BlobProjectionDefinition
            {
                Container = containerValue ?? string.Empty,
                Connection = connectionValue ?? string.Empty,
            };
        }

        var (isCosmosDbProjection, cosmosContainer, cosmosConnection, partitionKeyPath) = CheckCosmosDbJsonProjection(classSymbol);
        if (isCosmosDbProjection)
        {
            projectionDefinition.CosmosDbProjection = new CosmosDbProjectionDefinition
            {
                Container = cosmosContainer ?? string.Empty,
                Connection = cosmosConnection ?? string.Empty,
                PartitionKeyPath = partitionKeyPath ?? "/projectionName",
            };
        }
    }

    private ProjectionDefinition FindOrCreateProjection(List<ProjectionDefinition> projections)
    {
        var projection = projections.FirstOrDefault(p =>
            p.Name == classSymbol?.Name && p.Namespace == RoslynHelper.GetFullNamespace(classSymbol));

        if (projection != null)
        {
            return projection;
        }

        // Check if this is a routed projection
        var isRoutedProjection = InheritsFromRoutedProjection(classSymbol);
        var destinationTypeSymbols = isRoutedProjection ? ExtractDestinationTypeSymbols(classSymbol) : new Dictionary<string, ITypeSymbol>();

        if (isRoutedProjection)
        {
            // Extract path templates from destination types' [BlobJsonProjection] attributes
            var destinationPathTemplates = new Dictionary<string, string>();
            var destinationsWithExternalCheckpoint = new HashSet<string>();

            foreach (var kvp in destinationTypeSymbols)
            {
                var pathTemplate = GetBlobProjectionPath(kvp.Value);
                if (!string.IsNullOrEmpty(pathTemplate))
                {
                    destinationPathTemplates[kvp.Key] = pathTemplate;
                }

                // Check if destination has [ProjectionWithExternalCheckpoint]
                if (kvp.Value is INamedTypeSymbol namedType && HasExternalCheckpoint(namedType))
                {
                    destinationsWithExternalCheckpoint.Add(kvp.Key);
                }
            }

            projection = new RoutedProjectionDefinition
            {
                Name = classSymbol?.Name ?? string.Empty,
                Namespace = RoslynHelper.GetFullNamespace(classSymbol!),
                FileLocations = classSymbol!.DeclaringSyntaxReferences
                    .Select(r => Path.GetRelativePath(solutionRootPath, r.SyntaxTree.FilePath))
                    .ToList(),
                ExternalCheckpoint = HasExternalCheckpoint(classSymbol),
                IsRoutedProjection = true,
                DestinationType = destinationTypeSymbols.Keys.FirstOrDefault(),
                DestinationPathTemplates = destinationPathTemplates,
                DestinationsWithExternalCheckpoint = destinationsWithExternalCheckpoint
            };
        }
        else
        {
            projection = new ProjectionDefinition
            {
                Name = classSymbol?.Name ?? string.Empty,
                Namespace = RoslynHelper.GetFullNamespace(classSymbol!),
                FileLocations = classSymbol!.DeclaringSyntaxReferences
                    .Select(r => Path.GetRelativePath(solutionRootPath, r.SyntaxTree.FilePath))
                    .ToList(),
                ExternalCheckpoint = HasExternalCheckpoint(classSymbol)
            };
        }

        projections.Add(projection);

        return projection;
    }

    private static bool HasExternalCheckpoint(INamedTypeSymbol symbol)
    {
        return symbol?.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "ProjectionWithExternalCheckpointAttribute") != null;
    }

    private static bool InheritsFromProjection(INamedTypeSymbol? type)
    {
        while (type != null)
        {
            if (type.Name == "Projection" && type.ContainingNamespace.ToDisplayString() == "ErikLieben.FA.ES.Projections")
            {
                return true;
            }

            type = type.BaseType;
        }
        return false;
    }


    private static (bool, string?,string?) CheckBlobJsonProjection(INamedTypeSymbol classSymbol)
    {
        var attributes = classSymbol.GetAttributes();

        var blobJsonAttribute = attributes.FirstOrDefault(a =>
            a.AttributeClass?.Name == "BlobJsonProjectionAttribute" ||
            a.AttributeClass?.ToDisplayString() == "Namespace.BlobJsonProjectionAttribute");

        if (blobJsonAttribute == null)
        {
            return (false, null, null);
        }

        var projectionsValue = blobJsonAttribute.ConstructorArguments.FirstOrDefault().Value as string;
        var connectionValue = blobJsonAttribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == "Connection").Value.Value as string;
        return (true, projectionsValue, connectionValue);

    }

    private static (bool, string?, string?, string?) CheckCosmosDbJsonProjection(INamedTypeSymbol classSymbol)
    {
        var attributes = classSymbol.GetAttributes();

        var cosmosDbAttribute = attributes.FirstOrDefault(a =>
            a.AttributeClass?.Name == "CosmosDbJsonProjectionAttribute");

        if (cosmosDbAttribute == null)
        {
            return (false, null, null, null);
        }

        var containerValue = cosmosDbAttribute.ConstructorArguments.FirstOrDefault().Value as string;
        var connectionValue = cosmosDbAttribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == "Connection").Value.Value as string;
        var partitionKeyPath = cosmosDbAttribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == "PartitionKeyPath").Value.Value as string;
        return (true, containerValue, connectionValue, partitionKeyPath);
    }

    private static bool InheritsFromRoutedProjection(INamedTypeSymbol? type)
    {
        while (type != null)
        {
            if (type.Name == "RoutedProjection" && type.ContainingNamespace.ToDisplayString() == "ErikLieben.FA.ES.Projections")
            {
                return true;
            }

            type = type.BaseType;
        }
        return false;
    }

    private Dictionary<string, ITypeSymbol> ExtractDestinationTypeSymbols(INamedTypeSymbol? classSymbol)
    {
        var destinationTypes = new Dictionary<string, ITypeSymbol>();

        if (classSymbol == null)
            return destinationTypes;

        // Get all syntax references for this class
        foreach (var syntaxRef in classSymbol.DeclaringSyntaxReferences)
        {
            var syntaxNode = syntaxRef.GetSyntax();
            var tree = syntaxNode.SyntaxTree;

            // Get descendants and look for invocations of AddDestination<T>
            var invocations = syntaxNode.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var genericName = invocation.Expression.DescendantNodesAndSelf()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax>()
                    .FirstOrDefault(gn => gn.Identifier.Text == "AddDestination");

                if (genericName != null && genericName.TypeArgumentList.Arguments.Count > 0)
                {
                    var typeArg = genericName.TypeArgumentList.Arguments[0];
                    var typeInfo = compilation.GetSemanticModel(tree).GetTypeInfo(typeArg);

                    if (typeInfo.Type != null && !destinationTypes.ContainsKey(typeInfo.Type.Name))
                    {
                        destinationTypes[typeInfo.Type.Name] = typeInfo.Type;
                    }
                }
            }
        }

        return destinationTypes;
    }

    private static string? GetBlobProjectionPath(ITypeSymbol typeSymbol)
    {
        // Look for [BlobJsonProjection("path")] attribute
        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "BlobJsonProjectionAttribute")
            {
                // First constructor argument is the path
                if (attr.ConstructorArguments.Length > 0)
                {
                    return attr.ConstructorArguments[0].Value?.ToString();
                }
            }
        }

        return null;
    }
}
