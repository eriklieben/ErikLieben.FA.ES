using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.Analyze;

public class Analyze(Config config, IAnsiConsole console)
{
    private readonly Config config = config;

    public async Task<(SolutionDefinition, string)> AnalyzeAsync(string solutionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        console.MarkupLine($"Code generator version: [yellow]{GetGeneratorVersion()}[/]");
        var workspace = MSBuildWorkspace.Create();
        console.MarkupLine($"Loading solution: [gray42]{solutionPath}[/]");

        var solution = await workspace.OpenSolutionAsync(solutionPath);
        var solutionDefinition = new SolutionDefinition
        {
            SolutionName = Path.GetFileNameWithoutExtension(solution.FilePath) ?? "Unknown",
            Generator = new GeneratorInformation
            {
                Version = GetGeneratorVersion()
            }
        };
        var solutionRootPath = Path.GetDirectoryName(solution.FilePath) ??
                               throw new InvalidOperationException("Failed to get solution root path");

        var maxConcurrency = Environment.ProcessorCount;
        var semaphore = new SemaphoreSlim(maxConcurrency);

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {

                var taskbar = ctx.AddTask("[yellow]Analyzing projects[/]", maxValue: await CountClassDeclarationsAsync(solution));
                var stopwatch = Stopwatch.StartNew();
                taskbar.StartTask();

                var failed = false;
                var projectTasks = solution.Projects.Select(async project =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        console.MarkupLine($"Loading project: [gray54]{project.FilePath}[/]");
                        var compilation = await project.GetCompilationAsync();
                        if (compilation == null)
                        {
                            console.MarkupLine($"[red]Failed to compile project:[/] {project.Name}");
                            return;
                        }

                        if (config.Es.EnableDiagnostics)
                        {
                            LogCompilationIssues(compilation);
                        }

                        var projectDefinition = new ProjectDefinition
                        {
                            Name = project.Name,
                            FileLocation = project.FilePath?.Replace(solutionRootPath, string.Empty) ?? string.Empty,
                            Namespace = project.DefaultNamespace ?? string.Empty,
                        };

                        var innerSemaphore = new SemaphoreSlim(maxConcurrency);
                        var syntaxTreeTasks = compilation.SyntaxTrees.Select(async syntaxTree =>
                        {
                            await innerSemaphore.WaitAsync();
                            try
                            {
                                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                                var root = await syntaxTree.GetRootAsync();
                                var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                                var classDeclarationsList = classDeclarations.ToList();
                                classDeclarationsList.ForEach(classDeclaration =>
                                {
                                    if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol
                                            classSymbol || classSymbol.ContainingAssembly.Name.StartsWith("ErikLieben.FA.ES") ||
                                        classDeclaration.SyntaxTree.FilePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase))
                                    {
                                        taskbar.Increment(1);
                                        return;
                                    }

                                    new AnalyzeAggregates(
                                        classSymbol,
                                        classDeclaration,
                                        semanticModel,
                                        compilation,
                                        solutionRootPath).Run(projectDefinition.Aggregates);

                                    new AnalyzeInheritedAggregates(
                                        classSymbol,
                                        semanticModel,
                                        solutionRootPath).Run(projectDefinition.InheritedAggregates);

                                    new AnalyzeProjections(
                                        classSymbol,
                                        semanticModel,
                                        compilation,
                                        solutionRootPath).Run(projectDefinition.Projections);


                                    new AnalyzeVersionTokenOfTJsonConverter(
                                        classSymbol,
                                        solutionRootPath).Run(projectDefinition.VersionTokenJsonConverterDefinitions);

                                    taskbar.Increment(1);
                                });

                                var recordDeclarations = root.DescendantNodes().OfType<RecordDeclarationSyntax>();
                                var recordDeclarationsList = recordDeclarations.ToList();
                                recordDeclarationsList.ForEach(recordDeclaration =>
                                {

                                    if (semanticModel.GetDeclaredSymbol(recordDeclaration) is not INamedTypeSymbol
                                            recordSymbol || recordSymbol.ContainingAssembly.Name.StartsWith("ErikLieben.FA.ES") ||
                                        recordDeclaration.SyntaxTree.FilePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase))
                                    {
                                        taskbar.Increment(1);
                                        return;
                                    }

                                    new AnalyzeVersionTokenOfT(
                                            recordSymbol,
                                            solutionRootPath)
                                        .Run(projectDefinition.VersionTokens);


                                });


                            }
                            finally
                            {
                                innerSemaphore.Release();
                            }
                        });

