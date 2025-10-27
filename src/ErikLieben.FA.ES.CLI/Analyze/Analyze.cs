﻿using System.Diagnostics;
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
                        await ProcessProjectAsync(project, solutionDefinition, solutionRootPath, maxConcurrency, taskbar);
                    }
                    catch (Exception ex)
                    {
                        HandleProjectFailure(project, ex, taskbar, stopwatch);
                        failed = true;
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

    private async Task ProcessProjectAsync(
        Project project,
        SolutionDefinition solutionDefinition,
        string solutionRootPath,
        int maxConcurrency,
        ProgressTask taskbar)
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

        await ProcessSyntaxTreesAsync(compilation, projectDefinition, solutionRootPath, maxConcurrency, taskbar);

        if (HasRelevantDefinitions(projectDefinition))
        {
            solutionDefinition.Projects.Add(projectDefinition);
        }
    }

    private static bool HasRelevantDefinitions(ProjectDefinition projectDefinition)
    {
        return projectDefinition.Projections.Count != 0 ||
               projectDefinition.Aggregates.Count != 0 ||
               projectDefinition.InheritedAggregates.Count != 0 ||
               projectDefinition.VersionTokens.Count != 0;
    }

    private static async Task ProcessSyntaxTreesAsync(
        Compilation compilation,
        ProjectDefinition projectDefinition,
        string solutionRootPath,
        int maxConcurrency,
        ProgressTask taskbar)
    {
        var innerSemaphore = new SemaphoreSlim(maxConcurrency);
        var syntaxTreeTasks = compilation.SyntaxTrees.Select(async syntaxTree =>
        {
            await innerSemaphore.WaitAsync();
            try
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync();

                var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
                ProcessClassDeclarations(classDeclarations, semanticModel, compilation, projectDefinition, solutionRootPath, taskbar);

                var recordDeclarations = root.DescendantNodes().OfType<RecordDeclarationSyntax>().ToList();
                ProcessRecordDeclarations(recordDeclarations, semanticModel, projectDefinition, solutionRootPath, taskbar);
            }
            finally
            {
                innerSemaphore.Release();
            }
        });

        await Task.WhenAll(syntaxTreeTasks);
    }

    private static void ProcessClassDeclarations(
        List<ClassDeclarationSyntax> classDeclarations,
        SemanticModel semanticModel,
        Compilation compilation,
        ProjectDefinition projectDefinition,
        string solutionRootPath,
        ProgressTask taskbar)
    {
        foreach (var classDeclaration in classDeclarations)
        {
            if (!TryGetUserDefinedClassSymbol(classDeclaration, semanticModel, out var classSymbol))
            {
                taskbar.Increment(1);
                continue;
            }

            new AnalyzeAggregates(classSymbol, classDeclaration, semanticModel, compilation, solutionRootPath)
                .Run(projectDefinition.Aggregates);

            new AnalyzeInheritedAggregates(classSymbol, semanticModel, solutionRootPath)
                .Run(projectDefinition.InheritedAggregates);

            new AnalyzeProjections(classSymbol, semanticModel, compilation, solutionRootPath)
                .Run(projectDefinition.Projections);

            new AnalyzeVersionTokenOfTJsonConverter(classSymbol, solutionRootPath)
                .Run(projectDefinition.VersionTokenJsonConverterDefinitions);

            taskbar.Increment(1);
        }
    }

    private static void ProcessRecordDeclarations(
        List<RecordDeclarationSyntax> recordDeclarations,
        SemanticModel semanticModel,
        ProjectDefinition projectDefinition,
        string solutionRootPath,
        ProgressTask taskbar)
    {
        foreach (var recordDeclaration in recordDeclarations)
        {
            if (!TryGetUserDefinedRecordSymbol(recordDeclaration, semanticModel, out var recordSymbol))
            {
                taskbar.Increment(1);
                continue;
            }

            new AnalyzeVersionTokenOfT(recordSymbol, solutionRootPath)
                .Run(projectDefinition.VersionTokens);
        }
    }

    private static bool TryGetUserDefinedClassSymbol(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        out INamedTypeSymbol classSymbol)
    {
        classSymbol = null!;

        if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol symbol)
        {
            return false;
        }

        if (symbol.ContainingAssembly.Name.StartsWith("ErikLieben.FA.ES"))
        {
            return false;
        }

        if (classDeclaration.SyntaxTree.FilePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        classSymbol = symbol;
        return true;
    }

    private static bool TryGetUserDefinedRecordSymbol(
        RecordDeclarationSyntax recordDeclaration,
        SemanticModel semanticModel,
        out INamedTypeSymbol recordSymbol)
    {
        recordSymbol = null!;

        if (semanticModel.GetDeclaredSymbol(recordDeclaration) is not INamedTypeSymbol symbol)
        {
            return false;
        }

        if (symbol.ContainingAssembly.Name.StartsWith("ErikLieben.FA.ES"))
        {
            return false;
        }

        if (recordDeclaration.SyntaxTree.FilePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        recordSymbol = symbol;
        return true;
    }

    private void HandleProjectFailure(Project project, Exception ex, ProgressTask taskbar, Stopwatch stopwatch)
    {
        console.MarkupLine($"[red]Failed to analyze project:[/] {project.Name}");
        console.MarkupLine($"[red]Exception:[/] {ex.Message}");
        console.MarkupLine($"[red]Stack trace:[/] [white dim]{ex.StackTrace}[/]");
        stopwatch.Stop();
        taskbar.Description = $"[red]Analyze Failed[/][white dim] - Total time: {stopwatch.Elapsed:hh\\:mm\\:ss}[/]";
        taskbar.Value(0);
        taskbar.StopTask();
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
