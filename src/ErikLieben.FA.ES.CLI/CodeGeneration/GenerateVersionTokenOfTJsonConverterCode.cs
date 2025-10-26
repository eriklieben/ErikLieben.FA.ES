using System.Text;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using ErikLieben.FA.ES.JsonConverters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

public class GenerateVersionTokenOfTJsonConverterCode
{

    private readonly SolutionDefinition solution;
    private readonly Config config;
    private readonly string solutionPath;

    public GenerateVersionTokenOfTJsonConverterCode(SolutionDefinition solution, Config config, string solutionPath)
    {
        this.solution = solution;
        this.config = config;
        this.solutionPath = solutionPath;
    }

    public async Task Generate()
    {
        foreach (var project in solution.Projects.Where(p => !p.Name.StartsWith("ErikLieben.FA.ES")))
        {
            foreach (var versionTokenJsonConverter in project.VersionTokenJsonConverterDefinitions)
            {
                AnsiConsole.MarkupLine($"Generating supporting partial class for: [darkcyan]{versionTokenJsonConverter.Name}[/]");
                var currentFile = versionTokenJsonConverter.FileLocations.FirstOrDefault();
                if (currentFile is null || currentFile.Contains(".generated", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rel = (versionTokenJsonConverter.FileLocations.FirstOrDefault() ?? string.Empty).Replace('\\', '/');
                var relGen = rel.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    ? string.Concat(rel.AsSpan(0, rel.Length - 3), ".Generated.cs")
                    : rel + ".Generated.cs";
                var normalized = relGen.Replace('/', System.IO.Path.DirectorySeparatorChar)
                    .TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                var path = System.IO.Path.Combine(solutionPath, normalized);
                AnsiConsole.MarkupLine($"Path: [blue]{path}[/]");
                await GenerateVersionToken(versionTokenJsonConverter, path, config, project.VersionTokens);
            }

            // Generate json
        }
    }

    private async Task GenerateVersionToken(VersionTokenJsonConverterDefinition versionTokenJsonConverter, string? path, Config config, List<VersionTokenDefinition> versionTokens)
    {
        if (!versionTokenJsonConverter.IsPartialClass)
        {
            AnsiConsole.MarkupLine(
                $"[red][bold]ERROR:[/] Skipping [underline]{versionTokenJsonConverter.Name}[/] class; it needs to be partial to support generated code, make it partial please.[/]");
            return;
        }

        var usings = new List<string>
        {
            "System.Text.Json",
            "ErikLieben.FA.ES"
        };
        usings.AddRange(versionTokens.Select(versionToken => versionToken.NamespaceOfType));

        var code = new StringBuilder();
        foreach (var namespaceName in usings.Order())
        {
            code.AppendLine($"using {namespaceName};");
        }
        code.Append($$"""

                      namespace {{versionTokenJsonConverter.Namespace}};
                        
                      #nullable enable
                      public partial class {{versionTokenJsonConverter.Name}}<T>
                      {
                      public override partial T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                      {
                      var versionToken = Converter.Read(ref reader, typeof(VersionToken), options);
                      return typeof(T) switch
                      {
                      """);

        foreach (var versionToken in versionTokens)
        {
            code.AppendLine($$"""{ } when typeof(T) == typeof({{versionToken.Name}}) => new {{versionToken.Name}}(versionToken.Value) as T,""");
        }

        code.Append($$"""
                        _ => null
                    };
                }
            }
            #nullable restore
            """);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path!)!);
        await File.WriteAllTextAsync(path!, FormatCode(code.ToString()));
    }


    private static string FormatCode(string code, CancellationToken cancelToken = default)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancelToken);
        var syntaxNode = syntaxTree.GetRoot(cancelToken);

        using var workspace = new AdhocWorkspace();
        var options = workspace.Options
            .WithChangedOption(FormattingOptions.SmartIndent, LanguageNames.CSharp,
                FormattingOptions.IndentStyle.Smart);

        var formattedNode = Formatter.Format(syntaxNode, workspace, options, cancellationToken: cancelToken);
        return formattedNode.ToFullString();
    }
}