                        await Task.WhenAll(syntaxTreeTasks);


                        if (projectDefinition.Projections.Count != 0 ||
                            projectDefinition.Aggregates.Count != 0 ||
                            projectDefinition.InheritedAggregates.Count != 0 ||
                            projectDefinition.VersionTokens.Count != 0)
                        {
                            solutionDefinition.Projects.Add(projectDefinition);
                        }

                    }
                    catch (Exception ex)
                    {
                        console.MarkupLine($"[red]Failed to analyze project:[/] {project.Name}");
                        console.MarkupLine($"[red]Exception:[/] {ex.Message}");
                        console.MarkupLine($"[red]Stack trace:[/] [white dim]{ex.StackTrace}[/]");
                        failed = true;
                        stopwatch.Stop();
                        taskbar.Description = $"[red]Analyze Failed[/][white dim] - Total time: {stopwatch.Elapsed:hh\\:mm\\:ss}[/]";
                        taskbar.Value(0);
                        taskbar.StopTask();

                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });


                await Task.WhenAll(projectTasks);

                if (!failed)
                {
                    stopwatch.Stop();
                    taskbar.Description = $"[green]Analyze Completed[/][white dim] - Total time: {stopwatch.Elapsed:hh\\:mm\\:ss}[/]";
                    taskbar.StopTask();
                }
            });
        return (solutionDefinition, solutionRootPath);
    }

    private static void LogCompilationIssues(Compilation compilation)
    {
        var diagnostics = compilation.GetDiagnostics();
        foreach (var diagnostic in diagnostics)
        {
            var severityColor = diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => "red",
                DiagnosticSeverity.Warning => "yellow",
                DiagnosticSeverity.Info => "blue",
                _ => "white"
            };

            var escapedId = Markup.Escape(diagnostic.Id);
            var escapedMessage = Markup.Escape(diagnostic.GetMessage(CultureInfo.InvariantCulture));
            AnsiConsole.MarkupLine($"[{severityColor}]{diagnostic.Severity}[/] {escapedId} - {escapedMessage}");

            if (diagnostic.Location.IsInSource)
            {
                var lineSpan = diagnostic.Location.GetLineSpan();
                var escapedPath = Markup.Escape(lineSpan.Path);
                AnsiConsole.MarkupLine($"  at [green]{escapedPath}[/]:line {lineSpan.StartLinePosition.Line + 1}, column {lineSpan.StartLinePosition.Character + 1}");

                if (diagnostic.Location.SourceTree != null)
                {
                    var text = diagnostic.Location.SourceTree.ToString();
                    var span = diagnostic.Location.SourceSpan;
                    var startLine = text.Take(span.Start).Count(c => c == '\n');
                    if (startLine < text.Split('\n').Length)
                    {
                        var line = text.Split('\n')[startLine].Trim();
                        var escapedLine = Markup.Escape(line);
                        AnsiConsole.MarkupLine($"  Code: [dim]{escapedLine}[/]");
                    }
                }
            }

            AnsiConsole.WriteLine();
        }
    }

    private static async Task<int> CountClassDeclarationsAsync(Solution solution)
    {
        var totalTasksItems = 0;

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                var syntaxTree = await document.GetSyntaxTreeAsync();
                if (syntaxTree is null)
                {
                    continue;
                }

                var root = await syntaxTree.GetRootAsync();
                var count = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Count();
                totalTasksItems += count;
            }
        }

        return totalTasksItems;
    }

    private static string? GetGeneratorVersion()
    {
        var generatorVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyFileVersionAttribute>()
            ?.Version;
        return generatorVersion;
    }
}
