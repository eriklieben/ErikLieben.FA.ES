using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;

namespace ErikLieben.FA.ES.CLI.Generation;

/// <summary>
/// Generates extension registration code.
/// </summary>
public class ExtensionCodeGenerator : CodeGeneratorBase
{
    public override string Name => "Extensions";

    public ExtensionCodeGenerator(
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

        var legacyGenerator = new GenerateExtensionCode(solution, Config, solutionPath);
        await legacyGenerator.Generate();

        Logger.Log(ActivityType.GenerationCompleted, $"Completed {Name}");
    }
}
