#pragma warning disable S2589 // Boolean expressions should not be gratuitous - defensive length checks after StringBuilder modification

using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;

namespace ErikLieben.FA.ES.CLI.Generation;

/// <summary>
/// Generates supporting partial classes for aggregates.
/// </summary>
public class AggregateCodeGenerator : CodeGeneratorBase
{
    public override string Name => "Aggregates";

    public AggregateCodeGenerator(
        IActivityLogger logger,
        ICodeWriter codeWriter,
        Config config)
        : base(logger, codeWriter, config)
    {
    }

    public override async Task GenerateAsync(
        SolutionDefinition solution,
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        Logger.Log(ActivityType.GenerationStarted, $"Generating {Name}");

        foreach (var project in solution.Projects.Where(p => !p.Name.StartsWith("ErikLieben.FA.ES")))
        {
            foreach (var aggregate in project.Aggregates)
            {
                await GenerateAggregateAsync(aggregate, solutionPath, cancellationToken);
            }
        }

        Logger.Log(ActivityType.GenerationCompleted, $"Completed {Name}");
    }

    private async Task GenerateAggregateAsync(
        AggregateDefinition aggregate,
        string solutionPath,
        CancellationToken cancellationToken)
    {
        var currentFile = Path.Combine(solutionPath, aggregate.FileLocations.FirstOrDefault()
            ?? throw new InvalidOperationException($"No file location for aggregate {aggregate.IdentifierName}"));

        if (currentFile.Contains(".generated"))
        {
            return;
        }

        if (!aggregate.IsPartialClass)
        {
            Logger.Log(ActivityType.Warning,
                $"Skipping {aggregate.IdentifierName}: class must be partial to support generated code",
                "Aggregate", aggregate.IdentifierName);
            return;
        }

        Logger.Log(ActivityType.Info,
            $"Generating partial class for: {aggregate.IdentifierName}",
            "Aggregate", aggregate.IdentifierName);

        var path = GetGeneratedFilePath(solutionPath, aggregate.FileLocations.FirstOrDefault() ?? string.Empty);
        var projectDir = GetProjectDirectory(path);

        // Use the existing code generation logic
        var usings = GenerateAggregateCode.BuildUsings(aggregate);
        var postWhenCode = GenerateAggregateCode.GeneratePostWhenCode(aggregate, usings);
        var foldCode = GenerateAggregateCode.GenerateFoldCode(aggregate, usings);
        var serializableCode = GenerateAggregateCode.GenerateJsonSerializableCode(aggregate, usings);
        var (propertyCode, propertySnapshotCode) = GenerateAggregateCode.GeneratePropertyCode(aggregate, serializableCode);

        // Remove trailing newline from serializableCode
        if (serializableCode.Length > 0 && serializableCode[serializableCode.Length - 1] == '\n')
        {
            serializableCode.Length--;
            if (serializableCode.Length > 0 && serializableCode[serializableCode.Length - 1] == '\r')
            {
                serializableCode.Length--;
            }
        }

        var (get, ctorInput) = GenerateAggregateCode.GenerateConstructorParameters(aggregate);
        var setupCode = GenerateAggregateCode.GenerateSetupCode(aggregate);
        var version = "1.0.0";
        var processSnapshotCode = GenerateAggregateCode.GenerateProcessSnapshotCode(aggregate, version);

        // Determine if factory/repository should be in separate files
        var factoryOutputPath = ResolveFactoryOutputPath(aggregate, solutionPath, projectDir);
        var repositoryOutputPath = ResolveRepositoryOutputPath(aggregate, solutionPath, projectDir);

        if (factoryOutputPath != null || repositoryOutputPath != null)
        {
            // Split mode: always write all three files when any split is needed
            var coreCode = GenerateAggregateCode.AssembleAggregateCore(
                aggregate, usings, postWhenCode, foldCode, serializableCode,
                propertyCode, propertySnapshotCode, setupCode, processSnapshotCode, version,
                factorySeparate: true);

            await WriteCodeAsync(path, coreCode.ToString(), projectDir, cancellationToken);

            // Write factory file
            var factoryPath = factoryOutputPath
                ?? GetGeneratedFilePathForType(path, $"{aggregate.IdentifierName}Factory");
            var factoryNamespace = aggregate.UserDefinedFactoryNamespace
                ?? ResolveConfigNamespace(Config.Generation.Factory.Namespace, aggregate.Namespace);
            var factoryCode = GenerateAggregateCode.AssembleFactoryFile(
                aggregate, usings, get, ctorInput, version,
                factoryNamespace);
            await WriteCodeAsync(factoryPath, factoryCode.ToString(), GetProjectDirectory(factoryPath), cancellationToken);

            Logger.Log(ActivityType.Info,
                $"  Factory written to: {factoryPath}",
                "Aggregate", aggregate.IdentifierName);

            // Write repository file
            var repoPath = repositoryOutputPath
                ?? GetGeneratedFilePathForType(path, $"{aggregate.IdentifierName}Repository");
            var repoNamespace = aggregate.UserDefinedRepositoryNamespace
                ?? ResolveConfigNamespace(Config.Generation.Repository.Namespace, aggregate.Namespace);
            var repoCode = GenerateAggregateCode.AssembleRepositoryFile(
                aggregate, usings, repoNamespace, factoryNamespace);
            await WriteCodeAsync(repoPath, repoCode.ToString(), GetProjectDirectory(repoPath), cancellationToken);

            Logger.Log(ActivityType.Info,
                $"  Repository written to: {repoPath}",
                "Aggregate", aggregate.IdentifierName);
        }
        else
        {
            // Default: everything in one file (backward compatible)
            var code = GenerateAggregateCode.AssembleAggregateCode(
                aggregate, usings, postWhenCode, foldCode, serializableCode,
                propertyCode, propertySnapshotCode, get, ctorInput, setupCode, processSnapshotCode, version);

            await WriteCodeAsync(path, code.ToString(), projectDir, cancellationToken);
        }
    }

