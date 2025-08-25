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

        if (classSymbol?.ContainingAssembly.Identity.Name.StartsWith("ErikLieben.FA.ES") ?? false)
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
    }

    private ProjectionDefinition FindOrCreateProjection(List<ProjectionDefinition> projections)
    {
        var projection = projections.FirstOrDefault(p =>
            p.Name == classSymbol?.Name && p.Namespace == RoslynHelper.GetFullNamespace(classSymbol));

        if (projection != null)
        {
            return projection;
        }

        projection = new ProjectionDefinition
        {
            Name = classSymbol?.Name ?? string.Empty,
            Namespace = RoslynHelper.GetFullNamespace(classSymbol!),
            FileLocations = classSymbol!.DeclaringSyntaxReferences
                .Select(r => Path.GetRelativePath(solutionRootPath, r.SyntaxTree.FilePath))
                .ToList(),
            ExternalCheckpoint = HasExternalCheckpoint(classSymbol)
        };
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
}
