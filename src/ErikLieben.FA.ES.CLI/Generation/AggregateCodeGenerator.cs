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
        var code = GenerateAggregateCode.AssembleAggregateCode(
            aggregate, usings, postWhenCode, foldCode, serializableCode,
            propertyCode, propertySnapshotCode, get, ctorInput, setupCode, processSnapshotCode, version);

        await WriteCodeAsync(path, code.ToString(), projectDir, cancellationToken);
    }
}