    /// <summary>
    /// Resolves the factory output path based on detection > config > default priority.
    /// Returns null if factory should remain in the aggregate's generated file.
    /// </summary>
    private string? ResolveFactoryOutputPath(AggregateDefinition aggregate, string solutionPath, string? projectDir)
    {
        // Priority 1: User-defined partial detected
        if (aggregate.UserDefinedFactoryFileLocation != null)
        {
            return GetGeneratedFilePath(solutionPath, aggregate.UserDefinedFactoryFileLocation);
        }

        // Priority 2: Config-based output directory
        if (Config.Generation.Factory.OutputDirectory != null && projectDir != null)
        {
            var outputDir = Path.Combine(projectDir, Config.Generation.Factory.OutputDirectory);
            return Path.Combine(outputDir, $"{aggregate.IdentifierName}Factory.Generated.cs");
        }

        // Priority 3: Default (null = include in aggregate file)
        return null;
    }

    /// <summary>
    /// Resolves the repository output path based on detection > config > default priority.
    /// Returns null if repository should remain in the aggregate's generated file.
    /// </summary>
    private string? ResolveRepositoryOutputPath(AggregateDefinition aggregate, string solutionPath, string? projectDir)
    {
        // Priority 1: User-defined partial detected
        if (aggregate.UserDefinedRepositoryFileLocation != null)
        {
            return GetGeneratedFilePath(solutionPath, aggregate.UserDefinedRepositoryFileLocation);
        }

        // Priority 2: Config-based output directory
        if (Config.Generation.Repository.OutputDirectory != null && projectDir != null)
        {
            var outputDir = Path.Combine(projectDir, Config.Generation.Repository.OutputDirectory);
            return Path.Combine(outputDir, $"{aggregate.IdentifierName}Repository.Generated.cs");
        }

        // Priority 3: Default (null = include in aggregate file)
        return null;
    }

    /// <summary>
    /// Resolves a namespace template, replacing {ProjectNamespace} with the aggregate namespace.
    /// </summary>
    internal static string? ResolveConfigNamespace(string? namespaceTemplate, string aggregateNamespace)
    {
        if (namespaceTemplate == null) return null;

        // Extract project namespace (root namespace, typically the first two segments)
        var projectNamespace = aggregateNamespace.Contains('.')
            ? aggregateNamespace[..aggregateNamespace.LastIndexOf('.')]
            : aggregateNamespace;

        return namespaceTemplate.Replace("{ProjectNamespace}", projectNamespace);
    }

    /// <summary>
    /// Gets a generated file path for a specific type in the same directory as the aggregate.
    /// </summary>
    private static string GetGeneratedFilePathForType(string aggregateGeneratedPath, string typeName)
    {
        var directory = Path.GetDirectoryName(aggregateGeneratedPath)!;
        return Path.Combine(directory, $"{typeName}.Generated.cs");
    }
}
