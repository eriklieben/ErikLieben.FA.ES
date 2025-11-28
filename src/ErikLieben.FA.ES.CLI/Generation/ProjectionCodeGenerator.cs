using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;

namespace ErikLieben.FA.ES.CLI.Generation;

/// <summary>
/// Generates supporting partial classes for projections.
/// </summary>
public class ProjectionCodeGenerator : CodeGeneratorBase
{
    public override string Name => "Projections";

    public ProjectionCodeGenerator(
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

        // Use existing generator but capture file writes
        var legacyGenerator = new GenerateProjectionCode(solution, Config, solutionPath);
        await legacyGenerator.Generate();

        Logger.Log(ActivityType.GenerationCompleted, $"Completed {Name}");
    }
}
